namespace HCP.Domain.Constants;

/// <summary>
/// Module chức năng quản trị nền tảng mở cho từng cơ sở (NCC). Tầng này chồng lên role nhân viên:
/// màn/API chỉ dùng được khi cơ sở được mở module VÀ người dùng có role phù hợp.
/// Danh mục cơ bản (Tổng quan, Kho, Cơ sở SX, Khâu, Quy trình, NCC đầu vào, Nhân sự, Thực phẩm) luôn mở - không có mã.
/// Tắt module chỉ ẩn + chặn truy cập, KHÔNG xoá dữ liệu.
/// </summary>
public static class ChucNang
{
    public const string LoSanXuat = "LoSanXuat";
    public const string SuatAn = "SuatAn";
    public const string KhoSanXuat = "KhoSanXuat";
    public const string BanHang = "BanHang";
    public const string HanoiCheck = "HanoiCheck";
    public const string KiemSoatAttp = "KiemSoatAttp";

    /// <summary>Tên chính sách uỷ quyền tương ứng từng module (dùng trong [Authorize(Policy = ...)]).</summary>
    public static class ChinhSach
    {
        public const string TienTo = "ChucNang:";
        public const string LoSanXuat = TienTo + ChucNang.LoSanXuat;
        public const string SuatAn = TienTo + ChucNang.SuatAn;
        public const string KhoSanXuat = TienTo + ChucNang.KhoSanXuat;
        public const string BanHang = TienTo + ChucNang.BanHang;
        public const string HanoiCheck = TienTo + ChucNang.HanoiCheck;
        public const string KiemSoatAttp = TienTo + ChucNang.KiemSoatAttp;
    }

    public sealed record MoTaChucNang(string Ma, string Ten, string MoTa, IReadOnlyList<string> PhuThuoc);

    /// <summary>Thứ tự hiển thị trên màn quản trị.</summary>
    public static readonly IReadOnlyList<MoTaChucNang> DanhSach =
    [
        new(LoSanXuat, "Lô sản xuất", "Lô sản xuất, truy xuất nguồn gốc theo lô", []),
        new(SuatAn, "Suất ăn", "Món ăn, Thực đơn", []),
        new(KhoSanXuat, "Kho & sản xuất nội bộ", "Tồn kho & nhập, Định mức, Lệnh sản xuất (web + app)", []),
        new(BanHang, "Bán hàng", "Khách hàng, Đơn hàng bán, giao hàng trên app (xuất kho trừ tồn)", [KhoSanXuat]),
        new(HanoiCheck, "Kết nối HanoiCheck", "Cài đặt kết nối, đồng bộ, đơn hàng từ trường", [BanHang]),
        new(KiemSoatAttp, "Kiểm soát ATTP", "Biểu mẫu kiểm soát, nhập phiếu, nhắc hạn thiết bị (web + app)", []),
    ];

    public static readonly IReadOnlyList<string> TatCa = DanhSach.Select(c => c.Ma).ToList();

    /// <summary>Gói mẫu để quản trị nền tảng bấm nhanh.</summary>
    public static readonly IReadOnlyList<(string Ten, IReadOnlyList<string> Ma)> GoiMau =
    [
        ("Cơ bản", []),
        ("Tiệm bánh", [LoSanXuat, KhoSanXuat, BanHang, HanoiCheck, KiemSoatAttp]),
        ("Suất ăn trường học", [SuatAn, KhoSanXuat, BanHang, HanoiCheck, KiemSoatAttp]),
        ("Đầy đủ", TatCa),
    ];

    public static string Ten(string ma) => DanhSach.FirstOrDefault(c => c.Ma == ma)?.Ten ?? ma;

    public static IReadOnlyList<string> Tach(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Where(TatCa.Contains).Distinct().ToList();

    /// <summary>Chuẩn hoá: bỏ mã lạ, tự thêm module phụ thuộc, sắp theo thứ tự hiển thị, ghép CSV.</summary>
    public static string Ghep(IEnumerable<string> ma)
    {
        var chon = new HashSet<string>(ma.Where(TatCa.Contains));
        bool them;
        do
        {
            them = false;
            foreach (var c in DanhSach.Where(c => chon.Contains(c.Ma)).ToList())
                foreach (var p in c.PhuThuoc)
                    them |= chon.Add(p);
        } while (them);
        return string.Join(",", TatCa.Where(chon.Contains));
    }

    public static bool Co(string? csv, string ma) => Tach(csv).Contains(ma);
}
