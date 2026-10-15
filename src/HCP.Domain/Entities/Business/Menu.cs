using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Thực đơn theo thứ trong tuần của một nhóm tuổi (đặc tả HanoiCheck v2.6, dịch vụ 10).
/// Đồng bộ qua POST /supplier/menus/merge. HanoiCheck không có dịch vụ xoá: thực đơn không còn dùng
/// thì gửi trang_thai = false.
/// </summary>
public class Menu : TenantEntity, ICoDongBoHnC
{
    public int Id { get; set; }

    /// <summary>Có gửi bản ghi này sang HanoiCheck không (khi cơ sở bật đồng bộ). Mặc định có.</summary>
    public bool DongBoHnC { get; set; } = true;

    /// <summary>ma_thuc_don - khoá nghiệp vụ.</summary>
    public string MaThucDon { get; set; } = string.Empty;

    /// <summary>ten_thuc_don.</summary>
    public string TenThucDon { get; set; } = string.Empty;

    /// <summary>nhom_tuoi_id - mọi món trong thực đơn phải cùng nhóm tuổi này.</summary>
    public int NhomTuoiId { get; set; }

    /// <summary>thu_ap_dung - 0 là Chủ nhật, 1 là Thứ 2 ... 6 là Thứ 7.</summary>
    public int ThuApDung { get; set; } = 1;

    /// <summary>trang_thai - thực đơn còn áp dụng hay không.</summary>
    public bool TrangThai { get; set; } = true;

    /// <summary>
    /// danh_sach_truong - id trường (GET /supplier/schools, KHÔNG phải mã trường), lưu gộp CSV.
    /// Trống = dùng chung cho toàn bộ trường cơ sở được phân công.
    /// </summary>
    public string? DanhSachTruongCsv { get; set; }

    /// <summary>danh_sach_mon - các dòng (bữa ăn, món), ít nhất 1, THAY THẾ TOÀN BỘ mỗi lần gửi.</summary>
    public List<MenuItem> DanhSachMon { get; set; } = new();

    /// <summary>Danh sách id trường đã tách từ CSV (không map DB).</summary>
    public IReadOnlyList<int> DanhSachTruong =>
        string.IsNullOrWhiteSpace(DanhSachTruongCsv)
            ? Array.Empty<int>()
            : DanhSachTruongCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var id) ? id : 0).Where(id => id > 0).Distinct().ToArray();

    /// <summary>Tên thứ trong tuần theo quy ước thu_ap_dung.</summary>
    public static string TenThu(int thu) => thu switch
    {
        0 => "Chủ nhật",
        >= 1 and <= 6 => $"Thứ {thu + 1}",
        _ => $"({thu})"
    };
}

/// <summary>Một dòng của thực đơn: món ăn phục vụ ở bữa ăn nào (phần tử của danh_sach_mon).</summary>
public class MenuItem : TenantEntity
{
    public int Id { get; set; }

    public int MenuId { get; set; }
    public Menu? Menu { get; set; }

    /// <summary>buoi_an_id - id bữa ăn trong danh mục GET /supplier/meal-types.</summary>
    public int BuoiAnId { get; set; }

    /// <summary>ma_mon_an - món của chính cơ sở, cùng nhóm tuổi với thực đơn.</summary>
    public string MaMonAn { get; set; } = string.Empty;
}
