using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Trường học cơ sở được phân công phục vụ trong năm học hiện tại, lấy từ GET /supplier/schools
/// (đặc tả v2.6, Phần V mục 5 - cần ký HMAC). Dữ liệu RIÊNG từng cơ sở nên gắn TenantId.
/// Dùng làm nguồn chọn danh_sach_truong của Thực đơn.
/// </summary>
public class TruongHocHnC : TenantEntity
{
    public int Id { get; set; }

    /// <summary>id trường của HanoiCheck - giá trị gửi ở danh_sach_truong (không phải mã trường).</summary>
    public int TruongId { get; set; }

    /// <summary>name - tên trường.</summary>
    public string Ten { get; set; } = string.Empty;

    public DateTime CapNhatLucUtc { get; set; } = DateTime.UtcNow;
}
