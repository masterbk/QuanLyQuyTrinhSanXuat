using System.Net;
using System.Text;
using System.Text.Json;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCP.Tests;

/// <summary>
/// JSON thật gửi lên POST orders/{code}/process: đặc tả v2.6 mục 11.k cấm có mặt cùng lúc phan_bo và nguyen_lieu
/// trên một dòng (kể cả để null) - dòng thực phẩm chỉ có phan_bo, dòng suất ăn chỉ có nguyen_lieu.
/// </summary>
public class HanoiCheckOrderCommandClientTests
{
    private const string CoSo = "coso-a";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.ClearTenant();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private async Task<JsonElement> GuiAsync(ProcessOrderRequest req)
    {
        using (var db = MoDb())
        {
            db.TenantHnCCredentials.Add(new TenantHnCCredential
            {
                TenantId = CoSo, BaseUrl = "https://hnc.example.vn", ClientId = "c",
                ClientSecretEncrypted = "s", HmacSecretEncrypted = "h", BatDongBo = true
            });
            db.SaveChanges();
        }

        var handler = new BatRequest();
        using var db2 = MoDb();
        var client = new HanoiCheckOrderCommandClient(db2, new KhongMaHoa(), new TokenGia(), new HmacSigner(),
            new HttpGia(handler), NullLogger<HanoiCheckOrderCommandClient>.Instance);
        var kq = await client.XuLyDonAsync(CoSo, "DH-1", req);
        Assert.True(kq.ThanhCong, kq.ThongBao);
        Assert.Equal("https://hnc.example.vn/api/supplier/orders/DH-1/process", handler.Url);
        return JsonDocument.Parse(handler.Body!).RootElement;
    }

    [Fact]
    public async Task Dong_Suat_An_Chi_Co_Nguyen_Lieu()
    {
        var e = await GuiAsync(new ProcessOrderRequest
        {
            MaNguoiGiao = "NV-01",
            ChiTiet =
            {
                new ProcessOrderLine
                {
                    TraceCode = "MTX-0002",
                    NguyenLieu = new()
                    {
                        new ProcessOrderIngredient("TP-001", new() { new ProcessOrderAllocation("LO-001", "KHO-01", 5m) })
                    }
                }
            }
        });

        var dong = e.GetProperty("chi_tiet")[0];
        Assert.Equal("MTX-0002", dong.GetProperty("trace_code").GetString());
        Assert.False(dong.TryGetProperty("phan_bo", out _));          // KHÔNG được có, kể cả null
        Assert.False(dong.TryGetProperty("ma_mon_an", out _));
        var nl = dong.GetProperty("nguyen_lieu")[0];
        Assert.Equal("TP-001", nl.GetProperty("ma_thuc_pham").GetString());
        var pb = nl.GetProperty("phan_bo")[0];
        Assert.Equal(("LO-001", "KHO-01", 5m),
                     (pb.GetProperty("ma_lo").GetString(), pb.GetProperty("ma_kho").GetString(), pb.GetProperty("so_luong").GetDecimal()));
    }

    [Fact]
    public async Task Dong_Thuc_Pham_Chi_Co_Phan_Bo()
    {
        var e = await GuiAsync(new ProcessOrderRequest
        {
            ChiTiet =
            {
                new ProcessOrderLine
                {
                    MaThucPham = "TP-001",
                    PhanBo = { new ProcessOrderAllocation("LO-001", "KHO-01", 6.5m) }
                }
            }
        });

        var dong = e.GetProperty("chi_tiet")[0];
        Assert.Equal("TP-001", dong.GetProperty("ma_thuc_pham").GetString());
        Assert.False(dong.TryGetProperty("nguyen_lieu", out _));
        Assert.Equal(6.5m, dong.GetProperty("phan_bo")[0].GetProperty("so_luong").GetDecimal());
    }

    [Fact]
    public async Task Dong_Suat_An_Chua_Co_Trace_Code_Dung_Ma_Mon_An()
    {
        var e = await GuiAsync(new ProcessOrderRequest
        {
            ChiTiet =
            {
                new ProcessOrderLine
                {
                    MaMonAn = "MA-001",
                    NguyenLieu = new() { new ProcessOrderIngredient("TP-001", new() { new ProcessOrderAllocation("LO-1", "K", 1m) }) }
                }
            }
        });
        var dong = e.GetProperty("chi_tiet")[0];
        Assert.Equal("MA-001", dong.GetProperty("ma_mon_an").GetString());
        Assert.False(dong.TryGetProperty("trace_code", out _));
    }

    // --- Test doubles ---

    private sealed class KhongMaHoa : ISecretProtector
    {
        public string Protect(string plainText) => plainText;
        public string? TryUnprotect(string protectedText) => protectedText;
    }

    private sealed class TokenGia : ITenantTokenManager
    {
        public Task<TokenKetQua> LayAccessTokenAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult(TokenKetQua.Co("access"));
    }

    private sealed class HttpGia(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class BatRequest : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"data\":{}}", Encoding.UTF8, "application/json")
            };
        }
    }
}
