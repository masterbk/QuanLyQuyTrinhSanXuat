using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>Một dòng sản phẩm/món ăn trong đơn nhận về (products[] của dịch vụ danh sách đơn).</summary>
public class DonHangNhanDong : AuditableEntity
{
    public int Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public int DonHangNhanId { get; set; }
    public DonHangNhan? DonHangNhan { get; set; }

    /// <summary>code - mã thực phẩm hoặc mã món ăn.</summary>
    public string MaSanPham { get; set; } = string.Empty;

    /// <summary>name - tên sản phẩm/món ăn.</summary>
    public string? TenSanPham { get; set; }

    // --- Bổ sung từ chi tiết đơn (GET /orders/{code}) ---

    /// <summary>Số lượng đặt (từ chi tiết đơn).</summary>
    public decimal? SoLuong { get; set; }

    /// <summary>trace_code - mã truy vết dòng hàng (khi 1 sản phẩm xuất hiện nhiều dòng).</summary>
    public string? MaTruyVet { get; set; }

    /// <summary>menu_code - mã thực đơn nguồn của dòng (nếu có).</summary>
    public string? MaThucDon { get; set; }

    /// <summary>Phân bổ cung ứng của dòng: lô/kho/số lượng theo từng supplier_food_code.</summary>
    public List<DonHangNhanPhanBo> PhanBo { get; set; } = new();
}
