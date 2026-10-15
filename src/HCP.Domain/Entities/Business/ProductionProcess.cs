using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Quy trình sản xuất: một chuỗi khâu sản xuất có thứ tự, dùng cho thực phẩm hoặc cho món ăn.
/// Đồng bộ qua POST /supplier/processes/merge.
/// </summary>
public class ProductionProcess : TenantEntity, ICoDongBoHnC
{
    public int Id { get; set; }

    /// <summary>Có gửi bản ghi này sang HanoiCheck không (khi cơ sở bật đồng bộ). Mặc định có.</summary>
    public bool DongBoHnC { get; set; } = true;

    /// <summary>ma_quy_trinh - khoá nghiệp vụ.</summary>
    public string MaQuyTrinh { get; set; } = string.Empty;

    /// <summary>ten_quy_trinh.</summary>
    public string TenQuyTrinh { get; set; } = string.Empty;

    /// <summary>
    /// loai_san_pham (đặc tả v2.6): "food" = quy trình của thực phẩm, "dish" = quy trình chế biến món ăn.
    /// HanoiCheck chỉ cho thực phẩm gắn quy trình food và món ăn gắn quy trình dish. Xem <see cref="LoaiQuyTrinh"/>.
    /// </summary>
    public string LoaiApDung { get; set; } = LoaiQuyTrinh.ThucPham;

    /// <summary>danh_sach_khau - tối thiểu 1 khâu, có thứ tự thực hiện.</summary>
    public List<ProcessStepLine> DanhSachKhau { get; set; } = new();
}

/// <summary>Giá trị loai_san_pham của quy trình theo đặc tả HanoiCheck v2.6.</summary>
public static class LoaiQuyTrinh
{
    public const string ThucPham = "food";
    public const string MonAn = "dish";

    public static bool HopLe(string? loai) => loai is ThucPham or MonAn;

    public static string Ten(string? loai) => loai == MonAn ? "Món ăn" : "Thực phẩm";
}
