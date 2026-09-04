using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng quản lý token theo từng cơ sở: lấy khi chưa có, tái dùng khi còn hạn,
/// refresh khi hết hạn, và - quan trọng nhất - KHÔNG dùng nhầm secret giữa các cơ sở.
/// </summary>
public class TenantTokenManagerTests
{
    private const string CoSoA = "coso-a";
    private const string CoSoB = "coso-b";

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly FakeTimeProvider _clock = new(new DateTime(2026, 9, 4, 8, 0, 0, DateTimeKind.Utc));

    // --- Hạ tầng giả lập ---

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.ClearTenant(); // job nền chạy ngoài tenant context; bảng credential không lọc theo tenant.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private void SeedCredential(string tenantId, string clientId, string clientSecretPlain)
    {
        using var db = MoDb();
        db.TenantHnCCredentials.Add(new TenantHnCCredential
        {
            TenantId = tenantId,
            BaseUrl = "https://hnc.example.vn",
            ClientId = clientId,
            // Protector giả là pass-through nên "đã mã hoá" == chuỗi gốc.
            ClientSecretEncrypted = clientSecretPlain,
            HmacSecretEncrypted = "hmac-" + tenantId
        });
        db.SaveChanges();
    }

    private TenantTokenManager TaoManager(AppDbContext db, IHanoiCheckTokenClient client,
                                          ISecretProtector? protector = null) =>
        new(db, protector ?? new PassThroughProtector(), client, _clock,
            NullLogger<TenantTokenManager>.Instance);

    // --- Kịch bản ---

    [Fact]
    public async Task Chua_Cau_Hinh_Thi_Bao_ChuaCauHinh_Khong_Goi_HnC()
    {
        var client = new FakeTokenClient();
        using var db = MoDb();
        var kq = await TaoManager(db, client).LayAccessTokenAsync(CoSoA);

        Assert.Equal(TokenTrangThai.ChuaCauHinh, kq.TrangThai);
        Assert.Empty(client.LanGoi); // tuyệt đối không gọi HnC khi chưa cấu hình.
    }

    [Fact]
    public async Task Lan_Dau_Lay_Token_Bang_ClientCredentials_Va_Luu_Lai()
    {
        SeedCredential(CoSoA, "client-a", "secret-a");
        var client = new FakeTokenClient();
        client.KetQuaLayToken = TokenFetchResult.Ok(new HanoiCheckTokenResponse
        {
            AccessToken = "access-1", RefreshToken = "refresh-1", ExpiresIn = 3600
        });

        using (var db = MoDb())
        {
            var kq = await TaoManager(db, client).LayAccessTokenAsync(CoSoA);
            Assert.Equal(TokenTrangThai.CoToken, kq.TrangThai);
            Assert.Equal("access-1", kq.AccessToken);
        }

        // Token được lưu để lần sau dùng lại.
        using (var db = MoDb())
        {
            var luu = await db.TenantOAuthTokens.SingleAsync(t => t.TenantId == CoSoA);
            Assert.Equal("access-1", luu.AccessToken);
            Assert.Equal(_clock.Now.AddSeconds(3600), luu.AccessTokenExpiresAtUtc);
        }
    }

    [Fact]
    public async Task Token_Con_Han_Thi_Dung_Lai_Khong_Goi_HnC()
    {
        SeedCredential(CoSoA, "client-a", "secret-a");
        using (var db = MoDb())
        {
            db.TenantOAuthTokens.Add(new TenantOAuthToken
            {
                TenantId = CoSoA,
                AccessToken = "access-con-han",
                RefreshToken = "refresh-1",
                AccessTokenExpiresAtUtc = _clock.Now.AddMinutes(30),
                RefreshTokenExpiresAtUtc = _clock.Now.AddDays(10)
            });
            db.SaveChanges();
        }

        var client = new FakeTokenClient();
        using var db2 = MoDb();
        var kq = await TaoManager(db2, client).LayAccessTokenAsync(CoSoA);

        Assert.Equal("access-con-han", kq.AccessToken);
        Assert.Empty(client.LanGoi);
    }

    [Fact]
    public async Task Token_Het_Han_Con_Refresh_Thi_Refresh()
    {
        SeedCredential(CoSoA, "client-a", "secret-a");
        using (var db = MoDb())
        {
            db.TenantOAuthTokens.Add(new TenantOAuthToken
            {
                TenantId = CoSoA,
                AccessToken = "access-cu",
                RefreshToken = "refresh-con-han",
                AccessTokenExpiresAtUtc = _clock.Now.AddSeconds(-10), // đã hết hạn
                RefreshTokenExpiresAtUtc = _clock.Now.AddDays(10)     // refresh còn hạn
            });
            db.SaveChanges();
        }

        var client = new FakeTokenClient();
        client.KetQuaRefresh = TokenFetchResult.Ok(new HanoiCheckTokenResponse
        {
            AccessToken = "access-moi", RefreshToken = "refresh-moi", ExpiresIn = 3600
        });

        using var db2 = MoDb();
        var kq = await TaoManager(db2, client).LayAccessTokenAsync(CoSoA);

        Assert.Equal("access-moi", kq.AccessToken);
        Assert.Contains(client.LanGoi, g => g.Loai == "refresh" && g.RefreshToken == "refresh-con-han");
        Assert.DoesNotContain(client.LanGoi, g => g.Loai == "token"); // không cần client_credentials.
    }

    [Fact]
    public async Task Refresh_That_Bai_Thi_Lay_Token_Moi_Han()
    {
        SeedCredential(CoSoA, "client-a", "secret-a");
        using (var db = MoDb())
        {
            db.TenantOAuthTokens.Add(new TenantOAuthToken
            {
                TenantId = CoSoA,
                AccessToken = "access-cu",
                RefreshToken = "refresh-hong",
                AccessTokenExpiresAtUtc = _clock.Now.AddSeconds(-10),
                RefreshTokenExpiresAtUtc = _clock.Now.AddDays(10)
            });
            db.SaveChanges();
        }

        var client = new FakeTokenClient();
        client.KetQuaRefresh = TokenFetchResult.Loi("refresh_token không dùng được", 401);
        client.KetQuaLayToken = TokenFetchResult.Ok(new HanoiCheckTokenResponse
        {
            AccessToken = "access-lam-lai", RefreshToken = "refresh-lam-lai", ExpiresIn = 3600
        });

        using var db2 = MoDb();
        var kq = await TaoManager(db2, client).LayAccessTokenAsync(CoSoA);

        Assert.Equal("access-lam-lai", kq.AccessToken);
        Assert.Contains(client.LanGoi, g => g.Loai == "refresh");
        Assert.Contains(client.LanGoi, g => g.Loai == "token");
    }

    [Fact]
    public async Task Lay_Token_That_Bai_Thi_Bao_Loi_Kem_HttpStatus()
    {
        SeedCredential(CoSoA, "client-a", "secret-sai");
        var client = new FakeTokenClient();
        client.KetQuaLayToken = TokenFetchResult.Loi("client_secret không đúng", 401);

        using var db = MoDb();
        var kq = await TaoManager(db, client).LayAccessTokenAsync(CoSoA);

        Assert.Equal(TokenTrangThai.Loi, kq.TrangThai);
        Assert.Equal(401, kq.HttpStatusCode);
    }

    [Fact]
    public async Task Khong_Giai_Ma_Duoc_Secret_Thi_Bao_Loi()
    {
        SeedCredential(CoSoA, "client-a", "secret-a");
        var client = new FakeTokenClient();

        using var db = MoDb();
        var manager = TaoManager(db, client, new AlwaysFailProtector());
        var kq = await manager.LayAccessTokenAsync(CoSoA);

        Assert.Equal(TokenTrangThai.Loi, kq.TrangThai);
        Assert.Empty(client.LanGoi); // không thể ký/gọi khi secret hỏng.
    }

    [Fact]
    public async Task Moi_Co_So_Dung_Dung_Secret_Cua_Chinh_Minh()
    {
        // Hàng rào chống dùng nhầm credential giữa các cơ sở.
        SeedCredential(CoSoA, "client-a", "secret-A");
        SeedCredential(CoSoB, "client-b", "secret-B");

        var client = new FakeTokenClient();
        client.KetQuaLayToken = TokenFetchResult.Ok(new HanoiCheckTokenResponse
        {
            AccessToken = "x", RefreshToken = "y", ExpiresIn = 3600
        });

        using (var db = MoDb()) await TaoManager(db, client).LayAccessTokenAsync(CoSoA);
        using (var db = MoDb()) await TaoManager(db, client).LayAccessTokenAsync(CoSoB);

        var goiA = Assert.Single(client.LanGoi, g => g.ClientId == "client-a");
        var goiB = Assert.Single(client.LanGoi, g => g.ClientId == "client-b");
        Assert.Equal("secret-A", goiA.ClientSecret);
        Assert.Equal("secret-B", goiB.ClientSecret);
    }

    // --- Test doubles ---

    private sealed class FakeTimeProvider(DateTime now) : TimeProvider
    {
        public DateTime Now { get; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class PassThroughProtector : ISecretProtector
    {
        public string Protect(string plainText) => plainText;
        public string? TryUnprotect(string protectedText) => protectedText;
    }

    private sealed class AlwaysFailProtector : ISecretProtector
    {
        public string Protect(string plainText) => plainText;
        public string? TryUnprotect(string protectedText) => null;
    }

    private sealed record GhiNhanGoi(string Loai, string BaseUrl, string ClientId, string ClientSecret, string? RefreshToken);

    private sealed class FakeTokenClient : IHanoiCheckTokenClient
    {
        public List<GhiNhanGoi> LanGoi { get; } = new();
        public TokenFetchResult KetQuaLayToken { get; set; } =
            TokenFetchResult.Ok(new HanoiCheckTokenResponse { AccessToken = "default", ExpiresIn = 3600 });
        public TokenFetchResult KetQuaRefresh { get; set; } =
            TokenFetchResult.Ok(new HanoiCheckTokenResponse { AccessToken = "default", ExpiresIn = 3600 });

        public Task<TokenFetchResult> LayTokenAsync(string baseUrl, string clientId, string clientSecret, CancellationToken ct = default)
        {
            LanGoi.Add(new GhiNhanGoi("token", baseUrl, clientId, clientSecret, null));
            return Task.FromResult(KetQuaLayToken);
        }

        public Task<TokenFetchResult> LamMoiTokenAsync(string baseUrl, string clientId, string clientSecret, string refreshToken, CancellationToken ct = default)
        {
            LanGoi.Add(new GhiNhanGoi("refresh", baseUrl, clientId, clientSecret, refreshToken));
            return Task.FromResult(KetQuaRefresh);
        }

        public Task<ConnectionTestResult> KiemTraKetNoiAsync(string baseUrl, string clientId, string clientSecret, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
