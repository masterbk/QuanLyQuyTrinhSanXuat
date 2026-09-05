using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>File minh chứng gắn theo khâu của món ăn - phần tử của danh_sach_file.</summary>
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

    public string? MaKhau { get; set; }
    public string? MaBuocSx { get; set; }
}
