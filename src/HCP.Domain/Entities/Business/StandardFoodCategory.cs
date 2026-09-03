namespace HCP.Domain.Entities.Business;

/// <summary>
/// Danh mục thực phẩm chuẩn lấy từ GET /supplier/standard-foods.
///
/// Dữ liệu này do HanoiCheck ban hành, giống nhau với mọi cơ sở nên KHÔNG gắn TenantId
/// và không bị lọc theo tenant. Được đồng bộ định kỳ về để làm dropdown chọn mã nhóm
/// thực phẩm, tránh cơ sở gõ tay sai mã rồi bị HnC trả 422.
/// </summary>
public class StandardFoodCategory
{
    public int Id { get; set; }

    /// <summary>code - vd "THIT", "RAU_CU".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>name - vd "Thịt và sản phẩm từ thịt".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>measure_name - đơn vị đo, vd "Kilogram".</summary>
    public string? MeasureName { get; set; }

    public DateTime CapNhatLucUtc { get; set; } = DateTime.UtcNow;
}
