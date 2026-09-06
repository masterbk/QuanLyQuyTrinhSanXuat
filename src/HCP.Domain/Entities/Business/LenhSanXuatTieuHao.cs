using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>Nguyên liệu (theo lô) đã tiêu hao cho một lệnh sản xuất - phục vụ truy xuất nguồn gốc.</summary>
public class LenhSanXuatTieuHao : TenantEntity
{
    public int Id { get; set; }

    public int LenhSanXuatId { get; set; }
    public LenhSanXuat? LenhSanXuat { get; set; }

    public string MaNguyenLieu { get; set; } = string.Empty;
    public string MaLo { get; set; } = string.Empty;
    public decimal SoLuong { get; set; }
}
