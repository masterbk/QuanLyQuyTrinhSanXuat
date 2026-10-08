using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Identity;

/// <summary>Người dùng tự đổi mật khẩu của mình (web + app).</summary>
public interface IDoiMatKhauService
{
    /// <summary>
    /// Đổi mật khẩu sau khi kiểm tra mật khẩu hiện tại. Thành công thì mọi phiên khác bị đăng xuất: phiên web khác
    /// hết hiệu lực (đổi security stamp, tối đa ~1 phút), mọi phiên app bị thu hồi - nơi gọi tự cấp phiên mới cho
    /// thiết bị đang dùng (web: RefreshSignIn, app: đăng nhập lại bằng mật khẩu mới).
    /// </summary>
    Task<KetQuaThaoTac> DoiMatKhauAsync(string userId, string matKhauHienTai, string matKhauMoi,
                                        CancellationToken ct = default);
}

/// <inheritdoc cref="IDoiMatKhauService"/>
public sealed class DoiMatKhauService : IDoiMatKhauService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public DoiMatKhauService(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<KetQuaThaoTac> DoiMatKhauAsync(string userId, string matKhauHienTai, string matKhauMoi,
                                                     CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(matKhauHienTai)) return KetQuaThaoTac.Loi("Vui lòng nhập mật khẩu hiện tại.");
        if (string.IsNullOrEmpty(matKhauMoi)) return KetQuaThaoTac.Loi("Vui lòng nhập mật khẩu mới.");
        if (matKhauMoi == matKhauHienTai) return KetQuaThaoTac.Loi("Mật khẩu mới phải khác mật khẩu hiện tại.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.DangHoatDong) return KetQuaThaoTac.Loi("Tài khoản không còn hoạt động.");

        // ChangePasswordAsync tự kiểm mật khẩu cũ + độ mạnh mật khẩu mới, và đổi security stamp.
        var kq = await _userManager.ChangePasswordAsync(user, matKhauHienTai, matKhauMoi);
        if (!kq.Succeeded) return KetQuaThaoTac.Loi(LoiIdentity.MoTa(kq));

        // Thu hồi mọi phiên app (thiết bị khác phải đăng nhập lại bằng mật khẩu mới).
        var now = DateTime.UtcNow;
        var dangMo = await _db.MobileRefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in dangMo) t.RevokedAtUtc = now;
        if (dangMo.Count > 0) await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok("Đã đổi mật khẩu. Các thiết bị khác cần đăng nhập lại bằng mật khẩu mới.");
    }
}
