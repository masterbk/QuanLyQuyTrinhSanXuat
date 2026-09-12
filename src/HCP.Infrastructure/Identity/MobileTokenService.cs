using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HCP.Infrastructure.Identity;

/// <summary>Cấu hình JWT cho ứng dụng di động, đọc từ khoá "Jwt" trong appsettings.</summary>
public sealed class MobileJwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "HanoiCheckPlatform";
    public string Audience { get; set; } = "HanoiCheckPlatform.Mobile";
    /// <summary>Hạn access token (phút). Ngắn để giảm thiệt hại nếu lộ; app tự làm mới.</summary>
    public int AccessTokenPhut { get; set; } = 120;
    /// <summary>Hạn refresh token (ngày) - quyết định bao lâu người dùng phải đăng nhập lại.</summary>
    public int RefreshTokenNgay { get; set; } = 30;
}

public sealed record PhienDangNhap(
    string AccessToken,
    DateTime AccessTokenHetHanUtc,
    string RefreshToken,
    DateTime RefreshTokenHetHanUtc,
    ApplicationUser NguoiDung,
    IReadOnlyList<string> VaiTro);

public sealed record KetQuaDangNhap(bool ThanhCong, string? ThongBao, PhienDangNhap? Phien)
{
    public static KetQuaDangNhap Ok(PhienDangNhap p) => new(true, null, p);
    public static KetQuaDangNhap Loi(string thongBao) => new(false, thongBao, null);
}

/// <summary>
/// Cấp và làm mới token cho ứng dụng di động. Claim trong token được dựng bằng ĐÚNG
/// <see cref="AppUserClaimsPrincipalFactory"/> của web, nên token mang sẵn claim cơ sở
/// (tenantIdentifier) - hàng rào cách ly dữ liệu giữa các cơ sở hoạt động y hệt bản web.
/// </summary>
public interface IMobileTokenService
{
    Task<KetQuaDangNhap> DangNhapAsync(string email, string matKhau, string? thietBi, CancellationToken ct = default);
    Task<KetQuaDangNhap> LamMoiAsync(string refreshToken, string? thietBi, CancellationToken ct = default);
    Task DangXuatAsync(string refreshToken, CancellationToken ct = default);
}

/// <inheritdoc cref="IMobileTokenService"/>
public sealed class MobileTokenService : IMobileTokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _claimsFactory;
    private readonly AppDbContext _db;
    private readonly MobileJwtOptions _opt;
    private readonly TimeProvider _clock;

    public MobileTokenService(UserManager<ApplicationUser> userManager,
                              IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
                              AppDbContext db,
                              IConfiguration cauHinh,
                              TimeProvider clock)
    {
        _userManager = userManager;
        _claimsFactory = claimsFactory;
        _db = db;
        _clock = clock;
        _opt = cauHinh.GetSection("Jwt").Get<MobileJwtOptions>() ?? new MobileJwtOptions();
        if (string.IsNullOrWhiteSpace(_opt.Key) || Encoding.UTF8.GetByteCount(_opt.Key) < 32)
        {
            // Khoá ngắn ký được nhưng dễ dò; chặn ngay lúc chạy thay vì để lộ về sau.
            throw new InvalidOperationException(
                "Thiếu hoặc quá ngắn khoá cấu hình \"Jwt:Key\" (cần tối thiểu 32 ký tự) để ký token ứng dụng di động.");
        }
    }

    public async Task<KetQuaDangNhap> DangNhapAsync(string email, string matKhau, string? thietBi,
                                                    CancellationToken ct = default)
    {
        email = email?.Trim() ?? "";
        var user = string.IsNullOrEmpty(email) ? null : await _userManager.FindByEmailAsync(email);
        // Thông báo chung cho cả sai email lẫn sai mật khẩu - không tiết lộ email nào có thật.
        const string SAI = "Email hoặc mật khẩu không đúng.";
        if (user is null) return KetQuaDangNhap.Loi(SAI);

        if (await _userManager.IsLockedOutAsync(user))
            return KetQuaDangNhap.Loi("Tài khoản đang bị khoá tạm thời do nhập sai nhiều lần. Vui lòng thử lại sau.");

        if (!await _userManager.CheckPasswordAsync(user, matKhau ?? ""))
        {
            await _userManager.AccessFailedAsync(user);
            return KetQuaDangNhap.Loi(SAI);
        }
        await _userManager.ResetAccessFailedCountAsync(user);

        if (!user.EmailConfirmed && _userManager.Options.SignIn.RequireConfirmedAccount)
            return KetQuaDangNhap.Loi("Tài khoản chưa được xác nhận.");

        var vaiTro = await _userManager.GetRolesAsync(user);
        return KetQuaDangNhap.Ok(await TaoPhienAsync(user, vaiTro, thietBi, ct));
    }

    public async Task<KetQuaDangNhap> LamMoiAsync(string refreshToken, string? thietBi, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken ?? "");
        var ban = await _db.MobileRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        if (ban is null || !ban.ConHieuLuc(now))
            return KetQuaDangNhap.Loi("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");

        var user = await _userManager.FindByIdAsync(ban.UserId);
        if (user is null) return KetQuaDangNhap.Loi("Tài khoản không còn tồn tại.");

        ban.RevokedAtUtc = now;          // xoay vòng: token cũ dùng một lần rồi bỏ
        var vaiTro = await _userManager.GetRolesAsync(user);
        return KetQuaDangNhap.Ok(await TaoPhienAsync(user, vaiTro, thietBi ?? ban.ThietBi, ct));
    }

    public async Task DangXuatAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken ?? "");
        var ban = await _db.MobileRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (ban is null || ban.RevokedAtUtc is not null) return;
        ban.RevokedAtUtc = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PhienDangNhap> TaoPhienAsync(ApplicationUser user, IList<string> vaiTro,
                                                    string? thietBi, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        // Dùng lại factory của web -> có sẵn tenantIdentifier, tenantId, vai trò, hoTen.
        var principal = await _claimsFactory.CreateAsync(user);
        var claims = principal.Claims
            .Where(c => c.Type != JwtRegisteredClaimNames.Aud && c.Type != JwtRegisteredClaimNames.Iss)
            .ToList();
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));

        var hetHan = now.AddMinutes(_opt.AccessTokenPhut);
        var khoa = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now,
            expires: hetHan,
            signingCredentials: new SigningCredentials(khoa, SecurityAlgorithms.HmacSha256));
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var refresh = TaoChuoiNgauNhien();
        var refreshHetHan = now.AddDays(_opt.RefreshTokenNgay);
        _db.MobileRefreshTokens.Add(new MobileRefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(refresh),
            ThietBi = string.IsNullOrWhiteSpace(thietBi) ? null : thietBi.Trim(),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshHetHan
        });
        await _db.SaveChangesAsync(ct);

        return new PhienDangNhap(accessToken, hetHan, refresh, refreshHetHan, user, vaiTro.ToList());
    }

    private static string TaoChuoiNgauNhien() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private static string Hash(string giaTri) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(giaTri)));
}
