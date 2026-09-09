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
}
