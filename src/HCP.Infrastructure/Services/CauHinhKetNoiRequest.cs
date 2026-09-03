using System.ComponentModel.DataAnnotations;

namespace HCP.Infrastructure.Services;

/// <summary>
/// Thông tin kết nối HanoiCheck do chính cơ sở nhập vào.
/// Cơ sở tự xin bộ credential này từ HanoiCheck qua quy trình đăng ký với cơ quan quản lý.
/// </summary>
public class CauHinhKetNoiRequest
{
    [Required(ErrorMessage = "Vui lòng nhập Base URL")]
    [StringLength(500)]
    [Url(ErrorMessage = "Base URL không hợp lệ")]
    public string BaseUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập client_id")]
    [StringLength(255)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Để trống khi sửa nếu không muốn đổi secret đã lưu.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>KHÁC client_secret - dùng để ký X-Signature. Để trống nếu không đổi.</summary>
    public string? HmacSecret { get; set; }
}
