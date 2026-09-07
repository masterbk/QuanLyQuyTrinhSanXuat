using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;

namespace HCP.Infrastructure.Services.Kho;

/// <summary>Yêu cầu nhập kho nguyên liệu.</summary>
public sealed class NhapKhoRequest
{
    public string MaSanPham { get; set; } = string.Empty;
    public string MaKho { get; set; } = string.Empty;
    public string MaLo { get; set; } = string.Empty;
    public decimal SoLuong { get; set; }
    public DateOnly? HanSuDung { get; set; }
    public string? MaNccDauVao { get; set; }
    public string? GhiChu { get; set; }
}

/// <summary>
/// Yêu cầu điều chỉnh tồn (kiểm kê): đặt tồn của một lô về đúng số đếm được thực tế.
/// Hệ thống tự tính chênh lệch so với tồn sổ và ghi một dòng "Điều chỉnh" (+/−).
/// </summary>
public sealed class DieuChinhTonRequest
{
    public string MaSanPham { get; set; } = string.Empty;
    public string MaKho { get; set; } = string.Empty;
    public string MaLo { get; set; } = string.Empty;

    /// <summary>Số tồn thực tế đếm được (sẽ trở thành tồn mới của lô).</summary>
    public decimal SoLuongThucTe { get; set; }

    /// <summary>Lý do điều chỉnh (hao hụt, vỡ hỏng, đếm lại...). Nên ghi để truy vết.</summary>
    public string? LyDo { get; set; }
}

/// <summary>Một dòng tồn kho theo (sản phẩm, kho, lô).</summary>
public sealed record TonKhoDto(
    string MaSanPham,
    string TenSanPham,
    LoaiSanPham LoaiSanPham,
    string? DonViTinh,
    string MaKho,
    string TenKho,
    string MaLo,
    DateOnly? HanSuDung,
    decimal SoLuongTon);

/// <summary>
/// Quản lý kho nội bộ (nhập–xuất–tồn theo lô) cho cơ sở đang đăng nhập. Đây là nghiệp vụ vận
/// hành nội bộ, KHÔNG đồng bộ sang HanoiCheck.
/// </summary>
public interface IKhoNoiBoService
{
    Task<KetQuaThaoTac> NhapNguyenLieuAsync(NhapKhoRequest req, CancellationToken ct = default);

    /// <summary>Điều chỉnh tồn một lô về số thực tế (kiểm kê); ghi dòng "Điều chỉnh" chênh lệch.</summary>
    Task<KetQuaThaoTac> DieuChinhTonAsync(DieuChinhTonRequest req, CancellationToken ct = default);

    /// <summary>Tồn kho hiện tại theo từng lô (chỉ các dòng còn tồn khác 0).</summary>
    Task<IReadOnlyList<TonKhoDto>> LayTonAsync(CancellationToken ct = default);

    /// <summary>Lịch sử giao dịch kho gần đây.</summary>
    Task<IReadOnlyList<KhoGiaoDich>> LayLichSuAsync(int gioiHan = 100, CancellationToken ct = default);
}
