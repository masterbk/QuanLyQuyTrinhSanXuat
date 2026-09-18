using Microsoft.AspNetCore.Identity;

namespace HCP.Infrastructure.Identity;

/// <summary>
/// Tên đăng nhập: quản trị cơ sở đăng nhập bằng email; tài khoản nhân viên có tên đăng nhập là SỐ ĐIỆN THOẠI
/// (email không bắt buộc). Ô đăng nhập web/app nhận một trong hai.
/// </summary>
public static class TenDangNhap
{
    /// <summary>
    /// Chuẩn hoá số điện thoại thành dãy chữ số (bỏ khoảng trắng, dấu chấm...; "+84"/"84" đầu số đổi thành "0").
    /// Không hợp lệ (ngoài 9-11 chữ số) trả null.
    /// </summary>
    public static string? ChuanHoaSoDienThoai(string? soDienThoai)
    {
        if (string.IsNullOrWhiteSpace(soDienThoai)) return null;

        var so = new string(soDienThoai.Where(char.IsDigit).ToArray());
        if (soDienThoai.TrimStart().StartsWith("+84") || (so.StartsWith("84") && so.Length == 11))
            so = "0" + so[2..];

        return so.Length is >= 9 and <= 11 ? so : null;
    }

    /// <summary>Tìm tài khoản theo nội dung ô đăng nhập: có "@" là email, còn lại là số điện thoại.</summary>
    public static async Task<ApplicationUser?> TimAsync(UserManager<ApplicationUser> userManager, string? nhap)
    {
        var s = nhap?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (s.Contains('@')) return await userManager.FindByEmailAsync(s);

        var sdt = ChuanHoaSoDienThoai(s);
        return sdt is null ? null : await userManager.FindByNameAsync(sdt);
    }
}
