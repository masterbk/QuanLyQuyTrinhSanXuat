namespace HCP.Domain.Entities.Infrastructure;

/// <summary>
/// Refresh token của ứng dụng di động: cho phép app xin access token mới mà không bắt người
/// dùng đăng nhập lại mỗi lần token hết hạn.
///
/// Chỉ lưu HASH của token (SHA-256), không lưu bản gốc - lộ cơ sở dữ liệu cũng không mạo danh
/// được. Mỗi lần làm mới thì thu hồi token cũ và phát token mới (xoay vòng), nhờ vậy nếu một
/// token bị dùng lại sau khi đã xoay thì biết ngay là bất thường.
///
/// KHÔNG kế thừa TenantEntity: bảng hạ tầng, truy cập lúc chưa có tenant context (đang đăng nhập).
/// </summary>
public class MobileRefreshToken
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>SHA-256 (Base64) của token gửi cho app.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Mô tả thiết bị do app gửi lên, để người dùng biết phiên nào của máy nào.</summary>
    public string? ThietBi { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Thời điểm bị thu hồi (đăng xuất, hoặc đã xoay sang token mới).</summary>
    public DateTime? RevokedAtUtc { get; set; }

    public bool ConHieuLuc(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
}
