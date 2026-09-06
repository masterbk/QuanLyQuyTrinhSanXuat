using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Phiếu xuất bán thành phẩm cho khách hàng. Khi thực hiện sẽ trừ tồn thành phẩm theo lô
/// (FEFO - lô hết hạn trước xuất trước). Nội bộ, không gửi HanoiCheck.
/// </summary>
public class PhieuXuatBan : TenantEntity
{
    public int Id { get; set; }

    /// <summary>Mã phiếu (khoá nghiệp vụ, cũng là chứng từ ghi vào sổ kho).</summary>
    public string MaPhieu { get; set; } = string.Empty;

    /// <summary>Khách hàng nhận thành phẩm.</summary>
    public string MaKhachHang { get; set; } = string.Empty;

    /// <summary>Kho xuất thành phẩm.</summary>
    public string MaKho { get; set; } = string.Empty;

    public DateOnly NgayXuat { get; set; }

    public TrangThaiXuatBan TrangThai { get; set; } = TrangThaiXuatBan.MoiTao;

    public DateTime? ThoiGianHoanThanhUtc { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>Các dòng thành phẩm cần xuất.</summary>
    public List<PhieuXuatBanChiTiet> ChiTiet { get; set; } = new();
}
