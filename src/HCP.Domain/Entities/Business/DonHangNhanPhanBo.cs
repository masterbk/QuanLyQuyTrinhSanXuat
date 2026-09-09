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

    /// <summary>ma_lo - lô xuất.</summary>
    public string? MaLo { get; set; }

    /// <summary>ma_kho - kho xuất.</summary>
    public string? MaKho { get; set; }

    public decimal? SoLuong { get; set; }
}
