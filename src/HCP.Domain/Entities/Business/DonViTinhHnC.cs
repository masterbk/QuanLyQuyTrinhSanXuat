namespace HCP.Domain.Entities.Business;

/// <summary>
/// Danh mục đơn vị tính lấy từ GET /supplier/measures (đặc tả v2.6, Phần V mục 4).
///
/// Do HanoiCheck ban hành, giống nhau với mọi cơ sở nên KHÔNG gắn TenantId. Dùng làm dropdown
/// chọn food_config.measure_id khi đồng bộ thực phẩm.
/// </summary>
public class DonViTinhHnC
{
    /// <summary>id của HanoiCheck - chính là giá trị gửi ở food_config.measure_id (không tự tăng).</summary>
    public int Id { get; set; }

    /// <summary>name - vd "Kg", "Lít".</summary>
    public string Ten { get; set; } = string.Empty;

    public DateTime CapNhatLucUtc { get; set; } = DateTime.UtcNow;
}
