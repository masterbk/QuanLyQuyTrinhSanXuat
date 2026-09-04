using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một khâu (bước sản xuất) đã thực hiện cho lô - phần tử của danh_sach_khau.
///
/// danh_sach_nguoi_thuc_hien (tối đa 20 mã nhân sự) được lưu gộp thành chuỗi CSV để tránh
/// thêm một bảng con cấp 3; khi dựng payload sẽ tách lại thành mảng. Đây chỉ là danh sách
/// mã tham chiếu đồng bộ sang HnC, không cần quan hệ khoá ngoại.
/// </summary>
public class BatchStep : TenantEntity
{
    public int Id { get; set; }

    public int BatchId { get; set; }
    public Batch? Batch { get; set; }

    /// <summary>ma_buoc_sx - mã bước sản xuất, duy nhất trong một lô.</summary>
    public string MaBuocSx { get; set; } = string.Empty;

    /// <summary>ma_khau - phải tồn tại trong danh mục khâu.</summary>
    public string MaKhau { get; set; } = string.Empty;

    /// <summary>thu_tu - 0..99999.</summary>
    public int ThuTu { get; set; }

    public string? MaLoNhap { get; set; }
    public string? MaLoNguyenLieu { get; set; }
    public string? MaLoSanXuat { get; set; }

    /// <summary>thoi_gian - thời điểm thực hiện bước.</summary>
    public DateTime? ThoiGian { get; set; }

    /// <summary>danh_sach_nguoi_thuc_hien - CSV mã nhân sự (tối đa 20).</summary>
    public string? NguoiThucHienCsv { get; set; }

    public string? DiaChi { get; set; }
    public string? TrangThai { get; set; }
    public string? MaQrTruyVet { get; set; }
    public string? GhiChu { get; set; }
    public string? MaCoSo { get; set; }
    public string? MaNccDauVao { get; set; }

    /// <summary>Danh sách mã nhân sự đã tách từ CSV (không map DB).</summary>
    public IReadOnlyList<string> NguoiThucHien =>
        string.IsNullOrWhiteSpace(NguoiThucHienCsv)
            ? Array.Empty<string>()
            : NguoiThucHienCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
