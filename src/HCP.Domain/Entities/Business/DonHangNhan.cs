using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Đơn hàng NHẬN VỀ từ HanoiCheck (do trường học tạo, nhà cung cấp kéo về qua
/// GET /api/supplier/orders). Đây là dữ liệu chỉ đọc phía cơ sở, cập nhật bằng job đồng bộ.
///
/// KHÁC với <see cref="Order"/> (chiều ĐẨY lên HnC): entity này là chiều KÉO xuống.
/// Vì job nền ghi dữ liệu KHÔNG có tenant context, bảng này KHÔNG dùng global filter
/// multi-tenant; TenantId được gán tường minh khi ghi và lọc tường minh khi đọc
/// (giống SyncOutbox / SystemLog).
/// </summary>
public class DonHangNhan : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Cơ sở (tenant) sở hữu đơn - gán tường minh khi đồng bộ.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>code - mã đơn hàng (khoá nghiệp vụ, duy nhất trong một cơ sở).</summary>
    public string MaDonHang { get; set; } = string.Empty;

    /// <summary>school.name - tên trường học đặt đơn (có thể null nếu đơn không gắn trường).</summary>
    public string? TenTruong { get; set; }

    /// <summary>status - trạng thái đơn (CHO_XAC_NHAN, DANG_GIAO, DA_GIAO...).</summary>
    public string? TrangThai { get; set; }

    /// <summary>Ngày giao (order_date) nếu HnC trả về.</summary>
    public DateOnly? NgayGiao { get; set; }

    /// <summary>Người giao hàng (từ chi tiết đơn).</summary>
    public string? MaNguoiGiao { get; set; }

    /// <summary>Địa chỉ/điểm giao (từ chi tiết đơn).</summary>
    public string? DiaChiGiao { get; set; }

    /// <summary>Đã kéo chi tiết đơn (items + phân bổ) hay chưa.</summary>
    public bool DaLayChiTiet { get; set; }

    /// <summary>Thời điểm đồng bộ gần nhất từ HanoiCheck.</summary>
    public DateTime LanDongBoUtc { get; set; }

    /// <summary>Các dòng sản phẩm/món ăn của đơn (từ products[] của dịch vụ danh sách).</summary>
    public List<DonHangNhanDong> Dong { get; set; } = new();
}
