using Microsoft.AspNetCore.Identity;

namespace HCP.Infrastructure.Identity;

/// <summary>Thông báo lỗi Identity bằng tiếng Việt (mặc định Identity trả tiếng Anh).</summary>
public static class LoiIdentity
{
    public static string MoTa(IdentityResult kq) => string.Join(" ", kq.Errors.Select(e => e.Code switch
    {
        "PasswordTooShort" => "Mật khẩu phải có ít nhất 8 ký tự.",
        "PasswordRequiresDigit" => "Mật khẩu phải có ít nhất một chữ số.",
        "PasswordRequiresLower" => "Mật khẩu phải có ít nhất một chữ thường.",
        "PasswordRequiresUpper" => "Mật khẩu phải có ít nhất một chữ hoa.",
        "PasswordRequiresNonAlphanumeric" => "Mật khẩu phải có ít nhất một ký tự đặc biệt (vd @, #, !).",
        "PasswordRequiresUniqueChars" => "Mật khẩu cần nhiều ký tự khác nhau hơn.",
        "PasswordMismatch" => "Mật khẩu hiện tại không đúng.",
        "DuplicateUserName" => "Tên đăng nhập đã được dùng.",
        "DuplicateEmail" => "Email đã được dùng.",
        _ => e.Description
    }));
}
