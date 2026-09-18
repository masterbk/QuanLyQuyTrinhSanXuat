using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một khâu sản xuất của lô thành phẩm: ai làm và làm ở cơ sở nào.
///
/// Khâu lấy từ quy trình đã chọn và bắt buộc có cơ sở. Người thực hiện KHÔNG bắt buộc lúc lập lệnh (nhân viên sản
/// xuất quét mã QR của lệnh để tự tham gia) nhưng phải có khi hoàn thành - đây là dữ liệu HanoiCheck cần cho truy xuất
/// nguồn gốc (danh_sach_nguoi_thuc_hien).
/// </summary>
public class LenhSanXuatKhau : TenantEntity
{
    public int Id { get; set; }

    public int LenhSanXuatSanPhamId { get; set; }
    public LenhSanXuatSanPham? LenhSanXuatSanPham { get; set; }

    /// <summary>Mã khâu (theo danh mục Khâu sản xuất).</summary>
    public string MaKhau { get; set; } = string.Empty;

    /// <summary>Thứ tự khâu trong quy trình.</summary>
    public int ThuTu { get; set; }

    /// <summary>Cơ sở thực hiện khâu này (ma_co_so).</summary>
    public string MaCoSo { get; set; } = string.Empty;

    /// <summary>
    /// Danh sách ma_nhan_su người thực hiện, ngăn cách bằng dấu phẩy - cùng cách lưu với
    /// <see cref="BatchStep.NguoiThucHienCsv"/> để map thẳng sang payload HanoiCheck.
    /// </summary>
    public string NguoiThucHienCsv { get; set; } = string.Empty;

    public string? GhiChu { get; set; }

    public IReadOnlyList<string> NguoiThucHien =>
        string.IsNullOrWhiteSpace(NguoiThucHienCsv)
            ? Array.Empty<string>()
            : NguoiThucHienCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
