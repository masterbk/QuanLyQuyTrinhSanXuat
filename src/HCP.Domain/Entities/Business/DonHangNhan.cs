using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Đơn hàng NHẬN VỀ từ HanoiCheck (do trường học tạo, nhà cung cấp kéo về qua
/// GET /api/supplier/orders). Đây là dữ liệu chỉ đọc phía cơ sở, cập nhật bằng job đồng bộ.
///
/// Bản sao đơn từ HanoiCheck; đơn bán nội bộ của nhà cung cấp là <see cref="DonHangBan"/>.
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

    /// <summary>transporter.code - ma_nhan_su của người giao.</summary>
    public string? MaNguoiGiao { get; set; }

    /// <summary>transporter.name</summary>
    public string? TenNguoiGiao { get; set; }

    /// <summary>transporter.phone</summary>
    public string? SdtNguoiGiao { get; set; }

    /// <summary>transporter.transport_mean (vd "Xe máy").</summary>
    public string? PhuongTienGiao { get; set; }

    /// <summary>transporter.license_plate</summary>
    public string? BienSoXe { get; set; }

    /// <summary>delivery_address</summary>
    public string? DiaChiGiao { get; set; }

    /// <summary>school_point - điểm trường nhận hàng.</summary>
    public string? DiemTruong { get; set; }

    /// <summary>warehouses[] - kho xuất, dạng "K-01 - Kho số 1; ...".</summary>
    public string? KhoXuat { get; set; }

    /// <summary>product_type_label (Thực phẩm / Món ăn).</summary>
    public string? LoaiDon { get; set; }

    /// <summary>note - ghi chú của đơn (vd lý do trường huỷ).</summary>
    public string? GhiChu { get; set; }

    /// <summary>traceability_url - trang truy xuất công khai của đơn trên HanoiCheck.</summary>
    public string? LinkTruyXuat { get; set; }

    /// <summary>created_at - thời điểm trường tạo đơn (giờ Việt Nam, như HnC trả).</summary>
    public DateTime? NgayTaoTrenHnC { get; set; }

    /// <summary>Đã kéo chi tiết đơn (items + phân bổ) hay chưa.</summary>
    public bool DaLayChiTiet { get; set; }

    /// <summary>Thời điểm đồng bộ gần nhất từ HanoiCheck.</summary>
    public DateTime LanDongBoUtc { get; set; }

    /// <summary>Các dòng sản phẩm/món ăn của đơn (từ products[] của dịch vụ danh sách).</summary>
    public List<DonHangNhanDong> Dong { get; set; } = new();
}
