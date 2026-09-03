using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một mã nhóm thực phẩm mà nhà cung ứng đầu vào cung cấp (vd "THIT", "TRUNG").
/// Mã lấy từ danh mục thực phẩm chuẩn của HanoiCheck.
/// </summary>
public class SubSupplierFoodGroup : TenantEntity
{
    public int Id { get; set; }

    public int SubSupplierId { get; set; }
    public SubSupplier? SubSupplier { get; set; }

    /// <summary>Mã nhóm thực phẩm, khớp trường code của GET /supplier/standard-foods.</summary>
    public string MaNhom { get; set; } = string.Empty;
}
