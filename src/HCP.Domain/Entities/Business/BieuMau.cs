using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Định nghĩa một biểu mẫu kiểm soát (GMP/ISO) - mẫu để nhân viên ghi nhận dữ liệu thay cho biểu mẫu giấy.
/// Một biểu mẫu gồm các TRƯỜNG (cột) và - với bố cục Checklist - các HẠNG MỤC cố định. Hồ sơ nội bộ, KHÔNG
/// đồng bộ HanoiCheck.
/// </summary>
public class BieuMau : TenantEntity
{
    public int Id { get; set; }

    /// <summary>Mã hiệu biểu mẫu (khoá nghiệp vụ), vd "BM-GMP.08-04".</summary>
    public string MaHieu { get; set; } = string.Empty;

    public string Ten { get; set; } = string.Empty;

    public BoCucBieuMau BoCuc { get; set; } = BoCucBieuMau.TheoNgay;

    /// <summary>Ngày ban hành biểu mẫu (in ở header PDF, theo hồ sơ tài liệu).</summary>
    public DateOnly? NgayBanHanh { get; set; }

    /// <summary>Lần ban hành, vd "01", "02" (in ở header PDF).</summary>
    public string? LanBanHanh { get; set; }

    /// <summary>Tần suất thực hiện (mô tả tự do), vd "Hàng ngày", "Mỗi mẻ", "Mỗi lần nhập".</summary>
    public string? TanSuat { get; set; }

    /// <summary>
    /// KHÔNG CÒN DÙNG để lọc (từ 08/10/2026 quyền nhập do role "Nhân viên nhập biểu mẫu" + PhanQuyenBieuMau quyết định).
    /// Giữ cột để không phải migration xoá dữ liệu.
    /// </summary>
    public string NhomQuyen { get; set; } = string.Empty;

    /// <summary>Quy chuẩn / chú thích in ở chân biểu mẫu.</summary>
    public string? GhiChuChan { get; set; }

    public bool KichHoat { get; set; } = true;

    /// <summary>
    /// true = mỗi ngày chỉ MỘT phiếu (khoá nghiệp vụ = biểu mẫu + ngày): mở ngày đã có phiếu thì sửa (nếu còn
    /// nháp) hoặc xem (nếu đã hoàn thành), không tạo trùng. false = cho NHIỀU phiếu trong một ngày (sổ/log, vd
    /// sản phẩm không phù hợp): mỗi lần là một phiếu riêng.
    /// </summary>
    public bool MotPhieuMoiNgay { get; set; } = true;

    public int ThuTu { get; set; }

    public List<TruongBieuMau> Truong { get; set; } = new();

    /// <summary>Danh sách hạng mục cố định (chỉ dùng cho bố cục Checklist).</summary>
    public List<HangMucBieuMau> HangMuc { get; set; } = new();
}
