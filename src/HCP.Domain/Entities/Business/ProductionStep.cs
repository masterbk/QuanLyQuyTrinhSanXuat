using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Khâu sản xuất (vd GIET_MO, SO_CHE). Là danh mục dùng lại cho quy trình sản xuất,
/// lô sản xuất và món ăn.
/// Đồng bộ qua POST /supplier/steps/merge (đồng bộ, trả 200).
/// </summary>
public class ProductionStep : TenantEntity, ICoDongBoHnC
{
    public int Id { get; set; }

    /// <summary>Có gửi bản ghi này sang HanoiCheck không (khi cơ sở bật đồng bộ). Mặc định có.</summary>
    public bool DongBoHnC { get; set; } = true;

    /// <summary>ma_khau - khoá nghiệp vụ, vd "GIET_MO".</summary>
    public string MaKhau { get; set; } = string.Empty;

    /// <summary>ten_khau.</summary>
    public string TenKhau { get; set; } = string.Empty;

    public string? GhiChu { get; set; }
}
