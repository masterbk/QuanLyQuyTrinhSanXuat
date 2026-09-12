using HCP.Domain.Constants;
using HCP.Infrastructure.Identity;
using HCP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace HCP.Tests;

/// <summary>
/// Cấp token cho ứng dụng di động: token phải mang claim cơ sở (nếu không, mọi truy vấn phía API
/// sẽ không xác định được cơ sở), refresh token xoay vòng một lần, và sai mật khẩu phải bị chặn.
/// </summary>
public class MobileTokenServiceTests
{
    private const string CoSo = "coso-a";
    private const string MatKhau = "MatKhauThu@2026";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private static readonly ApplicationUser NguoiDung = new()
    {
        Id = "u1", UserName = "tho@example.vn", Email = "tho@example.vn",
        HoTen = "Thợ bánh", TenantId = "tenant-id-a"
    };

    private MobileTokenService Svc(AppDbContext db, string? key = null, TimeProvider? clock = null) =>
        new(new FakeUserManager(), new FakeClaimsFactory(), db, CauHinh(key), clock ?? TimeProvider.System);

    private static IConfiguration CauHinh(string? key) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = key ?? new string('k', 40),
            ["Jwt:AccessTokenPhut"] = "120",
            ["Jwt:RefreshTokenNgay"] = "30",
        }).Build();

    [Fact]
    public async Task Dang_Nhap_Dung_Thi_Cap_Token_Mang_Claim_Co_So()
    {
        using var db = MoDb();
        var kq = await Svc(db).DangNhapAsync(NguoiDung.Email!, MatKhau, "Pixel 8");

        Assert.True(kq.ThanhCong, kq.ThongBao);
        var phien = kq.Phien!;
        Assert.NotEmpty(phien.AccessToken);
        Assert.NotEmpty(phien.RefreshToken);
        Assert.True(phien.AccessTokenHetHanUtc > DateTime.UtcNow.AddMinutes(100));
        Assert.Contains(AppRoles.TenantStaff, phien.VaiTro);

        // Claim cơ sở nằm trong token - đây là mắt xích để API lọc đúng dữ liệu của cơ sở.
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(phien.AccessToken);
        Assert.Equal("coso-a-identifier", jwt.Claims.First(c => c.Type == AppClaimTypes.TenantIdentifier).Value);
        Assert.Equal("tenant-id-a", jwt.Claims.First(c => c.Type == AppClaimTypes.TenantId).Value);

        // Refresh token chỉ lưu dạng băm, không lưu bản gốc.
        var luu = await db.MobileRefreshTokens.SingleAsync();
        Assert.NotEqual(phien.RefreshToken, luu.TokenHash);
        Assert.Equal("Pixel 8", luu.ThietBi);
    }

    [Fact]
    public async Task Sai_Mat_Khau_Hoac_Email_La_Deu_Bi_Tu_Choi_Cung_Mot_Thong_Bao()
    {
        using var db = MoDb();
        var svc = Svc(db);

        var saiMk = await svc.DangNhapAsync(NguoiDung.Email!, "sai-be-bet", null);
        var saiEmail = await svc.DangNhapAsync("khong-ton-tai@example.vn", MatKhau, null);

        Assert.False(saiMk.ThanhCong);
        Assert.False(saiEmail.ThanhCong);
        Assert.Equal(saiMk.ThongBao, saiEmail.ThongBao);   // không tiết lộ email nào có thật
        Assert.Empty(await db.MobileRefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Lam_Moi_Thi_Xoay_Vong_Token_Cu_Khong_Dung_Lai_Duoc()
    {
        using var db = MoDb();
        var svc = Svc(db);
        var dau = (await svc.DangNhapAsync(NguoiDung.Email!, MatKhau, null)).Phien!;

        var moi = await svc.LamMoiAsync(dau.RefreshToken, null);
        Assert.True(moi.ThanhCong, moi.ThongBao);
        Assert.NotEqual(dau.RefreshToken, moi.Phien!.RefreshToken);

        // Token cũ đã bị thu hồi -> dùng lại phải hỏng.
        var dungLai = await svc.LamMoiAsync(dau.RefreshToken, null);
        Assert.False(dungLai.ThanhCong);
    }

    [Fact]
    public async Task Dang_Xuat_Thi_Refresh_Token_Het_Hieu_Luc()
    {
        using var db = MoDb();
        var svc = Svc(db);
        var phien = (await svc.DangNhapAsync(NguoiDung.Email!, MatKhau, null)).Phien!;

        await svc.DangXuatAsync(phien.RefreshToken);

        Assert.False((await svc.LamMoiAsync(phien.RefreshToken, null)).ThanhCong);
        Assert.NotNull((await db.MobileRefreshTokens.SingleAsync()).RevokedAtUtc);
    }

    [Fact]
    public async Task Refresh_Token_Qua_Han_Thi_Khong_Lam_Moi_Duoc()
    {
        using var db = MoDb();
        var gio = new FakeClock(new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc));
        var svc = Svc(db, clock: gio);
        var phien = (await svc.DangNhapAsync(NguoiDung.Email!, MatKhau, null)).Phien!;

        gio.Now = gio.Now.AddDays(31);   // quá hạn 30 ngày
        Assert.False((await svc.LamMoiAsync(phien.RefreshToken, null)).ThanhCong);
    }

    [Fact]
    public void Khoa_Ky_Qua_Ngan_Thi_Bao_Loi_Ngay_Khi_Khoi_Tao()
    {
        using var db = MoDb();
        var loi = Assert.Throws<InvalidOperationException>(() => Svc(db, key: "ngan-qua"));
        Assert.Contains("Jwt:Key", loi.Message);
    }

    // ---------- Giả lập tối thiểu cho Identity ----------

    private sealed class FakeClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    private sealed class FakeUserManager() : UserManager<ApplicationUser>(
        new FakeUserStore(), null!, new PasswordHasher<ApplicationUser>(), null!, null!, null!, null!, null!, null!)
    {
        public override Task<ApplicationUser?> FindByEmailAsync(string email) =>
            Task.FromResult(string.Equals(email, NguoiDung.Email, StringComparison.OrdinalIgnoreCase)
                ? NguoiDung : null);

        public override Task<bool> CheckPasswordAsync(ApplicationUser user, string password) =>
            Task.FromResult(password == MatKhau);

        public override Task<bool> IsLockedOutAsync(ApplicationUser user) => Task.FromResult(false);
        public override Task<IdentityResult> AccessFailedAsync(ApplicationUser user) => Task.FromResult(IdentityResult.Success);
        public override Task<IdentityResult> ResetAccessFailedCountAsync(ApplicationUser user) => Task.FromResult(IdentityResult.Success);
        public override Task<IList<string>> GetRolesAsync(ApplicationUser user) =>
            Task.FromResult<IList<string>>(new List<string> { AppRoles.TenantStaff });
        public override Task<ApplicationUser?> FindByIdAsync(string userId) =>
            Task.FromResult(userId == NguoiDung.Id ? NguoiDung : null);
    }

    private sealed class FakeUserStore : IUserStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser u, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser u, CancellationToken ct) => throw new NotSupportedException();
        public void Dispose() { }
        public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => Task.FromResult<ApplicationUser?>(null);
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult<string?>(u.UserName);
        public Task<string> GetUserIdAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult<string?>(u.UserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser u, string? n, CancellationToken ct) => Task.CompletedTask;
        public Task SetUserNameAsync(ApplicationUser u, string? n, CancellationToken ct) => Task.CompletedTask;
        public Task<IdentityResult> UpdateAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
    }

    /// <summary>Giả lập AppUserClaimsPrincipalFactory: phát đúng bộ claim mà bản web phát ra.</summary>
    private sealed class FakeClaimsFactory : IUserClaimsPrincipalFactory<ApplicationUser>
    {
        public Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
        {
            var id = new ClaimsIdentity("jwt");
            id.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
            id.AddClaim(new Claim(ClaimTypes.Email, user.Email!));
            id.AddClaim(new Claim(ClaimTypes.Role, AppRoles.TenantStaff));
            id.AddClaim(new Claim(AppClaimTypes.TenantId, user.TenantId!));
            id.AddClaim(new Claim(AppClaimTypes.TenantIdentifier, "coso-a-identifier"));
            id.AddClaim(new Claim("hoTen", user.HoTen ?? ""));
            return Task.FromResult(new ClaimsPrincipal(id));
        }
    }
}
