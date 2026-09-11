using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một dòng phân bổ cung ứng (allocations[]) của một dòng hàng trong đơn nhận về:
/// thực phẩm NCC nào, lấy từ lô/kho nào, số lượng bao nhiêu.
/// </summary>
public class DonHangNhanPhanBo : AuditableEntity
{
    public int Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public int DonHangNhanDongId { get; set; }
    public DonHangNhanDong? DonHangNhanDong { get; set; }

    /// <summary>supplier_food_code - mã thực phẩm của nhà cung cấp được phân bổ.</summary>
    public string? MaThucPhamNcc { get; set; }

    /// <summary>stock_out_code - mã phiếu xuất kho trên HanoiCheck.</summary>
    public string? MaPhieuXuat { get; set; }

    /// <summary>batch.code - lô xuất.</summary>
    public string? MaLo { get; set; }

    /// <summary>batch.name</summary>
    public string? TenLo { get; set; }

    /// <summary>warehouse.code - kho xuất.</summary>
    public string? MaKho { get; set; }

    /// <summary>warehouse.name</summary>
    public string? TenKho { get; set; }

    /// <summary>amount - số lượng xuất.</summary>
    public decimal? SoLuong { get; set; }
}
