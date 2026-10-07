using HCP.Domain.Entities.Common;
using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một phiếu ghi nhận theo một biểu mẫu: nhân viên điền dữ liệu cho một ngày/ca/khu vực. Gồm nhiều dòng
/// (DongGhiNhan). Các cột thẩm tra để trống sẵn cho bước QC sau này.
/// </summary>
public class PhieuGhiNhan : TenantEntity
{
    public int Id { get; set; }

    public int BieuMauId { get; set; }
    public BieuMau? BieuMau { get; set; }

    public DateOnly Ngay { get; set; }

    /// <summary>Giá trị các trường ĐẦU PHIẾU (khu vực, biển số, tổ...): JSON {ma_truong: giá trị}.</summary>
    public string GiaTriDauJson { get; set; } = "{}";

    /// <summary>Mã nhân sự người lập phiếu.</summary>
    public string? NguoiLap { get; set; }

    public TrangThaiPhieu TrangThai { get; set; } = TrangThaiPhieu.DaGhiNhan;

    public string? GhiChu { get; set; }

    // ----- Để dành cho bước QC thẩm tra (chưa dùng) -----
    public string? NguoiThamTra { get; set; }
    public DateTime? ThoiGianThamTraUtc { get; set; }
    public bool? KetQuaThamTra { get; set; }

    public DateTime ThoiGianUtc { get; set; } = DateTime.UtcNow;

    public List<DongGhiNhan> Dong { get; set; } = new();
}
