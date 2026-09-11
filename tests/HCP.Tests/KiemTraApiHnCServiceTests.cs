using System.Net;
using System.Text;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Logging;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCP.Tests;

/// <summary>
/// Màn Kiểm tra API: gọi đúng URL + ký đúng (PATH không kèm query, thân rỗng), API không cần ký
/// thì không gắn chữ ký, và KHÔNG thể lợi dụng tham số đường dẫn để trỏ sang endpoint khác.
/// </summary>
public class KiemTraApiHnCServiceTests
{
    private const string CoSo = "coso-a";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private void SeedCredential()
    {
        using var db = MoDb();
        db.TenantHnCCredentials.Add(new TenantHnCCredential
        {
            TenantId = CoSo, BaseUrl = "https://hnc.example.vn/", ClientId = "c",
            ClientSecretEncrypted = "s", HmacSecretEncrypted = "hmac-A"
        });
        db.SaveChanges();
    }

    private static KiemTraApiHnCService Svc(AppDbContext db, CapturingHandler handler) =>
        new(db, new PassThroughProtector(), new FakeTokenManager(), new HmacSigner(),
            new FakeHttpClientFactory(handler), new SystemLogWriter(db, NullLogger<SystemLogWriter>.Instance),
            NullLogger<KiemTraApiHnCService>.Instance);

    private static Dictionary<string, string?> TS(params (string, string?)[] cap) =>
        cap.ToDictionary(c => c.Item1, c => c.Item2);

    [Fact]
    public async Task Danh_Sach_Don_Ky_Tren_Path_Khong_Query_Va_Tra_JSON_Dep()
    {
        SeedCredential();
        var handler = new CapturingHandler("{\"data\":[{\"code\":\"DH01\",\"school\":{\"name\":\"Trường A\"}}]}");
        using var db = MoDb();

        var kq = await Svc(db, handler).GoiAsync("orders",
            TS(("page", "2"), ("per_page", "50"), ("status", "DA_GIAO"), ("keyword", "bánh mì"), ("code", "")));

        Assert.True(kq.DaGui);
        Assert.Equal(200, kq.HttpStatus);
        // Chỉ tham số có giá trị lên query, giá trị được mã hoá URL.
        Assert.Equal("https://hnc.example.vn/api/supplier/orders?page=2&per_page=50&keyword=b%C3%A1nh%20m%C3%AC&status=DA_GIAO",
            handler.Request!.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer tok", handler.Request.Headers.Authorization!.ToString());

        // Chữ ký tính trên PATH không kèm query, thân rỗng.
        var mongDoi = new HmacSigner().TinhChuKy("hmac-A", "GET", "/api/supplier/orders",
            handler.Header("X-Timestamp"), handler.Header("X-Nonce"), string.Empty);
        Assert.Equal(mongDoi, handler.Header("X-Signature"));

        // JSON được thụt lề, tiếng Việt giữ nguyên (không \uXXXX).
        Assert.True(kq.LaJson);
        Assert.Contains("\n", kq.NoiDung);
        Assert.Contains("Trường A", kq.NoiDung);

        // Mỗi lần gọi đều vào nhật ký hệ thống.
        var log = await db.SystemLogs.SingleAsync();
        Assert.Equal(CoSo, log.TenantId);
        Assert.Equal(200, log.HttpStatusCode);
        Assert.StartsWith("https://hnc.example.vn/api/supplier/orders?", log.RequestUrl);
    }

    [Fact]
    public async Task Chi_Tiet_Don_Ky_Tren_Path_Co_Ma_Don()
    {
        SeedCredential();
        var handler = new CapturingHandler("{\"data\":{}}");
        using var db = MoDb();

        var kq = await Svc(db, handler).GoiAsync("order-item",
            TS(("order_code", "DH-2026.01"), ("product_code", "BANH_MI"), ("trace_code", "T1")));

        Assert.Equal(200, kq.HttpStatus);
        Assert.Equal("/api/supplier/orders/DH-2026.01/items/BANH_MI", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("?trace_code=T1", handler.Request.RequestUri.Query);
        var mongDoi = new HmacSigner().TinhChuKy("hmac-A", "GET", "/api/supplier/orders/DH-2026.01/items/BANH_MI",
            handler.Header("X-Timestamp"), handler.Header("X-Nonce"), string.Empty);
        Assert.Equal(mongDoi, handler.Header("X-Signature"));
    }

    [Fact]
    public async Task Api_Khong_Can_Ky_Thi_Khong_Gan_Chu_Ky()
    {
        SeedCredential();
        var handler = new CapturingHandler("{\"data\":[{\"id\":1,\"name\":\"Mầm non\"}]}");
        using var db = MoDb();

        var kq = await Svc(db, handler).GoiAsync("student-groups", TS());

        Assert.Equal(200, kq.HttpStatus);
        Assert.False(kq.CoKy);
        Assert.Equal("https://hnc.example.vn/api/supplier/student-groups", handler.Request!.RequestUri!.ToString());
        Assert.Equal("", handler.Header("X-Signature"));
        Assert.Equal("Bearer tok", handler.Request.Headers.Authorization!.ToString());
    }

    [Theory]
    [InlineData("DH01/../../token")]
    [InlineData("DH01?x=1")]
    [InlineData("..")]
    [InlineData("DH 01")]
    public async Task Khong_Cho_Chen_Ky_Tu_La_Vao_Duong_Dan(string maDon)
    {
        SeedCredential();
        var handler = new CapturingHandler("{}");
        using var db = MoDb();

        var kq = await Svc(db, handler).GoiAsync("order-detail", TS(("order_code", maDon)));

        Assert.False(kq.DaGui);
        Assert.Null(handler.Request);   // chưa hề gửi gì đi
    }

    [Fact]
    public async Task Thieu_Tham_So_Bat_Buoc_Hoac_Api_La_Thi_Khong_Goi()
    {
        SeedCredential();
        var handler = new CapturingHandler("{}");
        using var db = MoDb();
        var svc = Svc(db, handler);

        Assert.False((await svc.GoiAsync("order-detail", TS(("order_code", "  ")))).DaGui);
        // Không có cách gọi endpoint ngoài danh sách, kể cả merge.
        Assert.False((await svc.GoiAsync("warehouses/merge", TS())).DaGui);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Loi_HTTP_Van_Tra_Nguyen_Van_De_Doc()
    {
        SeedCredential();
        var handler = new CapturingHandler("{\"message\":\"Không tìm thấy đơn hàng\"}", HttpStatusCode.NotFound);
        using var db = MoDb();

        var kq = await Svc(db, handler).GoiAsync("order-detail", TS(("order_code", "KHONG_CO")));

        Assert.True(kq.DaGui);
        Assert.Equal(404, kq.HttpStatus);
        Assert.Contains("Không tìm thấy đơn hàng", kq.NoiDung);
        Assert.Equal("Warning", (await db.SystemLogs.SingleAsync()).Level);
    }

    private sealed class PassThroughProtector : ISecretProtector
    {
        public string Protect(string plainText) => plainText;
        public string? TryUnprotect(string protectedText) => protectedText;
    }

    private sealed class FakeTokenManager : ITenantTokenManager
    {
        public Task<TokenKetQua> LayAccessTokenAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult(TokenKetQua.Co("tok"));
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CapturingHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string Header(string ten) =>
            Request!.Headers.TryGetValues(ten, out var v) ? string.Join(",", v) : "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
