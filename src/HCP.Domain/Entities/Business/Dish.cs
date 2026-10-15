using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Món ăn của cơ sở (suất ăn). Đồng bộ qua POST /supplier/dishes/merge (bất đồng bộ, trả 202).
/// Gồm thông tin món, công thức (nguyên liệu), các khâu chế biến và file minh chứng.
/// </summary>
public class Dish : TenantEntity, ICoDongBoHnC
{
    public int Id { get; set; }

    /// <summary>Có gửi bản ghi này sang HanoiCheck không (khi cơ sở bật đồng bộ). Mặc định có.</summary>
    public bool DongBoHnC { get; set; } = true;

    /// <summary>ma_mon_an - khoá nghiệp vụ.</summary>
    public string MaMonAn { get; set; } = string.Empty;

    /// <summary>ten_mon_an.</summary>
    public string TenMonAn { get; set; } = string.Empty;

    /// <summary>
    /// nhom_tuoi_id - id nhóm tuổi lấy từ GET /supplier/student-groups (bảng NhomTuoiHnC). Mỗi món đúng
    /// một nhóm tuổi; cùng món cho nhiều nhóm tuổi thì khai nhiều món với mã khác nhau.
    /// </summary>
    public int NhomTuoiId { get; set; }

    /// <summary>mo_ta.</summary>
    public string? MoTa { get; set; }

    /// <summary>ma_quy_trinh - quy trình chế biến, phải là quy trình loại món ăn ("dish"). Bắt buộc khi gửi HanoiCheck.</summary>
    public string? MaQuyTrinh { get; set; }

    /// <summary>danh_sach_nguyen_lieu - công thức, bắt buộc tối thiểu 1.</summary>
    public List<DishIngredient> DanhSachNguyenLieu { get; set; } = new();

    /// <summary>danh_sach_khau - các khâu chế biến.</summary>
    public List<DishStep> DanhSachKhau { get; set; } = new();

    /// <summary>
    /// Ảnh/tệp của món: file KHÔNG gắn mã khâu = album ảnh của món (danh_sach_anh, bắt buộc 1-3);
    /// file có mã khâu = tệp minh chứng của khâu đó (danh_sach_khau[].danh_sach_files, tối đa 3).
    /// </summary>
    public List<DishFile> DanhSachFile { get; set; } = new();
}
