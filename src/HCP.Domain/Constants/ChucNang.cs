namespace HCP.Domain.Constants;

/// <summary>
/// Chức năng (menu) mà quản trị nền tảng mở cho từng cơ sở (NCC). Tầng này chồng lên role nhân viên:
/// màn/API chỉ dùng được khi cơ sở được mở chức năng VÀ người dùng có role phù hợp.
///
/// Trước 18/10/2026 đây là 6 "module" gộp (LoSanXuat, SuatAn, KhoSanXuat, BanHang, HanoiCheck,
/// KiemSoatAttp) - quá thô: NCC suất ăn cần Tồn kho nhưng không dùng Lệnh sản xuất, mà hai thứ đó
/// nằm chung một module nên không tách được. Nay mỗi menu là một mã riêng.
///
/// Màn Tổng quan luôn mở, không có mã. Tắt chức năng chỉ ẩn + chặn truy cập, KHÔNG xoá dữ liệu.
/// </summary>
public static class ChucNang
{
    // --- Danh mục ---
    public const string Kho = "Kho";
    public const string CoSoSanXuat = "CoSoSanXuat";
    public const string KhauSanXuat = "KhauSanXuat";
    public const string QuyTrinh = "QuyTrinh";
    public const string NccDauVao = "NccDauVao";
    public const string NhanSu = "NhanSu";
    public const string ThucPham = "ThucPham";
    public const string LoSanXuat = "LoSanXuat";
    public const string MonAn = "MonAn";
    public const string ThucDon = "ThucDon";

    // --- Kho & Sản xuất ---
    public const string TonKho = "TonKho";
    public const string DinhMuc = "DinhMuc";
    public const string LenhSanXuat = "LenhSanXuat";

    // --- Bán hàng ---
    public const string KhachHang = "KhachHang";
    public const string DonHangBan = "DonHangBan";
    public const string DonHangNhan = "DonHangNhan";

    // --- Kết nối HanoiCheck ---
    public const string KetNoiHnC = "KetNoiHnC";
    public const string NhatKyDongBo = "NhatKyDongBo";

    // --- Kiểm soát ATTP ---
    public const string NhapPhieu = "NhapPhieu";
    public const string PhieuGhiNhan = "PhieuGhiNhan";
    public const string BieuMau = "BieuMau";
    public const string NhacHan = "NhacHan";

    /// <summary>Tên chính sách uỷ quyền tương ứng (dùng trong [Authorize(Policy = ...)]).</summary>
    public static class ChinhSach
    {
        public const string TienTo = "ChucNang:";

        public const string Kho = TienTo + ChucNang.Kho;
        public const string CoSoSanXuat = TienTo + ChucNang.CoSoSanXuat;
        public const string KhauSanXuat = TienTo + ChucNang.KhauSanXuat;
        public const string QuyTrinh = TienTo + ChucNang.QuyTrinh;
        public const string NccDauVao = TienTo + ChucNang.NccDauVao;
        public const string NhanSu = TienTo + ChucNang.NhanSu;
        public const string ThucPham = TienTo + ChucNang.ThucPham;
        public const string LoSanXuat = TienTo + ChucNang.LoSanXuat;
        public const string MonAn = TienTo + ChucNang.MonAn;
        public const string ThucDon = TienTo + ChucNang.ThucDon;
        public const string TonKho = TienTo + ChucNang.TonKho;
        public const string DinhMuc = TienTo + ChucNang.DinhMuc;
        public const string LenhSanXuat = TienTo + ChucNang.LenhSanXuat;
        public const string KhachHang = TienTo + ChucNang.KhachHang;
        public const string DonHangBan = TienTo + ChucNang.DonHangBan;
        public const string DonHangNhan = TienTo + ChucNang.DonHangNhan;
        public const string KetNoiHnC = TienTo + ChucNang.KetNoiHnC;
        public const string NhatKyDongBo = TienTo + ChucNang.NhatKyDongBo;
        public const string NhapPhieu = TienTo + ChucNang.NhapPhieu;
        public const string PhieuGhiNhan = TienTo + ChucNang.PhieuGhiNhan;
        public const string BieuMau = TienTo + ChucNang.BieuMau;
        public const string NhacHan = TienTo + ChucNang.NhacHan;
    }

    /// <summary>Tên nhóm trên cây menu - trùng nhóm của NavMenu để quản trị nhìn phát hiểu ngay.</summary>
    public static class TenNhom
    {
        public const string DanhMuc = "Danh mục";
        public const string KhoSanXuat = "Kho & Sản xuất";
        public const string BanHang = "Bán hàng";
        public const string HanoiCheck = "Kết nối HanoiCheck";
        public const string KiemSoatAttp = "Kiểm soát ATTP";
    }

    /// <param name="PhuThuoc">Bật chức năng này thì tự bật kèm các mã ở đây (và ngược lại: tắt mã ở
    /// đây thì chức năng này cũng tắt theo) - tránh mở nửa vời rồi lỗi khi dùng.</param>
    public sealed record MoTaChucNang(string Ma, string Ten, string Nhom, string MoTa, IReadOnlyList<string> PhuThuoc);

    /// <summary>Thứ tự hiển thị trên cây menu của màn quản trị.</summary>
    public static readonly IReadOnlyList<MoTaChucNang> DanhSach =
    [
        new(Kho,          "Kho",                  TenNhom.DanhMuc, "Danh mục nhà kho", []),
        new(CoSoSanXuat,  "Cơ sở sản xuất",       TenNhom.DanhMuc, "Địa điểm sản xuất của cơ sở", []),
        new(KhauSanXuat,  "Khâu sản xuất",        TenNhom.DanhMuc, "Các khâu dùng cho quy trình", []),
        new(QuyTrinh,     "Quy trình sản xuất",   TenNhom.DanhMuc, "Chuỗi khâu theo thứ tự", [KhauSanXuat]),
        new(NccDauVao,    "Nhà cung ứng đầu vào", TenNhom.DanhMuc, "NCC nguyên liệu đầu vào", []),
        new(NhanSu,       "Nhân sự",              TenNhom.DanhMuc, "Nhân sự và tài khoản đăng nhập", []),
        new(ThucPham,     "Thực phẩm / SKU",      TenNhom.DanhMuc, "Nguyên liệu và thành phẩm", []),
        new(LoSanXuat,    "Lô sản xuất",          TenNhom.DanhMuc, "Lô nhập kho, truy xuất nguồn gốc", [ThucPham]),
        new(MonAn,        "Món ăn",               TenNhom.DanhMuc, "Món ăn cho suất ăn trường học", [ThucPham, QuyTrinh]),
        new(ThucDon,      "Thực đơn",             TenNhom.DanhMuc, "Thực đơn theo thứ trong tuần", [MonAn]),

        new(TonKho,       "Tồn kho & nhập",       TenNhom.KhoSanXuat, "Tồn theo lô, nhập, kiểm kê", [Kho, ThucPham]),
        new(DinhMuc,      "Định mức",             TenNhom.KhoSanXuat, "Định mức nguyên liệu cho thành phẩm", [ThucPham]),
        new(LenhSanXuat,  "Lệnh sản xuất",        TenNhom.KhoSanXuat, "Lệnh sản xuất trên web và app", [TonKho, DinhMuc, QuyTrinh]),

        new(KhachHang,    "Khách hàng",           TenNhom.BanHang, "Danh mục khách mua hàng", []),
        new(DonHangBan,   "Đơn hàng bán",         TenNhom.BanHang, "Đơn bán, xuất kho, giao hàng trên app", [KhachHang, TonKho]),
        new(DonHangNhan,  "Đơn hàng từ trường",   TenNhom.BanHang, "Đơn trường đặt qua HanoiCheck", [DonHangBan]),

        new(KetNoiHnC,    "Cài đặt kết nối",      TenNhom.HanoiCheck, "Khai báo credential, kiểm tra API", []),
        new(NhatKyDongBo, "Nhật ký đồng bộ",      TenNhom.HanoiCheck, "Theo dõi và gửi lại bản ghi lỗi", [KetNoiHnC]),

        new(BieuMau,      "Biểu mẫu kiểm soát",   TenNhom.KiemSoatAttp, "Khai báo mẫu phiếu", []),
        new(NhapPhieu,    "Nhập phiếu",           TenNhom.KiemSoatAttp, "Nhập phiếu trên web và app", [BieuMau]),
        new(PhieuGhiNhan, "Phiếu ghi nhận",       TenNhom.KiemSoatAttp, "Tra cứu, xuất PDF phiếu đã nhập", [BieuMau]),
        new(NhacHan,      "Nhắc hạn thiết bị",    TenNhom.KiemSoatAttp, "Nhắc hiệu chuẩn, bảo trì thiết bị", []),
    ];

    public static readonly IReadOnlyList<string> TatCa = DanhSach.Select(c => c.Ma).ToList();

    /// <summary>Các nhóm theo đúng thứ tự xuất hiện - để dựng cây trên giao diện quản trị.</summary>
    public static readonly IReadOnlyList<string> CacNhom =
        DanhSach.Select(c => c.Nhom).Distinct().ToList();

    public static IReadOnlyList<MoTaChucNang> TheoNhom(string nhom) =>
        DanhSach.Where(c => c.Nhom == nhom).ToList();

    /// <summary>Danh mục thuần - cơ sở nào cũng cần để khai dữ liệu nền.</summary>
    private static readonly string[] CoBan =
        [Kho, CoSoSanXuat, KhauSanXuat, QuyTrinh, NccDauVao, NhanSu, ThucPham];

    private static readonly string[] Attp = [BieuMau, NhapPhieu, PhieuGhiNhan, NhacHan];
    private static readonly string[] BanHangDayDu = [KhachHang, DonHangBan, DonHangNhan, KetNoiHnC, NhatKyDongBo];

    /// <summary>Gói mẫu để quản trị nền tảng bấm nhanh thay vì tick từng ô.</summary>
    public static readonly IReadOnlyList<(string Ten, IReadOnlyList<string> Ma)> GoiMau =
    [
        ("Cơ bản", CoBan),
        ("Tiệm bánh", [.. CoBan, LoSanXuat, TonKho, DinhMuc, LenhSanXuat, .. BanHangDayDu, .. Attp]),
        ("Suất ăn trường học", [.. CoBan, MonAn, ThucDon, TonKho, .. BanHangDayDu, .. Attp]),
        ("Đầy đủ", TatCa),
    ];

    public static string Ten(string ma) => DanhSach.FirstOrDefault(c => c.Ma == ma)?.Ten ?? ma;

    public static IReadOnlyList<string> Tach(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Where(TatCa.Contains).Distinct().ToList();

    /// <summary>Chuẩn hoá: bỏ mã lạ, tự thêm chức năng phụ thuộc, sắp theo thứ tự hiển thị, ghép CSV.</summary>
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

    // ---------------------------------------------------------------------------------------
    // Tương thích app đã cài
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Sáu mã module CŨ mà các bản app phát trước 18/10/2026 dùng để ẩn/hiện tab. Máy chủ vẫn trả kèm
    /// chúng trong /auth/* để app cũ chạy y như trước; app mới dùng mã mịn ở trên.
    /// KHÔNG dùng cho web hay cho chính sách uỷ quyền - chỉ để trả về cho app.
    /// </summary>
    public static class MaCu
    {
        public const string LoSanXuat = "LoSanXuat";
        public const string SuatAn = "SuatAn";
        public const string KhoSanXuat = "KhoSanXuat";
        public const string BanHang = "BanHang";
        public const string HanoiCheck = "HanoiCheck";
        public const string KiemSoatAttp = "KiemSoatAttp";
    }

    /// <summary>Mã mới + mã cũ suy ra từ chúng (xem <see cref="MaCu"/>).</summary>
    public static IReadOnlyList<string> KemMaCu(string? csv)
    {
        var co = Tach(csv);
        var ra = co.ToList();

        void Them(string maCu, params string[] can)
        {
            if (can.Any(co.Contains) && !ra.Contains(maCu)) ra.Add(maCu);
        }

        // Tab app cũ: Lệnh sản xuất + Kho dùng chung KhoSanXuat; Biểu mẫu dùng KiemSoatAttp...
        Them(MaCu.KhoSanXuat, LenhSanXuat, TonKho, DinhMuc);
        Them(MaCu.BanHang, DonHangBan, KhachHang);
        Them(MaCu.SuatAn, MonAn, ThucDon);
        Them(MaCu.KiemSoatAttp, BieuMau, NhapPhieu, PhieuGhiNhan, NhacHan);
        Them(MaCu.HanoiCheck, KetNoiHnC, NhatKyDongBo, DonHangNhan);
        return ra;
    }

    /// <summary>
    /// Quy đổi CSV mã CŨ sang mã mới - dùng cho migration dữ liệu cơ sở đã có.
    /// Mọi cơ sở đều được mở nhóm danh mục cơ bản vì trước đây nhóm đó luôn hiện.
    /// </summary>
    public static string TuMaCu(string? csvCu)
    {
        var cu = (csvCu ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .ToHashSet(StringComparer.Ordinal);
        var moi = new List<string>(CoBan);

        if (cu.Contains(MaCu.LoSanXuat)) moi.Add(LoSanXuat);
        if (cu.Contains(MaCu.SuatAn)) moi.AddRange([MonAn, ThucDon]);
        if (cu.Contains(MaCu.KhoSanXuat)) moi.AddRange([TonKho, DinhMuc, LenhSanXuat]);
        if (cu.Contains(MaCu.BanHang)) moi.AddRange([KhachHang, DonHangBan]);
        if (cu.Contains(MaCu.HanoiCheck)) moi.AddRange([KetNoiHnC, NhatKyDongBo, DonHangNhan]);
        if (cu.Contains(MaCu.KiemSoatAttp)) moi.AddRange(Attp);

        return Ghep(moi);
    }
}
