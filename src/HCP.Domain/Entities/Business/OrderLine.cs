using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một dòng đặt hàng - phần tử của chi_tiet. Đơn food dùng <see cref="MaLoaiSp"/>,
/// đơn dish dùng <see cref="MaMonAn"/>.
/// </summary>
public class OrderLine : TenantEntity
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>ma_loai_sp - mã danh mục thực phẩm (đơn food).</summary>
    public string? MaLoaiSp { get; set; }

    /// <summary>ma_mon_an - mã món ăn (đơn dish).</summary>
    public string? MaMonAn { get; set; }

    /// <summary>so_luong - số lượng đặt, ≥ 0.</summary>
    public decimal SoLuong { get; set; }

    /// <summary>path_file - ảnh thực tế của dòng hàng (nếu có).</summary>
    public string? PathFile { get; set; }
}
