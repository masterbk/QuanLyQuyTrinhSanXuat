using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một dòng xuất kho thực tế phục vụ đơn - phần tử của xuat_kho. CHỈ dùng cho đơn food.
/// </summary>
public class OrderExport : TenantEntity
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>ma_xuat_kho - mã phiếu xuất kho (nếu có).</summary>
    public string? MaXuatKho { get; set; }

    /// <summary>ma_loai_sp - mã danh mục thực phẩm của dòng xuất (nếu có).</summary>
    public string? MaLoaiSp { get; set; }

    /// <summary>ma_san_pham - mã thực phẩm.</summary>
    public string MaSanPham { get; set; } = string.Empty;

    /// <summary>ma_kho - mã kho xuất.</summary>
    public string MaKho { get; set; } = string.Empty;

    /// <summary>ma_lo - mã lô xuất.</summary>
    public string MaLo { get; set; } = string.Empty;

    /// <summary>so_luong - số lượng xuất, ≥ 0.</summary>
    public decimal SoLuong { get; set; }
}
