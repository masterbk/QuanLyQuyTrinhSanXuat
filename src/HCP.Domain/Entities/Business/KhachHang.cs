using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Khách hàng mua thành phẩm của cơ sở (cửa hàng, đại lý, khách lẻ...). Dữ liệu nội bộ,
/// KHÔNG gửi sang HanoiCheck.
/// </summary>
public class KhachHang : TenantEntity
{
    public int Id { get; set; }

    /// <summary>Mã khách hàng (khoá nghiệp vụ).</summary>
    public string MaKhachHang { get; set; } = string.Empty;

    public string TenKhachHang { get; set; } = string.Empty;

    public LoaiKhachHang Loai { get; set; } = LoaiKhachHang.CuaHang;

    public string? DienThoai { get; set; }

    public string? DiaChi { get; set; }

    public string? GhiChu { get; set; }
}
