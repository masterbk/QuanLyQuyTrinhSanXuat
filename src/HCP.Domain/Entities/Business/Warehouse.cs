using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Kho của một cơ sở sản xuất.
/// Đồng bộ sang HanoiCheck qua POST /supplier/warehouses/merge (xử lý đồng bộ, trả 200).
///
/// Đây là entity nghiệp vụ mẫu - mọi entity nghiệp vụ khác theo đúng khuôn này:
/// kế thừa TenantEntity + đăng ký .IsMultiTenant() trong AppDbContext.
/// </summary>
public class Warehouse : TenantEntity
{
    public int Id { get; set; }

    /// <summary>ma_kho - khoá nghiệp vụ dùng để upsert phía HnC. Tối đa 255 ký tự.</summary>
    public string MaKho { get; set; } = string.Empty;

    /// <summary>ten_kho - tối đa 255 ký tự.</summary>
    public string TenKho { get; set; } = string.Empty;

    /// <summary>dia_chi - tối đa 255 ký tự.</summary>
    public string DiaChi { get; set; } = string.Empty;

    /// <summary>dien_tich (m²) - số thập phân >= 0, tối đa 999999.99. Không bắt buộc.</summary>
    public decimal? DienTich { get; set; }
}
