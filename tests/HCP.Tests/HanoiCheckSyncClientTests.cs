using System.Net;
using System.Text;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng tầng gửi request: ký HMAC đúng body và đúng hmac_secret của CƠ SỞ đang gửi,
/// gắn đủ header, và diễn giải phản hồi HnC (2xx / 422 / lỗi tạm) đúng cách.
/// </summary>
public class HanoiCheckSyncClientTests
{
    private const string CoSoA = "coso-a";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.ClearTenant();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private void SeedCredential(string hmacSecret)
    {
        using var db = MoDb();
        db.TenantHnCCredentials.Add(new TenantHnCCredential
        {
            TenantId = CoSoA,
            BaseUrl = "https://hnc.example.vn",
            ClientId = "client-a",
            ClientSecretEncrypted = "secret-a",
            HmacSecretEncrypted = hmacSecret // protector pass-through nên đây coi như đã mã hoá.
        });
        db.SaveChanges();
    }

    private HanoiCheckSyncClient TaoClient(AppDbContext db, CapturingHandler handler,
                                           TokenKetQua? token = null)
    {
        var factory = new FakeHttpClientFactory(handler);
        var tokenManager = new FakeTokenManager(token ?? TokenKetQua.Co("access-XYZ"));
        return new HanoiCheckSyncClient(db, new PassThroughProtector(), tokenManager,
            new HmacSigner(), factory, NullLogger<HanoiCheckSyncClient>.Instance);
    }

    [Fact]
    public async Task Gui_Dung_URL_Header_Va_Chu_Ky_Khop_Body_Voi_Secret_Cua_Co_So()
    {
        SeedCredential("hmac-secret-A");
        var handler = new CapturingHandler(HttpStatusCode.OK, "{\"success\":true,\"data\":{\"total\":1}}");
        using var db = MoDb();
        var client = TaoClient(db, handler);

        const string payload = "[{\"ma_kho\":\"KHO01\",\"ten_kho\":\"Kho A\"}]";
        var kq = await client.GuiMergeAsync(CoSoA, "Warehouse", payload);

        Assert.Equal(SyncTrangThai.ThanhCong, kq.TrangThai);

        // Đúng endpoint.
        Assert.Equal("https://hnc.example.vn/api/supplier/warehouses/merge", handler.Request!.RequestUri!.ToString());
        // Bearer đúng token của cơ sở.
        Assert.Equal("Bearer access-XYZ", handler.Request.Headers.Authorization!.ToString());
        // Đủ 3 header ký.
        var ts = handler.Header("X-Timestamp");
        var nonce = handler.Header("X-Nonce");
        var sig = handler.Header("X-Signature");
        Assert.False(string.IsNullOrEmpty(ts));
        Assert.False(string.IsNullOrEmpty(nonce));

        // Body gửi đi đúng nguyên văn.
        Assert.Equal(payload, handler.Body);

        // Chữ ký phải khớp khi tính lại bằng ĐÚNG hmac_secret của cơ sở trên body đã gửi.
        var mongDoi = new HmacSigner().TinhChuKy("hmac-secret-A", "POST",
            "/api/supplier/warehouses/merge", ts, nonce, payload);
        Assert.Equal(mongDoi, sig);

        // Và KHÔNG khớp nếu ai đó tính bằng secret của cơ sở khác - đảm bảo không dùng nhầm secret.
        var secretKhac = new HmacSigner().TinhChuKy("hmac-secret-KHAC", "POST",
            "/api/supplier/warehouses/merge", ts, nonce, payload);
        Assert.NotEqual(secretKhac, sig);
    }

    [Fact]
    public async Task Phan_Hoi_422_Thi_Bao_LoiDuLieu()
    {
        SeedCredential("hmac-secret-A");
        var handler = new CapturingHandler((HttpStatusCode)422,
            "{\"success\":false,\"errors\":{\"0.ma_kho\":[\"đã tồn tại\"]}}");
        using var db = MoDb();
        var client = TaoClient(db, handler);

        var kq = await client.GuiMergeAsync(CoSoA, "Warehouse", "[{}]");

        Assert.Equal(SyncTrangThai.LoiDuLieu, kq.TrangThai);
        Assert.Equal(422, kq.HttpStatusCode);
        Assert.Contains("ma_kho", kq.ResponseBody);
    }

    [Fact]
    public async Task Phan_Hoi_500_Thi_Bao_LoiTamThoi()
    {
        SeedCredential("hmac-secret-A");
        var handler = new CapturingHandler(HttpStatusCode.InternalServerError, "loi server");
        using var db = MoDb();
        var client = TaoClient(db, handler);

        var kq = await client.GuiMergeAsync(CoSoA, "Warehouse", "[{}]");

        Assert.Equal(SyncTrangThai.LoiTamThoi, kq.TrangThai);
        Assert.Equal(500, kq.HttpStatusCode);
    }

    [Fact]
    public async Task Chua_Cau_Hinh_Token_Thi_Khong_Gui()
    {
        SeedCredential("hmac-secret-A");
        var handler = new CapturingHandler(HttpStatusCode.OK, "{}");
        using var db = MoDb();
        var client = TaoClient(db, handler, TokenKetQua.ChuaCauHinh("chưa nhập credential"));

        var kq = await client.GuiMergeAsync(CoSoA, "Warehouse", "[{}]");

        Assert.Equal(SyncTrangThai.ChuaCauHinh, kq.TrangThai);
        Assert.Null(handler.Request); // tuyệt đối không gọi mạng.
    }

    [Fact]
    public async Task EntityType_La_Thi_Bao_LoiDuLieu_Khong_Gui()
    {
        SeedCredential("hmac-secret-A");
        var handler = new CapturingHandler(HttpStatusCode.OK, "{}");
        using var db = MoDb();
        var client = TaoClient(db, handler);

        var kq = await client.GuiMergeAsync(CoSoA, "KhongTonTai", "[{}]");

        Assert.Equal(SyncTrangThai.LoiDuLieu, kq.TrangThai);
        Assert.Null(handler.Request);
    }

    // --- Test doubles ---

    private sealed class PassThroughProtector : ISecretProtector
    {
        public string Protect(string plainText) => plainText;
        public string? TryUnprotect(string protectedText) => protectedText;
    }

    private sealed class FakeTokenManager(TokenKetQua ketQua) : ITenantTokenManager
    {
        public Task<TokenKetQua> LayAccessTokenAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult(ketQua);
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public CapturingHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        public string Header(string ten) =>
            Request!.Headers.TryGetValues(ten, out var v) ? string.Join(",", v) : "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
