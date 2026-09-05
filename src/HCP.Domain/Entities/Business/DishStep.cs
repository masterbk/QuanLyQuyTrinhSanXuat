using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một khâu chế biến của món ăn - phần tử của danh_sach_khau (giống cấu trúc khâu của lô,
/// nhưng bỏ các trường mã lô vốn chỉ hợp với lô sản xuất).
/// danh_sach_nguoi_thuc_hien lưu gộp CSV, tách lại thành mảng khi dựng payload.
/// </summary>
public class DishStep : TenantEntity
{
    public int Id { get; set; }

    public int DishId { get; set; }
    public Dish? Dish { get; set; }

    /// <summary>ma_buoc_sx - mã bước sản xuất (nếu có).</summary>
    public string? MaBuocSx { get; set; }

    /// <summary>ma_khau - phải tồn tại trong danh mục khâu.</summary>
    public string MaKhau { get; set; } = string.Empty;

    /// <summary>thu_tu - 0..99999.</summary>
    public int ThuTu { get; set; }

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
