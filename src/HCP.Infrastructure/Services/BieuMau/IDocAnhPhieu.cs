namespace HCP.Infrastructure.Services.BieuMau;

/// <summary>
/// Đọc bytes ảnh đã lưu (dưới wwwroot/uploads) từ đường dẫn lưu trong phiếu, để nhúng vào PDF.
/// Cài đặt ở tầng Web (biết WebRootPath). Null nếu không đọc được / đường dẫn không hợp lệ.
/// </summary>
public interface IDocAnhPhieu
{
    byte[]? Doc(string? duongDan);
}
