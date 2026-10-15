namespace HCP.Domain.Entities.Business;

/// <summary>
/// Danh mục bữa ăn lấy từ GET /supplier/meal-types (đặc tả v2.6, Phần V mục 3).
///
/// Do HanoiCheck ban hành, giống nhau với mọi cơ sở nên KHÔNG gắn TenantId. Là nguồn chọn
/// buoi_an_id của Thực đơn. HnC trả sẵn theo thứ tự bữa trong ngày -> lưu lại vào <see cref="ThuTu"/>.
/// </summary>
public class BuaAnHnC
{
    /// <summary>id của HanoiCheck - chính là giá trị gửi ở buoi_an_id (không tự tăng).</summary>
    public int Id { get; set; }

    /// <summary>name - vd "Bữa sáng", "Bữa trưa".</summary>
    public string Ten { get; set; } = string.Empty;

    /// <summary>Vị trí trong danh sách HnC trả về (thứ tự bữa trong ngày).</summary>
    public int ThuTu { get; set; }

    public DateTime CapNhatLucUtc { get; set; } = DateTime.UtcNow;
}
