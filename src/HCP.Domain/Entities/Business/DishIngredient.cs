using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một nguyên liệu trong công thức món ăn - phần tử của danh_sach_nguyen_lieu.
/// </summary>
public class DishIngredient : TenantEntity
{
    public int Id { get; set; }

    public int DishId { get; set; }
    public Dish? Dish { get; set; }

    /// <summary>ma_nguyen_lieu - mã thực phẩm (SKU) làm nguyên liệu.</summary>
    public string MaNguyenLieu { get; set; } = string.Empty;

    /// <summary>
    /// dinh_luong - định lượng cho một suất, tính theo đơn vị tính HanoiCheck của chính thực phẩm đó
    /// (v2.6 bỏ don_vi_tinh_id trên dòng công thức).
    /// </summary>
    public decimal DinhLuong { get; set; }
}
