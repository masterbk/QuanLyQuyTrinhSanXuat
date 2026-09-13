using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>Một dòng hàng của đơn bán: thành phẩm, số lượng, đơn giá và các lô đã xuất cho dòng này.</summary>
public class DonHangBanDong : TenantEntity
{
    public int Id { get; set; }

    public int DonHangBanId { get; set; }
    public DonHangBan? DonHangBan { get; set; }

    public string MaThanhPham { get; set; } = string.Empty;

    public decimal SoLuong { get; set; }

    /// <summary>Đơn giá bán (đồng/đơn vị tính của thành phẩm).</summary>
    public decimal DonGia { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>Phân bổ lô khi xuất kho - để truy xuất lô nào đã giao cho khách nào.</summary>
    public List<DonHangBanXuatLo> XuatLo { get; set; } = new();

    public decimal ThanhTien => SoLuong * DonGia;
}
