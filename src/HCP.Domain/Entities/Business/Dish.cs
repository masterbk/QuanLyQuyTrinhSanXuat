using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Món ăn của cơ sở (suất ăn). Đồng bộ qua POST /supplier/dishes/merge (bất đồng bộ, trả 202).
/// Gồm thông tin món, công thức (nguyên liệu), các khâu chế biến và file minh chứng.
/// </summary>
public class Dish : TenantEntity
{
    public int Id { get; set; }

    /// <summary>ma_mon_an - khoá nghiệp vụ.</summary>
    public string MaMonAn { get; set; } = string.Empty;

    /// <summary>ten_mon_an.</summary>
    public string TenMonAn { get; set; } = string.Empty;

    /// <summary>
    /// nhom_tuoi_id - mã nhóm tuổi (integer), bắt buộc theo đặc tả. HnC chưa cung cấp danh mục
    /// tra mã này (giống ma_danh_muc_thuc_pham) - cần hỏi đơn vị vận hành khi kiểm thử.
    /// </summary>
    public int NhomTuoiId { get; set; }

    /// <summary>mo_ta.</summary>
    public string? MoTa { get; set; }

    /// <summary>ma_co_so - cơ sở chế biến.</summary>
    public string? MaCoSo { get; set; }

    /// <summary>ma_quy_trinh - quy trình chế biến.</summary>
    public string? MaQuyTrinh { get; set; }

    /// <summary>danh_sach_nguyen_lieu - công thức, bắt buộc tối thiểu 1.</summary>
    public List<DishIngredient> DanhSachNguyenLieu { get; set; } = new();

    /// <summary>danh_sach_khau - các khâu chế biến.</summary>
    public List<DishStep> DanhSachKhau { get; set; } = new();

    /// <summary>danh_sach_file - file minh chứng theo khâu.</summary>
    public List<DishFile> DanhSachFile { get; set; } = new();
}
