namespace HCP.Domain.Entities.Business;

/// <summary>
/// Danh mục nhóm tuổi lấy từ GET /supplier/student-groups (đặc tả v2.6, Phần V mục 2).
///
/// Do HanoiCheck ban hành, giống nhau với mọi cơ sở nên KHÔNG gắn TenantId. Là nguồn chọn
/// nhom_tuoi_id của Món ăn (và Thực đơn sau này).
/// </summary>
public class NhomTuoiHnC
{
    /// <summary>id của HanoiCheck - chính là giá trị gửi ở nhom_tuoi_id (không tự tăng).</summary>
    public int Id { get; set; }

    /// <summary>name - vd "Mầm non", "Tiểu học".</summary>
    public string Ten { get; set; } = string.Empty;

    public DateTime CapNhatLucUtc { get; set; } = DateTime.UtcNow;
}
