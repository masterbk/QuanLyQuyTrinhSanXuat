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

    /// <summary>Kho xuất nguyên liệu và nhập thành phẩm (dùng chung cho mọi sản phẩm trong lệnh).</summary>
    public string MaKho { get; set; } = string.Empty;

    public DateOnly NgaySanXuat { get; set; }

    public TrangThaiLenhSX TrangThai { get; set; } = TrangThaiLenhSX.MoiTao;

    /// <summary>
    /// Khi thực hiện lệnh, tự sinh một Lô sản xuất (Batch) cho thành phẩm và đồng bộ sang
    /// HanoiCheck (kèm truy xuất lô nguyên liệu → lô thành phẩm). Mặc định tắt.
    /// </summary>
    public bool TaoLoDongBo { get; set; }

    public DateTime? ThoiGianHoanThanhUtc { get; set; }

    /// <summary>Thời điểm huỷ lệnh (đã ghi bút toán đảo kho).</summary>
    public DateTime? ThoiGianHuyUtc { get; set; }

    /// <summary>Lý do huỷ - bắt buộc nhập để còn truy vết vì sao tồn kho bị đảo.</summary>
    public string? LyDoHuy { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>Các thành phẩm của lệnh - mỗi dòng là một lô riêng, có quy trình và khâu riêng.</summary>
    public List<LenhSanXuatSanPham> SanPham { get; set; } = new();

    /// <summary>Nhân viên sản xuất đã quét mã QR của lệnh để tham gia.</summary>
    public List<LenhSanXuatThamGia> ThamGia { get; set; } = new();
}
