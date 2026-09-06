using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một dòng sổ kho nội bộ (nhập/xuất). Tồn kho = tổng số lượng theo (sản phẩm, kho, lô).
/// Quy ước: nhập dương, xuất âm. Đây là dữ liệu vận hành nội bộ, KHÔNG gửi sang HanoiCheck.
/// </summary>
public class KhoGiaoDich : TenantEntity
{
    public int Id { get; set; }

    /// <summary>Mã thực phẩm/SKU (nguyên liệu hoặc thành phẩm).</summary>
    public string MaSanPham { get; set; } = string.Empty;

    /// <summary>Mã kho.</summary>
    public string MaKho { get; set; } = string.Empty;

    /// <summary>Mã lô (bắt buộc để theo dõi tồn theo lô + hạn dùng).</summary>
    public string MaLo { get; set; } = string.Empty;

    /// <summary>Số lượng: nhập dương, xuất âm.</summary>
    public decimal SoLuong { get; set; }

    /// <summary>Hạn sử dụng của lô (mang theo ở dòng nhập; phục vụ xuất theo FEFO và cảnh báo).</summary>
    public DateOnly? HanSuDung { get; set; }

    public LoaiGiaoDichKho Loai { get; set; }

    /// <summary>Số chứng từ tham chiếu (vd phiếu nhập, lệnh sản xuất, phiếu xuất bán).</summary>
    public string? ChungTu { get; set; }

    /// <summary>Mã NCC đầu vào (với dòng nhập nguyên liệu).</summary>
    public string? MaNccDauVao { get; set; }

    public string? GhiChu { get; set; }

    public DateTime ThoiGianUtc { get; set; } = DateTime.UtcNow;
}
