using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Cơ sở sản xuất/chế biến vật lý của một tenant.
/// Đồng bộ qua POST /supplier/facilities/merge (bất đồng bộ, trả 202).
///
/// Lưu ý phân biệt: "Tenant" là pháp nhân nhà cung cấp (có tài khoản HanoiCheck riêng),
/// còn entity này là địa điểm sản xuất bên trong pháp nhân đó. Một tenant có thể có nhiều cơ sở.
/// </summary>
public class Facility : TenantEntity
{
    public int Id { get; set; }

    /// <summary>ma_co_so - khoá nghiệp vụ. Tối đa 255 ký tự.</summary>
    public string MaCoSo { get; set; } = string.Empty;

    /// <summary>ten_co_so - tối đa 255 ký tự.</summary>
    public string TenCoSo { get; set; } = string.Empty;

    /// <summary>dia_chi - không bắt buộc, tối đa 255 ký tự.</summary>
    public string? DiaChi { get; set; }
}
