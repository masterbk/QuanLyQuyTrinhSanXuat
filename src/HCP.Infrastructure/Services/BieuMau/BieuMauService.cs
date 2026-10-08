using System.Text.Json;
using HCP.Domain;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Business;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BieuMauEntity = HCP.Domain.Entities.Business.BieuMau;

namespace HCP.Infrastructure.Services.BieuMau;

/// <inheritdoc cref="IBieuMauService"/>
public sealed class BieuMauService : IBieuMauService
{
    private readonly AppDbContext _db;
    public BieuMauService(AppDbContext db) => _db = db;

    private IQueryable<BieuMauEntity> QueryDayDu() => _db.BieuMaus
        .Include(b => b.Truong.OrderBy(t => t.ThuTu))
        .Include(b => b.HangMuc.OrderBy(h => h.ThuTu));

    public async Task<IReadOnlyList<BieuMauEntity>> LayTatCaAsync(CancellationToken ct = default) =>
        await QueryDayDu().AsNoTracking().AsSplitQuery()
            .OrderBy(b => b.ThuTu).ThenBy(b => b.Ten).ToListAsync(ct);

    public Task<BieuMauEntity?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        QueryDayDu().AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<KetQuaThaoTac> LuuAsync(BieuMauEntity mau, CancellationToken ct = default)
    {
        mau.MaHieu = mau.MaHieu?.Trim() ?? "";
        mau.Ten = mau.Ten?.Trim() ?? "";
        mau.LanBanHanh = string.IsNullOrWhiteSpace(mau.LanBanHanh) ? null : mau.LanBanHanh.Trim();
        mau.NhomQuyen = string.IsNullOrWhiteSpace(mau.NhomQuyen) ? AppRoles.QuyenSanXuat : mau.NhomQuyen.Trim();
        if (mau.MaHieu.Length == 0) return KetQuaThaoTac.Loi("Vui lòng nhập mã hiệu biểu mẫu.");
        if (mau.Ten.Length == 0) return KetQuaThaoTac.Loi("Vui lòng nhập tên biểu mẫu.");

        if (await _db.BieuMaus.AnyAsync(b => b.MaHieu == mau.MaHieu && b.Id != mau.Id, ct))
            return KetQuaThaoTac.Loi($"Mã hiệu \"{mau.MaHieu}\" đã tồn tại.");

        var loi = ChuanHoaTruong(mau);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);
        if (mau.BoCuc != BoCucBieuMau.Checklist) mau.HangMuc.Clear();

        if (mau.Id == 0)
        {
            _db.BieuMaus.Add(mau);
        }
        else
        {
            var goc = await QueryDayDu().FirstOrDefaultAsync(b => b.Id == mau.Id, ct);
            if (goc is null) return KetQuaThaoTac.Loi("Không tìm thấy biểu mẫu.");
            goc.MaHieu = mau.MaHieu;
            goc.Ten = mau.Ten;
            goc.BoCuc = mau.BoCuc;
            goc.NgayBanHanh = mau.NgayBanHanh;
            goc.LanBanHanh = mau.LanBanHanh;
            goc.TanSuat = mau.TanSuat;
            goc.NhomQuyen = mau.NhomQuyen;
            goc.GhiChuChan = mau.GhiChuChan;
            goc.KichHoat = mau.KichHoat;
            goc.MotPhieuMoiNgay = mau.MotPhieuMoiNgay;
            goc.ThuTu = mau.ThuTu;
            // Trường: thay toàn bộ (dữ liệu phiếu bám theo khóa Ma nên không ảnh hưởng).
            _db.TruongBieuMaus.RemoveRange(goc.Truong);
            goc.Truong = mau.Truong;
            // Hạng mục checklist: GIỮ Id khi sửa (upsert) để phiếu cũ không mồ côi tên hạng mục.
            CapNhatHangMuc(goc, mau.HangMuc);
        }

        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã lưu biểu mẫu \"{mau.Ten}\".");
    }

    /// <summary>Upsert hạng mục theo Id: giữ hạng mục cũ (phiếu cũ còn liên kết), cập nhật nội dung, thêm mới,
    /// xoá hạng mục đã bỏ khỏi danh sách.</summary>
    private void CapNhatHangMuc(BieuMauEntity goc, List<HangMucBieuMau> moi)
    {
        var giuId = moi.Where(h => h.Id != 0).Select(h => h.Id).ToHashSet();
        _db.HangMucBieuMaus.RemoveRange(goc.HangMuc.Where(h => !giuId.Contains(h.Id)).ToList());
        var theoId = goc.HangMuc.ToDictionary(h => h.Id);
        foreach (var h in moi)
        {
            if (h.Id != 0 && theoId.TryGetValue(h.Id, out var cu))
            {
                cu.Ten = h.Ten;
                cu.DienGiai = h.DienGiai;
                cu.TanSuat = h.TanSuat;
                cu.ThuTu = h.ThuTu;
            }
            else
            {
                h.Id = 0;
                goc.HangMuc.Add(h);
            }
        }
    }

    /// <summary>Chuẩn hoá + kiểm tra danh sách trường. Trả thông báo lỗi hoặc null.</summary>
    private static string? ChuanHoaTruong(BieuMauEntity mau)
    {
        if (mau.Truong.Count == 0) return "Biểu mẫu phải có ít nhất một trường.";
        var thuTu = 0;
        foreach (var t in mau.Truong)
        {
            t.Id = 0;
            t.Ten = t.Ten?.Trim() ?? "";
            t.Ma = (t.Ma ?? "").Trim();
            if (t.Ten.Length == 0) return "Có trường chưa đặt tên.";
            if (t.Ma.Length == 0) t.Ma = BoKhoaTuTen(t.Ten);
            t.ThuTu = thuTu++;
        }
        var maTrung = mau.Truong.GroupBy(t => t.Ma, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (maTrung is not null) return $"Khoá trường \"{maTrung.Key}\" bị lặp - mỗi trường cần một khoá riêng.";

        thuTu = 0;
        foreach (var h in mau.HangMuc)
        {
            // GIỮ Id (nếu có) để khi sửa mẫu, phiếu cũ không mất liên kết hạng mục.
            h.Ten = h.Ten?.Trim() ?? "";
            if (h.Ten.Length == 0) return "Có hạng mục chưa đặt tên.";
            h.ThuTu = thuTu++;
        }
        return null;
    }

    /// <summary>Sinh khoá không dấu từ tên trường (bỏ dấu, thay khoảng trắng bằng "_").</summary>
    private static string BoKhoaTuTen(string ten)
    {
        var chuan = ten.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in chuan)
        {
            var loai = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (loai == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (c is ' ' or '-' or '_') sb.Append('_');
        }
        var s = sb.ToString().Replace("đ", "d").Trim('_');
        return s.Length == 0 ? "truong" : s;
    }

    public async Task<CaiDatInBieuMau> LayCaiDatInAsync(CancellationToken ct = default)
    {
        var cd = await _db.CaiDatInBieuMaus.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cd is not null) return cd;
        // Chưa cài đặt: lấy tên + địa chỉ cơ sở đã đăng ký.
        var tenant = _db.TenantInfo as Tenant;
        return new CaiDatInBieuMau { TenCongTy = _db.TenantInfo?.Name, DiaChi = tenant?.DiaChi };
    }

    public async Task<KetQuaThaoTac> LuuCaiDatInAsync(CaiDatInBieuMau caiDat, CancellationToken ct = default)
    {
        static string? Gon(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var cd = await _db.CaiDatInBieuMaus.FirstOrDefaultAsync(ct);
        if (cd is null) _db.CaiDatInBieuMaus.Add(cd = new CaiDatInBieuMau());
        cd.TenCongTy = Gon(caiDat.TenCongTy);
        cd.DiaChi = Gon(caiDat.DiaChi);
        cd.LogoDuongDan = Gon(caiDat.LogoDuongDan);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok("Đã lưu thông tin in biểu mẫu.");
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var mau = await _db.BieuMaus.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (mau is null) return KetQuaThaoTac.Loi("Không tìm thấy biểu mẫu.");
        if (await _db.PhieuGhiNhans.AnyAsync(p => p.BieuMauId == id, ct))
            return KetQuaThaoTac.Loi("Biểu mẫu đã có phiếu ghi nhận - không xoá được. Hãy bỏ kích hoạt thay vì xoá.");
        _db.BieuMaus.Remove(mau);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã xoá biểu mẫu \"{mau.Ten}\".");
    }

    public async Task<KetQuaThaoTac> NapMauMacDinhAsync(CancellationToken ct = default)
    {
        var daCo = await _db.BieuMaus.Select(b => b.MaHieu).ToListAsync(ct);
        var daCoSet = daCo.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var them = MauMacDinh().Where(m => !daCoSet.Contains(m.MaHieu)).ToList();
        if (them.Count == 0) return KetQuaThaoTac.Ok("Các biểu mẫu mẫu đã có sẵn, không nạp thêm gì.");
        _db.BieuMaus.AddRange(them);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã nạp {them.Count} biểu mẫu mẫu: {string.Join(", ", them.Select(m => m.MaHieu))}.");
    }

    public async Task<IReadOnlyList<NhacHan>> LayNhacHanAsync(int soNgay, CancellationToken ct = default)
    {
        var homNay = GioVietNam.HomNay;
        var moc = homNay.AddDays(Math.Max(0, soNgay));

        // Chỉ xét mẫu có trường "hạn nhắc".
        var mauCoHan = await _db.BieuMaus.AsNoTracking()
            .Include(b => b.Truong)
            .Where(b => b.Truong.Any(t => t.LaHanNhac))
            .ToListAsync(ct);
        if (mauCoHan.Count == 0) return Array.Empty<NhacHan>();

        var tenMau = mauCoHan.ToDictionary(b => b.Id, b => b.Ten);
        var truongHan = mauCoHan.ToDictionary(b => b.Id, b => b.Truong.Where(t => t.LaHanNhac).ToList());
        // Trường để lấy "nhãn" cho dòng (tên thiết bị...): các trường dòng, không phải đầu phiếu/hạn.
        var truongNhan = mauCoHan.ToDictionary(b => b.Id,
            b => b.Truong.Where(t => !t.LaDauPhieu && !t.LaHanNhac).OrderBy(t => t.ThuTu).ToList());

        var idMau = mauCoHan.Select(b => b.Id).ToHashSet();
        var phieu = await _db.PhieuGhiNhans.AsNoTracking().Include(p => p.Dong)
            .Where(p => idMau.Contains(p.BieuMauId)).ToListAsync(ct);

        var kq = new List<NhacHan>();
        foreach (var p in phieu)
        {
            foreach (var d in p.Dong)
            {
                Dictionary<string, string?> gt;
                try { gt = JsonSerializer.Deserialize<Dictionary<string, string?>>(d.GiaTriJson) ?? new(); }
                catch (JsonException) { continue; }

                foreach (var th in truongHan[p.BieuMauId])
                {
                    if (!gt.TryGetValue(th.Ma, out var v) || !DateOnly.TryParse(v, out var han)) continue;
                    if (han > moc) continue; // chưa tới ngưỡng nhắc

                    var nhan = truongNhan[p.BieuMauId]
                        .Select(t => gt.GetValueOrDefault(t.Ma))
                        .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "(không tên)";
                    kq.Add(new NhacHan(p.BieuMauId, tenMau[p.BieuMauId], p.Id, p.Ngay, nhan!,
                                       th.Ten, han, han.DayNumber - homNay.DayNumber));
                }
            }
        }
        return kq.OrderBy(x => x.Han).ToList();
    }

    // ==================== Dữ liệu biểu mẫu mẫu (nhóm A + B) ====================

    private static TruongBieuMau T(string ma, string ten, KieuTruongBieuMau kieu,
        string? donVi = null, string? chuan = null, string? nhom = null, bool batBuoc = false, bool dauPhieu = false,
        bool hanNhac = false, string? tuyChon = null) =>
        new() { Ma = ma, Ten = ten, Kieu = kieu, DonVi = donVi, GiaTriChuan = chuan, Nhom = nhom,
                BatBuoc = batBuoc, LaDauPhieu = dauPhieu, LaHanNhac = hanNhac, TuyChonCsv = tuyChon };

    private static HangMucBieuMau H(string ten, string? dienGiai = null, string? tanSuat = null) =>
        new() { Ten = ten, DienGiai = dienGiai, TanSuat = tanSuat };

    private static IEnumerable<BieuMauEntity> MauMacDinh()
    {
        // ----- Nhóm A: theo ngày -----
        yield return new BieuMauEntity
        {
            MaHieu = "BM-GMP.08-04", Ten = "Kiểm soát nhiệt độ tủ đông/mát", BoCuc = BoCucBieuMau.TheoNgay,
            TanSuat = "Hàng ngày (sáng & chiều)", NhomQuyen = AppRoles.QuyenSanXuat, ThuTu = 1,
            GhiChuChan = "Kiểm tra buổi sáng (8-9h) và buổi chiều (15-16h).",
            Truong =
            {
                T("khu_vuc", "Khu vực", KieuTruongBieuMau.Text, dauPhieu: true),
                T("nhiet_do_dong_sang", "Nhiệt độ tủ đông", KieuTruongBieuMau.So, "°C", nhom: "Buổi sáng (8-9h)", batBuoc: true),
                T("nhiet_do_mat_sang", "Nhiệt độ tủ mát", KieuTruongBieuMau.So, "°C", nhom: "Buổi sáng (8-9h)"),
                T("tinh_trang_sang", "Tình trạng hoạt động", KieuTruongBieuMau.DatKhongDat, nhom: "Buổi sáng (8-9h)"),
                T("nguoi_kt_sang", "Người kiểm tra", KieuTruongBieuMau.ChonNhanSu, nhom: "Buổi sáng (8-9h)"),
                T("nhiet_do_dong_chieu", "Nhiệt độ tủ đông", KieuTruongBieuMau.So, "°C", nhom: "Buổi chiều (15-16h)"),
                T("nhiet_do_mat_chieu", "Nhiệt độ tủ mát", KieuTruongBieuMau.So, "°C", nhom: "Buổi chiều (15-16h)"),
                T("tinh_trang_chieu", "Tình trạng hoạt động", KieuTruongBieuMau.DatKhongDat, nhom: "Buổi chiều (15-16h)"),
                T("nguoi_kt_chieu", "Người kiểm tra", KieuTruongBieuMau.ChonNhanSu, nhom: "Buổi chiều (15-16h)"),
            }
        };

        yield return new BieuMauEntity
        {
            MaHieu = "BM-HD-SX-01-01", NgayBanHanh = new DateOnly(2026, 7, 1), LanBanHanh = "01", Ten = "Nhật ký vận hành đèn UV", BoCuc = BoCucBieuMau.TheoNgay,
            TanSuat = "Hàng ngày", NhomQuyen = AppRoles.QuyenSanXuat, ThuTu = 2,
            GhiChuChan = "Bật đèn 1h sau khi vệ sinh phòng; đóng kín cửa, không còn người. Đèn \"Đạt\" khi bóng sáng bình thường, không nhấp nháy, không nứt.",
            Truong =
            {
                T("khu_vuc", "Khu vực", KieuTruongBieuMau.Text, dauPhieu: true),
                T("gio_bat", "Giờ bật", KieuTruongBieuMau.Gio, batBuoc: true),
                T("gio_tat", "Giờ tắt", KieuTruongBieuMau.Gio),
                T("den_hoat_dong", "Đèn hoạt động", KieuTruongBieuMau.DatKhongDat),
                T("ve_sinh_den", "Tình trạng vệ sinh đèn", KieuTruongBieuMau.DatKhongDat),
                T("nguoi_van_hanh", "Người vận hành", KieuTruongBieuMau.ChonNhanSu),
                T("ghi_chu", "Ghi chú", KieuTruongBieuMau.Text),
            }
        };

        yield return new BieuMauEntity
        {
            MaHieu = "BM-GMP-ISO-06-01", NgayBanHanh = new DateOnly(2026, 7, 1), LanBanHanh = "01", Ten = "Giám sát vệ sinh xe chở hàng", BoCuc = BoCucBieuMau.TheoNgay,
            TanSuat = "Trước mỗi lần xếp hàng lên xe", NhomQuyen = AppRoles.QuyenGiaoHang, ThuTu = 3,
            GhiChuChan = "Giới hạn nhiệt độ thùng xe ≤ 28°C. Phát hiện tiêu chí Không đạt phải báo ngay QC/quản lý và ghi vào cột Ghi chú KPH. (Dùng ô Khu vực để ghi biển số xe.)",
            Truong =
            {
                T("bien_so_xe", "Biển số xe", KieuTruongBieuMau.Text, dauPhieu: true),
                T("dong_phuc_lai_xe", "Đồng phục lái xe", KieuTruongBieuMau.DatKhongDat),
                T("ben_ngoai_xe", "Bên ngoài xe", KieuTruongBieuMau.DatKhongDat),
                T("ben_trong_thung", "Bên trong thùng xe", KieuTruongBieuMau.DatKhongDat),
                T("nhiet_do_thung", "Nhiệt độ thùng xe", KieuTruongBieuMau.So, "°C", chuan: "≤ 28"),
                T("con_trung", "Tình trạng côn trùng", KieuTruongBieuMau.DatKhongDat),
                T("ghi_chu_kph", "Ghi chú KPH / Khắc phục", KieuTruongBieuMau.Text),
                T("nguoi_kt", "Người kiểm tra", KieuTruongBieuMau.ChonNhanSu),
            }
        };

        // ----- Nhóm B: checklist -----
        var checklist = new BieuMauEntity
        {
            MaHieu = "BM-KT.KCS-01", Ten = "Check list vệ sinh hàng ngày", BoCuc = BoCucBieuMau.Checklist,
            TanSuat = "Hàng ngày (đầu ca & cuối ca)", NhomQuyen = AppRoles.QuyenSanXuat, ThuTu = 4,
            GhiChuChan = "Đạt ghi X, Không đạt ghi O.",
            Truong =
            {
                T("khu_vuc", "Khu vực", KieuTruongBieuMau.Text, dauPhieu: true),
                T("dau_ca", "Đầu ca", KieuTruongBieuMau.DatKhongDat),
                T("cuoi_ca", "Cuối ca", KieuTruongBieuMau.DatKhongDat),
                T("ghi_chu", "Ghi chú", KieuTruongBieuMau.Text),
            }
        };
        foreach (var h in HangMucVeSinh()) checklist.HangMuc.Add(h);
        yield return checklist;

        // ----- Nhóm C: phiếu nhiều dòng tự do -----
        yield return new BieuMauEntity
        {
            MaHieu = "BM-GMP-ISO-01-01", Ten = "Kiểm tra, tiếp nhận nguyên liệu NCC", BoCuc = BoCucBieuMau.NhieuDongTuDo,
            TanSuat = "Mỗi lần nhập", NhomQuyen = AppRoles.QuyenNhapLieu, ThuTu = 5,
            GhiChuChan = "Cảm quan: Đạt / Không đạt. Bộ phận: Kho nguyên liệu.",
            Truong =
            {
                T("bo_phan", "Bộ phận", KieuTruongBieuMau.Text, dauPhieu: true),
                T("loai_nguyen_lieu", "Loại nguyên liệu", KieuTruongBieuMau.Text, batBuoc: true),
                T("ten_ncc", "Cơ sở cung cấp", KieuTruongBieuMau.ChonNcc),
                T("so_luong", "Số lượng", KieuTruongBieuMau.Text),
                T("tinh_trang_xe", "Tình trạng xe (vệ sinh/nhiệt độ)", KieuTruongBieuMau.Text),
                T("cam_quan", "Cảm quan chất lượng", KieuTruongBieuMau.DatKhongDat),
                T("nsx", "Ngày sản xuất", KieuTruongBieuMau.Ngay),
                T("hsd", "Hạn sử dụng", KieuTruongBieuMau.Ngay),
                T("nguoi_kt", "Người kiểm tra", KieuTruongBieuMau.ChonNhanSu),
                T("ghi_chu", "Ghi chú", KieuTruongBieuMau.Text),
            }
        };

        yield return new BieuMauEntity
        {
            MaHieu = "BMKS-01", Ten = "Kiểm soát công đoạn nướng bánh", BoCuc = BoCucBieuMau.NhieuDongTuDo,
            TanSuat = "Mỗi mẻ", NhomQuyen = AppRoles.QuyenSanXuat, ThuTu = 6,
            GhiChuChan = "Ghi giờ vào/ra lò và ký xác nhận cho từng sản phẩm.",
            Truong =
            {
                T("to", "Tổ", KieuTruongBieuMau.Text, dauPhieu: true),
                T("san_pham", "Sản phẩm", KieuTruongBieuMau.ChonSanPham, batBuoc: true),
                T("so_luong", "Số lượng", KieuTruongBieuMau.So),
                T("thoi_gian_nuong", "Thời gian nướng", KieuTruongBieuMau.Text, donVi: "phút"),
                T("nhiet_do", "Nhiệt độ", KieuTruongBieuMau.So, donVi: "°C"),
                T("gio_vao", "Giờ vào lò", KieuTruongBieuMau.Gio),
                T("gio_ra", "Giờ ra lò", KieuTruongBieuMau.Gio),
                T("chat_luong", "Chất lượng thành phẩm", KieuTruongBieuMau.DatKhongDat),
                T("nguoi", "Người thực hiện", KieuTruongBieuMau.ChonNhanSu),
                T("ghi_chu", "Ghi chú", KieuTruongBieuMau.Text),
            }
        };

        yield return new BieuMauEntity
        {
            MaHieu = "BM-KPH-01", Ten = "Theo dõi sản phẩm không phù hợp (KPH)", BoCuc = BoCucBieuMau.NhieuDongTuDo,
            TanSuat = "Khi phát sinh", NhomQuyen = AppRoles.QuyenSanXuat, ThuTu = 7, MotPhieuMoiNgay = false,
            GhiChuChan = "Ghi nhận sản phẩm/bán thành phẩm không đạt và cách xử lý; báo QC/quản lý.",
            Truong =
            {
                T("lan_bh", "Lần ban hành", KieuTruongBieuMau.Text, dauPhieu: true),
                T("bo_phan", "Bộ phận", KieuTruongBieuMau.Text, dauPhieu: true),
                T("san_pham", "Sản phẩm", KieuTruongBieuMau.ChonSanPham),
                T("mo_ta", "Mô tả sự không phù hợp", KieuTruongBieuMau.Text, batBuoc: true),
                T("so_luong", "Số lượng", KieuTruongBieuMau.So),
                T("nguyen_nhan", "Nguyên nhân", KieuTruongBieuMau.Text),
                T("huong_xu_ly", "Hướng xử lý", KieuTruongBieuMau.Text),
                T("nguoi", "Người ghi nhận", KieuTruongBieuMau.ChonNhanSu),
            }
        };

        // Danh sách mã bẫy/vị trí là gợi ý ban đầu - cơ sở sửa theo sơ đồ đặt bẫy thực tế trong màn định nghĩa mẫu.
        yield return new BieuMauEntity
        {
            MaHieu = "BM-GMP-ISO-02-04", NgayBanHanh = new DateOnly(2026, 7, 1), LanBanHanh = "02", Ten = "Kiểm tra và giám sát bẫy côn trùng, động vật gây hại",
            BoCuc = BoCucBieuMau.NhieuDongTuDo, TanSuat = "Bẫy chuột 2-3 lần/tuần; đèn côn trùng cuối ngày làm việc",
            NhomQuyen = AppRoles.QuyenSanXuat, ThuTu = 10,
            GhiChuChan = "Động vật gây hại (chuột…): tuần đặt 2-3 lần. Côn trùng (muỗi, ruồi…): bật đèn khi kết thúc ngày làm việc.",
            Truong =
            {
                T("ma_bay", "Mã số bẫy", KieuTruongBieuMau.LuaChon, batBuoc: true,
                  tuyChon: "B01, B02, B03, B04, B05, B06, B07, B08, B09, B10, B11"),
                T("vi_tri", "Vị trí đặt bẫy", KieuTruongBieuMau.LuaChon,
                  tuyChon: "Cửa ra vào, Kho nguyên liệu, Khu sản xuất, Khu đóng gói, Khu rửa, Khu tập kết rác, Nhà vệ sinh"),
                T("loai_bay", "Loại bẫy", KieuTruongBieuMau.LuaChon,
                  tuyChon: "Bẫy chuột, Bẫy keo dính, Đèn diệt côn trùng, Khác"),
                T("tinh_trang", "Tình trạng", KieuTruongBieuMau.LuaChon,
                  tuyChon: "Bình thường, Hư hỏng, Cần thay keo/vệ sinh, Mất bẫy"),
                T("so_luong", "Số lượng bắt được", KieuTruongBieuMau.So, donVi: "con"),
                T("xu_ly", "Biện pháp xử lý/ghi chú", KieuTruongBieuMau.Text),
                T("nguoi_kt", "Người kiểm tra", KieuTruongBieuMau.ChonNhanSu),
            }
        };

        // ----- Nhóm D: danh mục + sổ theo dõi định kỳ (nhắc hạn) -----
        yield return new BieuMauEntity
        {
            MaHieu = "BM-TB-01", Ten = "Danh mục thiết bị đo & hiệu chuẩn", BoCuc = BoCucBieuMau.NhieuDongTuDo,
            TanSuat = "Rà soát định kỳ; nhắc theo hạn hiệu chuẩn", NhomQuyen = AppRoles.QuyenNhapLieu, ThuTu = 8,
            MotPhieuMoiNgay = false,
            GhiChuChan = "Mỗi thiết bị một dòng. Hệ thống nhắc khi đến/quá hạn hiệu chuẩn kế tiếp.",
            Truong =
            {
                T("ten_thiet_bi", "Tên thiết bị", KieuTruongBieuMau.Text, batBuoc: true),
                T("ma_thiet_bi", "Mã thiết bị", KieuTruongBieuMau.Text),
                T("vi_tri", "Vị trí/khu vực", KieuTruongBieuMau.Text),
                T("ngay_hieu_chuan", "Ngày hiệu chuẩn gần nhất", KieuTruongBieuMau.Ngay),
                T("chu_ky_thang", "Chu kỳ (tháng)", KieuTruongBieuMau.So, donVi: "tháng"),
                T("han_ke_tiep", "Hạn hiệu chuẩn kế tiếp", KieuTruongBieuMau.Ngay, hanNhac: true),
                T("ket_qua", "Kết quả", KieuTruongBieuMau.LuaChon, tuyChon: "Đạt, Không đạt, Chờ hiệu chuẩn"),
                T("ghi_chu", "Ghi chú", KieuTruongBieuMau.Text),
            }
        };

        yield return new BieuMauEntity
        {
            MaHieu = "BM-BD-01", Ten = "Sổ theo dõi bảo dưỡng thiết bị", BoCuc = BoCucBieuMau.NhieuDongTuDo,
            TanSuat = "Khi bảo dưỡng; nhắc theo hạn bảo dưỡng kế tiếp", NhomQuyen = AppRoles.QuyenNhapLieu, ThuTu = 9,
            MotPhieuMoiNgay = false,
            GhiChuChan = "Ghi mỗi lần bảo dưỡng. Hệ thống nhắc khi đến/quá hạn bảo dưỡng kế tiếp.",
            Truong =
            {
                T("thiet_bi", "Thiết bị", KieuTruongBieuMau.Text, batBuoc: true),
                T("noi_dung", "Nội dung bảo dưỡng", KieuTruongBieuMau.Text, batBuoc: true),
                T("ngay_bao_duong", "Ngày bảo dưỡng", KieuTruongBieuMau.Ngay),
                T("han_ke_tiep", "Hạn bảo dưỡng kế tiếp", KieuTruongBieuMau.Ngay, hanNhac: true),
                T("nguoi", "Người thực hiện", KieuTruongBieuMau.ChonNhanSu),
                T("ghi_chu", "Ghi chú", KieuTruongBieuMau.Text),
            }
        };
    }

    private static IEnumerable<HangMucBieuMau> HangMucVeSinh()
    {
        yield return H("Đèn chiếu sáng khu sản xuất", "Đèn hoạt động tốt, sạch sẽ.", "Tuần 1 lần (thứ 7)");
        yield return H("Quạt", "Lau chùi, bảo dưỡng định kỳ, không kẹt cốt.", "Tuần 1 lần (thứ 7)");
        yield return H("Máy điều hòa", "Vệ sinh sạch, nhiệt độ chuẩn 17-19°C.", "Tuần 1 lần (thứ 3)");
        yield return H("Đèn bắt côn trùng", "Đèn còn hoạt động, sạch, không có côn trùng/mạng nhện.", "Tuần 1 lần (thứ 7)");
        yield return H("Trần, tường nhà", "Sạch, không tạp chất (mạng nhện, dầu mỡ).", "Hàng ngày (cuối ngày)");
        yield return H("Sàn nhà", "Sạch, không rác/bao bì dưới sàn.", "Hàng ngày");
        yield return H("Thùng rác", "Sạch, có túi lồng, không đầy, luôn đậy nắp.", "Hàng ngày");
        yield return H("Kho ủ bánh", "Sạch trong/ngoài, không đọng nước, nhiệt độ chuẩn ~37°C.", "Hàng ngày");
        yield return H("Kho đông - Kho lạnh", "Sạch, nguyên liệu để gọn đúng vị trí, ghi đủ thông tin/HSD, cách sàn ≥20cm.", "Hàng ngày");
        yield return H("Lò nướng bánh", "Trong/ngoài lò sạch, không tạp chất bám.", "Hàng ngày (cuối ngày)");
        yield return H("Bếp chiên nhúng", "Thay dầu khi đổi màu, lau sạch dầu mỡ, đậy nắp.", "Hàng ngày");
        yield return H("Bếp đun nấu", "Lau sạch dầu mỡ, tạp chất quanh bếp.", "Hàng ngày");
        yield return H("Máy vê Baguette", "Sạch trước khi dùng, không tạp chất ngoài bột mỳ.", "Hàng ngày");
        yield return H("Máy chia bột, cán bột", "Sạch trước khi dùng, không tạp chất ngoài bột mỳ.", "Hàng ngày");
        yield return H("Máy đánh bột, máy đánh kem", "Sạch trước khi dùng, không tạp chất ngoài bột mỳ.", "Hàng ngày");
        yield return H("Dao, kéo, thớt, đũa, rổ, xoong, chậu, dụng cụ nhỏ", "Sạch, sắp xếp gọn đúng vị trí.", "Hàng ngày");
        yield return H("Cân đồng hồ (các loại)", "Không để quá tải, vệ sinh sạch sau khi cân.", "Hàng ngày");
        yield return H("Các loại hộp đựng NVL", "Sạch, dán tên NCC/NVL/HSD, đậy nắp sau khi dùng.", "Hàng ngày");
        yield return H("Bàn sản xuất, đóng gói", "Luôn sạch, không tạp chất trên bàn.", "Hàng ngày");
        yield return H("Kệ, nguyên liệu trong sản xuất", "NVL để 100% trên kệ, kệ cách sàn ≥20cm, đủ thông tin/HSD.", "Hàng ngày");
    }
}
