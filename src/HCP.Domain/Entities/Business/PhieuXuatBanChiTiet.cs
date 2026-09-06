using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>Một dòng thành phẩm trong phiếu xuất bán (chi tiết lô đã xuất nằm ở sổ kho KhoGiaoDich).</summary>
public class PhieuXuatBanChiTiet : TenantEntity
{
    public int Id { get; set; }

    public int PhieuXuatBanId { get; set; }
    public PhieuXuatBan? PhieuXuatBan { get; set; }

    public string MaThanhPham { get; set; } = string.Empty;

    public decimal SoLuong { get; set; }
}
