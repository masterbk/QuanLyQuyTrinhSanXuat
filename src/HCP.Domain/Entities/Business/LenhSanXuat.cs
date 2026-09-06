using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Lệnh sản xuất nội bộ: làm ra một lượng thành phẩm, tự trừ nguyên liệu theo định mức (FEFO)
/// và nhập thành phẩm vào kho. Nội bộ, không gửi HanoiCheck.
/// </summary>
public class LenhSanXuat : TenantEntity
{
    public int Id { get; set; }

    /// <summary>Mã lệnh (khoá nghiệp vụ).</summary>
    public string MaLenh { get; set; } = string.Empty;

    /// <summary>Thành phẩm cần sản xuất.</summary>
    public string MaThanhPham { get; set; } = string.Empty;

    /// <summary>Số lượng thành phẩm cần làm.</summary>
    public decimal SoLuong { get; set; }

    /// <summary>Kho xuất nguyên liệu và nhập thành phẩm.</summary>
    public string MaKho { get; set; } = string.Empty;

    /// <summary>Mã lô thành phẩm tạo ra.</summary>
    public string MaLoThanhPham { get; set; } = string.Empty;

    /// <summary>Hạn sử dụng của lô thành phẩm.</summary>
    public DateOnly? HanSuDungThanhPham { get; set; }

    public DateOnly NgaySanXuat { get; set; }

    public TrangThaiLenhSX TrangThai { get; set; } = TrangThaiLenhSX.MoiTao;

    public DateTime? ThoiGianHoanThanhUtc { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>Nguyên liệu (theo lô) đã tiêu hao khi thực hiện lệnh - lưu để truy xuất.</summary>
    public List<LenhSanXuatTieuHao> TieuHao { get; set; } = new();
}
