namespace HCP.Infrastructure.Security;

/// <summary>
/// Mã hoá/giải mã dữ liệu nhạy cảm trước khi lưu database.
///
/// Dùng cho client_secret và hmac_secret của từng cơ sở: tài liệu HnC yêu cầu
/// "bảo mật tuyệt đối mã xác thực được cấp". Nếu database bị lộ, secret vẫn ở dạng mã hoá.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plainText);

    /// <summary>Trả về null nếu chuỗi không giải mã được (khoá đổi, dữ liệu hỏng).</summary>
    string? TryUnprotect(string protectedText);
}
