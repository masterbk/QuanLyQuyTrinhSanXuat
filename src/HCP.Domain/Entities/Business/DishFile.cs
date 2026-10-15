using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Ảnh/tệp của món ăn. <see cref="MaKhau"/> trống = ảnh album của món (danh_sach_anh);
/// có mã khâu = tệp minh chứng của khâu đó (danh_sach_khau[].danh_sach_files).
/// </summary>
public class DishFile : TenantEntity
{
    public int Id { get; set; }

    public int DishId { get; set; }
    public Dish? Dish { get; set; }

    /// <summary>ma_file.</summary>
    public string MaFile { get; set; } = string.Empty;

    /// <summary>ten_file.</summary>
    public string TenFile { get; set; } = string.Empty;

    /// <summary>duong_dan - tối đa 1000 ký tự.</summary>
    public string DuongDan { get; set; } = string.Empty;

    /// <summary>loai - vd HINH_ANH, GIAY_KIEM_DICH.</summary>
    public string Loai { get; set; } = string.Empty;

    /// <summary>Mã khâu sở hữu tệp; trống = ảnh album của món.</summary>
    public string? MaKhau { get; set; }
}
