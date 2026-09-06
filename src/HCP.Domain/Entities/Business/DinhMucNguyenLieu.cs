using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một dòng định mức (công thức) của thành phẩm: cần bao nhiêu nguyên liệu cho MỘT đơn vị
/// thành phẩm. Dùng cho lệnh sản xuất tự trừ nguyên liệu. Nội bộ, không gửi HanoiCheck.
/// </summary>
public class DinhMucNguyenLieu : TenantEntity
{
    public int Id { get; set; }

    /// <summary>Thành phẩm sở hữu định mức này.</summary>
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Mã nguyên liệu (Product loại NguyenLieu).</summary>
    public string MaNguyenLieu { get; set; } = string.Empty;

    /// <summary>Lượng nguyên liệu cho 1 đơn vị thành phẩm (≥ 0).</summary>
    public decimal SoLuong { get; set; }
}
