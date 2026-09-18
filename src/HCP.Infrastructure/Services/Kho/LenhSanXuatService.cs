using HCP.Domain;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.Kho;

/// <inheritdoc cref="ILenhSanXuatService"/>
public sealed class LenhSanXuatService : ILenhSanXuatService
{
    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;
    private readonly IMaTuSinhService _maTuSinh;

    public LenhSanXuatService(AppDbContext db, ISyncOutboxWriter outbox, IMaTuSinhService maTuSinh)
    {
        _db = db;
        _outbox = outbox;
        _maTuSinh = maTuSinh;
    }

    public async Task<IReadOnlyList<LenhSanXuat>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.LenhSanXuats.AsNoTracking()
            .Include(l => l.SanPham).ThenInclude(s => s.Khau)
            .Include(l => l.ThamGia)
            .OrderByDescending(l => l.NgaySanXuat).ThenByDescending(l => l.Id)
            .ToListAsync(ct);

    public Task<LenhSanXuat?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        QueryDayDu().FirstOrDefaultAsync(l => l.Id == id, ct);

    private IQueryable<LenhSanXuat> QueryDayDu() =>
        _db.LenhSanXuats
            .Include(l => l.SanPham).ThenInclude(s => s.Khau)
            .Include(l => l.SanPham).ThenInclude(s => s.TieuHao)
            .Include(l => l.SanPham).ThenInclude(s => s.Anh)
            .Include(l => l.ThamGia);

    // ==================== Xem trước nguyên liệu ====================

    public async Task<IReadOnlyList<NguyenLieuCanDto>> TinhNguyenLieuCanAsync(
        IReadOnlyList<(string MaThanhPham, decimal SoLuong)> dong, string maKho, CancellationToken ct = default)
    {
        var can = await GopNhuCauAsync(dong, ct);
        if (can.Count == 0) return Array.Empty<NguyenLieuCanDto>();

        var sanPham = await _db.Products.AsNoTracking().ToListAsync(ct);
        var theoMa = sanPham.GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First());

        var ket = new List<NguyenLieuCanDto>();
        foreach (var (maNl, luong) in can.OrderBy(x => x.Key))
        {
            var ton = await TonAsync(maNl, maKho, ct);
            var p = theoMa.GetValueOrDefault(maNl);
            ket.Add(new NguyenLieuCanDto(maNl, p?.TenSanPham ?? maNl, p?.DonViTinh, luong, ton, ton >= luong));
        }
        return ket;
    }

    /// <summary>Nhu cầu nguyên liệu của RIÊNG từng dòng sản phẩm, theo thứ tự dòng truyền vào.</summary>
    private async Task<List<Dictionary<string, decimal>>> NhuCauTungDongAsync(
        IReadOnlyList<(string MaThanhPham, decimal SoLuong)> dong, CancellationToken ct)
    {
        var ket = new List<Dictionary<string, decimal>>();
        foreach (var d in dong)
        {
            var can = new Dictionary<string, decimal>();
            ket.Add(can);
            if (string.IsNullOrWhiteSpace(d.MaThanhPham) || d.SoLuong <= 0) continue;
            var tp = await _db.Products.AsNoTracking().Include(p => p.DanhSachDinhMuc)
                .FirstOrDefaultAsync(p => p.MaSanPham == d.MaThanhPham, ct);
            if (tp is null) continue;
            foreach (var dm in tp.DanhSachDinhMuc)
                can[dm.MaNguyenLieu] = can.GetValueOrDefault(dm.MaNguyenLieu) + NhuCau(dm, d.SoLuong);
        }
        return ket;
    }

    /// <summary>
    /// Cộng dồn nhu cầu nguyên liệu của mọi dòng sản phẩm. PHẢI cộng dồn trước khi so tồn:
    /// hai dòng cùng dùng bột mì mà so riêng lẻ thì mỗi dòng đều "đủ" nhưng tổng lại thiếu.
    /// </summary>
    private async Task<Dictionary<string, decimal>> GopNhuCauAsync(
        IReadOnlyList<(string MaThanhPham, decimal SoLuong)> dong, CancellationToken ct)
    {
        var tong = new Dictionary<string, decimal>();
        foreach (var can in await NhuCauTungDongAsync(dong, ct))
            foreach (var (ma, luong) in can)
                tong[ma] = tong.GetValueOrDefault(ma) + luong;
        return tong;
    }

    // ==================== Tạo / sửa ====================

    public async Task<KetQuaThaoTac> TaoAsync(LenhSanXuat lenh, CancellationToken ct = default)
    {
        // Mã lệnh và mã lô do hệ thống cấp: bỏ qua mọi mã gửi lên.
        foreach (var sp in lenh.SanPham) sp.MaLoThanhPham = "";
        if (lenh.NgaySanXuat == default) lenh.NgaySanXuat = MaTuSinhService.HomNay;

        var loi = await KiemTraAsync(lenh, maLoDuocGiu: null, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        // Sinh SAU khi dữ liệu hợp lệ (không phí số) và TRƯỚC khi Add (hàm sinh tự SaveChanges bộ đếm).
        lenh.MaLenh = await _maTuSinh.SinhAsync(LoaiMaTuSinh.LenhSanXuat, lenh.NgaySanXuat, ct);
        await GanMaLoChoDongMoiAsync(lenh, ct);

        lenh.TrangThai = TrangThaiLenhSX.MoiTao;
        _db.LenhSanXuats.Add(lenh);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã tạo lệnh sản xuất \"{lenh.MaLenh}\" với {lenh.SanPham.Count} sản phẩm. "
                                + "Bấm \"Hoàn thành\" để trừ nguyên liệu.");
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(LenhSanXuat lenh, CancellationToken ct = default)
    {
        var goc = await QueryDayDu().FirstOrDefaultAsync(l => l.Id == lenh.Id, ct);
        if (goc is null) return KetQuaThaoTac.Loi("Không tìm thấy lệnh sản xuất.");
        // Kiểm tra trên bản trong DB chứ không tin trạng thái màn hình gửi lên: lệnh có thể vừa
        // được hoàn thành ở tab khác - khi đó kho đã trừ, sửa số lượng sẽ lệch sổ kho.
        if (goc.TrangThai != TrangThaiLenhSX.MoiTao)
            return KetQuaThaoTac.Loi("Chỉ sửa được lệnh chưa hoàn thành. Lệnh đã hoàn thành thì dùng \"Huỷ lệnh\" rồi tạo lệnh mới.");

        // Dòng cũ phải giữ đúng mã lô đã cấp; dòng mới để trống và được cấp mã khi lưu.
        var maLoCu = goc.SanPham.Select(s => s.MaLoThanhPham)
            .ToDictionary(m => m, m => m, StringComparer.OrdinalIgnoreCase);
        var loi = await KiemTraAsync(lenh, maLoCu, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        await GanMaLoChoDongMoiAsync(lenh, ct);

        // Mã lệnh KHÔNG đổi.
        goc.MaKho = lenh.MaKho;
        goc.NgaySanXuat = lenh.NgaySanXuat;
        goc.GhiChu = lenh.GhiChu;
        goc.TaoLoDongBo = lenh.TaoLoDongBo;

        // Lệnh chưa hoàn thành nên chưa có tiêu hao/ảnh: thay nguyên danh sách sản phẩm cho gọn.
        _db.LenhSanXuatSanPhams.RemoveRange(goc.SanPham);
        goc.SanPham = lenh.SanPham;

        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã cập nhật lệnh sản xuất \"{goc.MaLenh}\".");
    }

    /// <summary>
    /// Chuẩn hoá + kiểm tra dữ liệu lệnh (dùng chung cho tạo và sửa). Trả về thông báo lỗi, hoặc null nếu hợp lệ.
    /// <paramref name="maLoDuocGiu"/>: mã lô đã cấp của lệnh đang sửa (null khi tạo mới) - dòng nào mang mã
    /// thì mã đó phải thuộc tập này, vì mã lô do hệ thống sinh và không sửa được.
    /// </summary>
    private async Task<string?> KiemTraAsync(LenhSanXuat lenh, IReadOnlyDictionary<string, string>? maLoDuocGiu,
                                             CancellationToken ct)
    {
        lenh.MaKho = lenh.MaKho?.Trim() ?? "";
        lenh.GhiChu = string.IsNullOrWhiteSpace(lenh.GhiChu) ? null : lenh.GhiChu.Trim();

        if (!await _db.Warehouses.AnyAsync(k => k.MaKho == lenh.MaKho, ct))
            return "Vui lòng chọn kho hợp lệ.";
        if (lenh.SanPham.Count == 0) return "Lệnh phải có ít nhất một sản phẩm.";

        foreach (var sp in lenh.SanPham)
        {
            var ma = sp.MaLoThanhPham?.Trim() ?? "";
            if (ma.Length == 0) { sp.MaLoThanhPham = ""; continue; }
            if (maLoDuocGiu is null || !maLoDuocGiu.TryGetValue(ma, out var maGoc))
                return $"Mã lô \"{ma}\" không thuộc lệnh này. Mã lô do hệ thống tự sinh, không sửa được.";
            sp.MaLoThanhPham = maGoc;   // giữ đúng cách viết của mã đã cấp
        }

        var maLoTrung = lenh.SanPham.Where(s => s.MaLoThanhPham.Length > 0)
            .GroupBy(s => s.MaLoThanhPham, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (maLoTrung is not null)
            return $"Mã lô \"{maLoTrung.Key}\" bị lặp giữa các sản phẩm trong cùng lệnh.";

        foreach (var sp in lenh.SanPham)
        {
            var loi = await KiemTraSanPhamAsync(sp, ct);
            if (loi is not null) return loi;
        }
        return null;
    }

    /// <summary>Cấp mã lô LO-yyyyMMdd-NNN (theo ngày sản xuất của lệnh) cho các dòng chưa có mã.</summary>
    private async Task GanMaLoChoDongMoiAsync(LenhSanXuat lenh, CancellationToken ct)
    {
        var dongMoi = lenh.SanPham.Where(s => string.IsNullOrEmpty(s.MaLoThanhPham)).ToList();
        if (dongMoi.Count == 0) return;
        var ma = await _maTuSinh.SinhNhieuAsync(LoaiMaTuSinh.LoSanXuat, dongMoi.Count, lenh.NgaySanXuat, ct);
        for (var i = 0; i < dongMoi.Count; i++) dongMoi[i].MaLoThanhPham = ma[i];
    }

    private async Task<string?> KiemTraSanPhamAsync(LenhSanXuatSanPham sp, CancellationToken ct)
    {
        sp.MaThanhPham = sp.MaThanhPham?.Trim() ?? "";
        sp.MaLoThanhPham = sp.MaLoThanhPham?.Trim() ?? "";
        sp.MaQuyTrinh = sp.MaQuyTrinh?.Trim() ?? "";

        var tp = await _db.Products.AsNoTracking().Include(p => p.DanhSachDinhMuc)
            .FirstOrDefaultAsync(p => p.MaSanPham == sp.MaThanhPham, ct);
        if (tp is null || tp.LoaiSanPham != LoaiSanPham.ThanhPham)
            return $"\"{sp.MaThanhPham}\" không phải thành phẩm hợp lệ.";
        if (tp.DanhSachDinhMuc.Count == 0)
            return $"\"{tp.TenSanPham}\" chưa có định mức. Khai định mức trước khi sản xuất.";
        if (sp.SoLuong <= 0) return $"Số lượng của \"{tp.TenSanPham}\" phải lớn hơn 0.";

        // Quy trình và khâu: đây là dữ liệu HanoiCheck cần để truy xuất nguồn gốc.
        if (string.IsNullOrWhiteSpace(sp.MaQuyTrinh))
            return $"Vui lòng chọn quy trình sản xuất cho \"{tp.TenSanPham}\".";
        var quyTrinh = await _db.ProductionProcesses.AsNoTracking().Include(q => q.DanhSachKhau)
            .FirstOrDefaultAsync(q => q.MaQuyTrinh == sp.MaQuyTrinh, ct);
        if (quyTrinh is null) return $"Không tìm thấy quy trình \"{sp.MaQuyTrinh}\".";
        if (quyTrinh.DanhSachKhau.Count == 0)
            return $"Quy trình \"{quyTrinh.TenQuyTrinh}\" chưa khai khâu nào.";
        if (sp.Khau.Count == 0)
            return $"\"{tp.TenSanPham}\": phải khai các khâu của quy trình.";

        var khauCanCo = quyTrinh.DanhSachKhau.Select(k => k.MaKhau).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var khauDaKhai = sp.Khau.Select(k => (k.MaKhau ?? "").Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var thieuKhau = khauCanCo.Except(khauDaKhai, StringComparer.OrdinalIgnoreCase).ToList();
        if (thieuKhau.Count > 0)
            return $"\"{tp.TenSanPham}\": còn khâu của quy trình chưa khai ({string.Join(", ", thieuKhau)}).";

        foreach (var k in sp.Khau)
        {
            var loi = await KiemTraKhauAsync(k, tp.TenSanPham, ct);
            if (loi is not null) return loi;
        }
        return null;
    }

    private async Task<string?> KiemTraKhauAsync(LenhSanXuatKhau k, string tenThanhPham, CancellationToken ct)
    {
        k.MaKhau = k.MaKhau?.Trim() ?? "";
        k.MaCoSo = k.MaCoSo?.Trim() ?? "";
        k.NguoiThucHienCsv = string.Join(",", (k.NguoiThucHienCsv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        k.GhiChu = string.IsNullOrWhiteSpace(k.GhiChu) ? null : k.GhiChu.Trim();

        if (!await _db.ProductionSteps.AnyAsync(s => s.MaKhau == k.MaKhau, ct))
            return $"\"{tenThanhPham}\": khâu \"{k.MaKhau}\" không có trong danh mục.";
        if (string.IsNullOrWhiteSpace(k.MaCoSo))
            return $"\"{tenThanhPham}\" - khâu \"{k.MaKhau}\": chưa chọn cơ sở thực hiện.";
        if (!await _db.Facilities.AnyAsync(c => c.MaCoSo == k.MaCoSo, ct))
            return $"\"{tenThanhPham}\" - khâu \"{k.MaKhau}\": cơ sở \"{k.MaCoSo}\" không có trong danh mục.";
        // Người thực hiện không bắt buộc lúc lập lệnh: nhân viên sản xuất quét mã QR của lệnh để tự tham gia.
        // Hoàn thành lệnh mới bắt buộc (xem ThucHienAsync).
        var ma = k.NguoiThucHien.ToList();
        var nhanSuCo = await _db.Staff.AsNoTracking()
            .Where(n => ma.Contains(n.MaNhanSu)).Select(n => n.MaNhanSu).ToListAsync(ct);
        var la = ma.Except(nhanSuCo, StringComparer.OrdinalIgnoreCase).ToList();
        if (la.Count > 0)
            return $"\"{tenThanhPham}\" - khâu \"{k.MaKhau}\": không có nhân sự {string.Join(", ", la)}.";
        return null;
    }

    // ==================== Hoàn thành ====================

    public async Task<KetQuaThaoTac> ThucHienAsync(
        int id,
        IReadOnlyList<AnhTheoSanPham> anhTheoSanPham,
        IReadOnlyList<LenhSanXuatKhau>? khauSuaLai = null,
        CancellationToken ct = default)
    {
        var lenh = await QueryDayDu().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lenh is null) return KetQuaThaoTac.Loi("Không tìm thấy lệnh sản xuất.");
        if (lenh.TrangThai == TrangThaiLenhSX.HoanThanh)
            return KetQuaThaoTac.Loi("Lệnh này đã hoàn thành rồi.");
        if (lenh.TrangThai == TrangThaiLenhSX.DaHuy)
            return KetQuaThaoTac.Loi("Lệnh này đã huỷ, không hoàn thành lại được. Hãy tạo lệnh mới.");
        if (lenh.SanPham.Count == 0) return KetQuaThaoTac.Loi("Lệnh không có sản phẩm nào.");

        // Ảnh: MỖI dòng sản phẩm phải có ảnh của chính lô đó. Kiểm TRƯỚC khi động vào kho.
        var anhTheoDong = (anhTheoSanPham ?? Array.Empty<AnhTheoSanPham>())
            .ToDictionary(a => a.SanPhamId,
                          a => a.Anh.Where(x => !string.IsNullOrWhiteSpace(x.DuongDan)).ToList());
        // Giới hạn album ảnh chỉ áp khi lệnh tạo Lô đồng bộ và cơ sở đang bật HanoiCheck.
        var apHnC = lenh.TaoLoDongBo && await _outbox.DangBatAsync(ct);
        foreach (var sp in lenh.SanPham)
        {
            if (!anhTheoDong.TryGetValue(sp.Id, out var anh) || anh.Count == 0)
                return KetQuaThaoTac.Loi($"Cần ít nhất 1 ảnh lô thành phẩm cho \"{sp.MaThanhPham}\" (lô {sp.MaLoThanhPham}).");
            // Ảnh lô là album ảnh của Lô sản xuất gửi HanoiCheck - đặc tả chỉ nhận 1-3 ảnh.
            if (apHnC && anh.Count > LoSanXuatService.SoAnhLoToiDa)
                return KetQuaThaoTac.Loi($"Lô {sp.MaLoThanhPham}: tối đa {LoSanXuatService.SoAnhLoToiDa} ảnh "
                                         + $"(đang chọn {anh.Count}) - giới hạn album ảnh lô của HanoiCheck.");
        }

        // Cho phép sửa lại người thực hiện / cơ sở ngay lúc hoàn thành (ai làm thực tế có thể khác kế hoạch).
        if (khauSuaLai is { Count: > 0 })
        {
            var loiKhau = await ApDungKhauSuaLaiAsync(lenh, khauSuaLai, ct);
            if (loiKhau is not null) return KetQuaThaoTac.Loi(loiKhau);
        }

        // Hoàn thành thì mỗi khâu phải có người thực hiện - dữ liệu truy xuất của lô (và HanoiCheck) cần có.
        var khauThieuNguoi = lenh.SanPham
            .SelectMany(s => s.Khau.Where(k => k.NguoiThucHien.Count == 0).Select(k => $"{s.MaThanhPham} - {k.MaKhau}"))
            .ToList();
        if (khauThieuNguoi.Count > 0)
            return KetQuaThaoTac.Loi($"Còn khâu chưa có người thực hiện: {string.Join("; ", khauThieuNguoi)}. "
                                     + "Nhân viên sản xuất quét mã QR của lệnh để tham gia, hoặc chọn người thực hiện khi hoàn thành.");

        // Nhu cầu nguyên liệu tính riêng từng dòng (để chia tiêu hao) rồi GỘP lại để so tồn.
        var canTungDong = await NhuCauTungDongAsync(
            lenh.SanPham.Select(s => (s.MaThanhPham, s.SoLuong)).ToList(), ct);
        var can = new Dictionary<string, decimal>();
        foreach (var d in canTungDong)
            foreach (var (ma, luong) in d)
                can[ma] = can.GetValueOrDefault(ma) + luong;
        if (can.Count == 0) return KetQuaThaoTac.Loi("Các thành phẩm không còn định mức, không thể thực hiện.");

        var thieu = new List<string>();
        var keHoachTru = new List<(string MaNL, string MaLo, DateOnly? Hsd, decimal SoLuong)>();
        foreach (var (maNl, luong) in can)
        {
            var lots = await LayLoFefoAsync(maNl, lenh.MaKho, ct);
            var tongTon = lots.Sum(x => x.Ton);
            if (tongTon < luong)
            {
                thieu.Add($"{maNl} (cần {luong:0.###}, còn {tongTon:0.###})");
                continue;
            }
            var conCanTru = luong;
            foreach (var lot in lots)
            {
                if (conCanTru <= 0) break;
                var tru = Math.Min(conCanTru, lot.Ton);
                keHoachTru.Add((maNl, lot.MaLo, lot.Hsd, tru));
                conCanTru -= tru;
            }
        }
        if (thieu.Count > 0)
            return KetQuaThaoTac.Loi("Không đủ nguyên liệu: " + string.Join("; ", thieu));

        var now = DateTime.UtcNow;

        // Trừ nguyên liệu (âm) theo kế hoạch FEFO.
        foreach (var t in keHoachTru)
        {
            _db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = t.MaNL, MaKho = lenh.MaKho, MaLo = t.MaLo,
                SoLuong = -t.SoLuong, HanSuDung = t.Hsd,
                Loai = LoaiGiaoDichKho.XuatSanXuat, ChungTu = lenh.MaLenh, ThoiGianUtc = now
            });
        }

        // Ghi tiêu hao về từng dòng sản phẩm theo tỷ lệ nhu cầu - để truy xuất lô nguyên liệu nào
        // đã vào lô thành phẩm nào.
        GanTieuHaoChoTungDong(lenh, canTungDong, keHoachTru, can);

        foreach (var sp in lenh.SanPham)
        {
            // Nhập thành phẩm (dương).
            _db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = sp.MaThanhPham, MaKho = lenh.MaKho, MaLo = sp.MaLoThanhPham,
                SoLuong = sp.SoLuong, HanSuDung = sp.HanSuDung,
                Loai = LoaiGiaoDichKho.NhapThanhPham, ChungTu = lenh.MaLenh, ThoiGianUtc = now
            });

            // Ảnh chứng từ của lô - mã file đánh theo mã lô để khớp với file gửi HanoiCheck.
            var thuTu = 1;
            foreach (var a in anhTheoDong[sp.Id])
            {
                sp.Anh.Add(new LenhSanXuatAnh
                {
                    MaFile = $"{sp.MaLoThanhPham}-A{thuTu++}",
                    TenFile = a.TenFile, DuongDan = a.DuongDan, ThoiGianUtc = now
                });
            }
        }

        lenh.TrangThai = TrangThaiLenhSX.HoanThanh;
        lenh.ThoiGianHoanThanhUtc = now;

        // Tuỳ chọn: sinh Lô sản xuất (Batch) cho TỪNG dòng sản phẩm để đồng bộ HanoiCheck.
        var batches = new List<Batch>();
        if (lenh.TaoLoDongBo)
        {
            foreach (var sp in lenh.SanPham)
            {
                var batch = await DungBatchTuDongAsync(lenh, sp, now, ct);
                if (batch is null) continue;
                _db.Batches.Add(batch);
                sp.MaLoDaTao = batch.MaLo;
                batches.Add(batch);
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var b in batches)
            await _outbox.GuiAsync(b, ct);

        var thongBao = $"Đã sản xuất {lenh.SanPham.Count} sản phẩm "
                       + $"({string.Join(", ", lenh.SanPham.Select(s => $"{s.MaThanhPham} {s.SoLuong:0.###}"))}), "
                       + "trừ nguyên liệu theo định mức.";
        if (batches.Count > 0)
            thongBao += $" Đã tạo {batches.Count} lô và đưa vào hàng đợi đồng bộ HanoiCheck.";
        else if (lenh.TaoLoDongBo)
            thongBao += " (Các lô đồng bộ đã tồn tại nên bỏ qua tạo mới.)";
        return KetQuaThaoTac.Ok(thongBao);
    }

    public Task<LenhSanXuat?> LayTheoMaAsync(string maLenh, CancellationToken ct = default)
    {
        var ma = (maLenh ?? "").Trim();
        return QueryDayDu().FirstOrDefaultAsync(l => l.MaLenh == ma, ct);
    }

    public async Task<KetQuaThaoTac> ThamGiaAsync(int lenhId, string maNhanSu, IReadOnlyCollection<int> khauIds,
                                                  CancellationToken ct = default)
    {
        var lenh = await QueryDayDu().FirstOrDefaultAsync(l => l.Id == lenhId, ct);
        if (lenh is null) return KetQuaThaoTac.Loi("Không tìm thấy lệnh sản xuất.");
        if (lenh.TrangThai != TrangThaiLenhSX.MoiTao)
            return KetQuaThaoTac.Loi($"Lệnh {lenh.MaLenh} đã "
                                     + (lenh.TrangThai == TrangThaiLenhSX.HoanThanh ? "hoàn thành" : "huỷ")
                                     + ", không tham gia được nữa.");

        var nhanSu = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(n => n.MaNhanSu == maNhanSu, ct);
        if (nhanSu is null) return KetQuaThaoTac.Loi("Tài khoản chưa gắn với hồ sơ nhân sự của cơ sở.");
        if (!nhanSu.TrangThai) return KetQuaThaoTac.Loi("Hồ sơ nhân sự của bạn đang ở trạng thái nghỉ.");

        var tatCaKhau = lenh.SanPham.SelectMany(s => s.Khau).ToList();
        var chon = khauIds.ToHashSet();
        if (chon.Any(id => tatCaKhau.All(k => k.Id != id)))
            return KetQuaThaoTac.Loi("Có khâu không thuộc lệnh này.");

        // Gắn / gỡ đúng người này ở từng khâu; người đã được giao sẵn giữ nguyên thứ tự.
        foreach (var k in tatCaKhau)
        {
            var coMat = k.NguoiThucHien.Contains(nhanSu.MaNhanSu, StringComparer.OrdinalIgnoreCase);
            if (chon.Contains(k.Id) == coMat) continue;
            var ds = k.NguoiThucHien.Where(m => !string.Equals(m, nhanSu.MaNhanSu, StringComparison.OrdinalIgnoreCase)).ToList();
            if (chon.Contains(k.Id)) ds.Add(nhanSu.MaNhanSu);
            k.NguoiThucHienCsv = string.Join(",", ds);
        }

        var banGhi = lenh.ThamGia.FirstOrDefault(t => string.Equals(t.MaNhanSu, nhanSu.MaNhanSu, StringComparison.OrdinalIgnoreCase));
        if (chon.Count == 0)
        {
            if (banGhi is not null) _db.LenhSanXuatThamGias.Remove(banGhi);
        }
        else if (banGhi is null)
        {
            lenh.ThamGia.Add(new LenhSanXuatThamGia
            {
                MaNhanSu = nhanSu.MaNhanSu, HoTen = nhanSu.HoTen, ThoiGianUtc = DateTime.UtcNow
            });
        }
        else
        {
            banGhi.HoTen = nhanSu.HoTen;   // giữ thời điểm tham gia lần đầu
        }

        await _db.SaveChangesAsync(ct);
        return chon.Count == 0
            ? KetQuaThaoTac.Ok($"Đã rời lệnh {lenh.MaLenh}.")
            : KetQuaThaoTac.Ok($"Đã tham gia lệnh {lenh.MaLenh}: {chon.Count}/{tatCaKhau.Count} khâu.");
    }

    /// <summary>Cập nhật người thực hiện / cơ sở của khâu ngay trước khi chốt lệnh.</summary>
    private async Task<string?> ApDungKhauSuaLaiAsync(
        LenhSanXuat lenh, IReadOnlyList<LenhSanXuatKhau> suaLai, CancellationToken ct)
    {
        foreach (var moi in suaLai)
        {
            var sp = lenh.SanPham.FirstOrDefault(s => s.Khau.Any(k => k.Id == moi.Id));
            var khau = sp?.Khau.FirstOrDefault(k => k.Id == moi.Id);
            if (sp is null || khau is null) continue;   // khâu không thuộc lệnh này thì bỏ qua

            var ban = new LenhSanXuatKhau
            {
                MaKhau = khau.MaKhau, MaCoSo = moi.MaCoSo,
                NguoiThucHienCsv = moi.NguoiThucHienCsv, GhiChu = moi.GhiChu
            };
            var loi = await KiemTraKhauAsync(ban, sp.MaThanhPham, ct);
            if (loi is not null) return loi;

            khau.MaCoSo = ban.MaCoSo;
            khau.NguoiThucHienCsv = ban.NguoiThucHienCsv;
            khau.GhiChu = ban.GhiChu;
        }
        return null;
    }

    /// <summary>
    /// Chia lượng nguyên liệu đã trừ về từng dòng sản phẩm theo tỷ lệ nhu cầu của dòng đó.
    /// Cần thiết vì kho trừ gộp (FEFO trên tổng nhu cầu) nhưng truy xuất lại theo từng lô thành phẩm.
    /// </summary>
    private static void GanTieuHaoChoTungDong(
        LenhSanXuat lenh,
        List<Dictionary<string, decimal>> canTungDong,
        List<(string MaNL, string MaLo, DateOnly? Hsd, decimal SoLuong)> keHoachTru,
        Dictionary<string, decimal> tongCan)
    {
        foreach (var t in keHoachTru)
        {
            var tong = tongCan.GetValueOrDefault(t.MaNL);
            if (tong <= 0) continue;

            for (var i = 0; i < lenh.SanPham.Count; i++)
            {
                var canCuaDong = canTungDong[i].GetValueOrDefault(t.MaNL);
                if (canCuaDong <= 0) continue;
                var phan = Math.Round(t.SoLuong * canCuaDong / tong, 4, MidpointRounding.AwayFromZero);
                if (phan <= 0) continue;
                lenh.SanPham[i].TieuHao.Add(new LenhSanXuatTieuHao
                {
                    MaNguyenLieu = t.MaNL, MaLo = t.MaLo, SoLuong = phan
                });
            }
        }
    }

    // ==================== Dựng Batch cho HanoiCheck ====================

    /// <summary>
    /// Dựng Batch từ một dòng sản phẩm: lô thành phẩm + kho + các khâu đã khai (kèm người thực hiện,
    /// địa chỉ cơ sở) + ảnh chứng từ. Trả null nếu mã lô này đã có Batch (tránh trùng khoá nghiệp vụ).
    /// </summary>
    private async Task<Batch?> DungBatchTuDongAsync(
        LenhSanXuat lenh, LenhSanXuatSanPham sp, DateTime now, CancellationToken ct)
    {
        if (await _db.Batches.AnyAsync(b => b.MaLo == sp.MaLoThanhPham, ct)) return null;

        var coSo = await _db.Facilities.AsNoTracking().ToListAsync(ct);
        var diaChiCoSo = coSo.GroupBy(c => c.MaCoSo).ToDictionary(g => g.Key, g => g.First().DiaChi);
        var khauSapXep = sp.Khau.OrderBy(k => k.ThuTu).ToList();

        var moTaNL = string.Join("; ", sp.TieuHao.Select(t => $"{t.MaLo}×{t.SoLuong:0.###}"));
        var batch = new Batch
        {
            MaSanPham = sp.MaThanhPham,
            MaLo = sp.MaLoThanhPham,
            TenLo = $"Lô SX {lenh.MaLenh} - {sp.MaThanhPham}",
            NgayNhap = lenh.NgaySanXuat,
            NgaySanXuat = lenh.NgaySanXuat,
            HanSuDung = sp.HanSuDung,
            // Cơ sở của lô lấy theo khâu đầu tiên - HanoiCheck chỉ nhận một mã cơ sở cho cả lô.
            MaCoSo = khauSapXep.FirstOrDefault()?.MaCoSo,
            GhiChu = $"Tự động từ lệnh sản xuất {lenh.MaLenh}."
                     + (moTaNL.Length > 0 ? $" Nguyên liệu tiêu hao: {moTaNL}." : ""),
            DanhSachKho = new() { new BatchWarehouse { MaKho = lenh.MaKho } }
        };

        // Mỗi khâu của quy trình thành một bước sản xuất, mang theo người thực hiện.
        var thuTu = 1;
        foreach (var k in khauSapXep)
        {
            batch.DanhSachKhau.Add(new BatchStep
            {
                MaBuocSx = $"{sp.MaLoThanhPham}-B{thuTu}",
                MaKhau = k.MaKhau,
                ThuTu = thuTu,
                // Thời gian khâu lưu theo giờ Việt Nam: màn Lô sản xuất và HanoiCheck (thoi_gian) đều hiểu là giờ VN.
                ThoiGian = GioVietNam.TuUtc(now),
                MaLoSanXuat = sp.MaLoThanhPham,
                // Khâu đầu gắn lô nguyên liệu đã tiêu hao để truy xuất ngược về đầu vào.
                MaLoNguyenLieu = thuTu == 1 ? string.Join(", ", sp.TieuHao.Select(t => t.MaLo).Distinct()) : null,
                NguoiThucHienCsv = k.NguoiThucHienCsv,
                DiaChi = diaChiCoSo.GetValueOrDefault(k.MaCoSo),
                GhiChu = k.GhiChu
            });
            thuTu++;
        }

        foreach (var anh in sp.Anh)
        {
            batch.DanhSachFile.Add(new BatchFile
            {
                MaFile = anh.MaFile,
                TenFile = anh.TenFile,
                DuongDan = anh.DuongDan,
                Loai = "HINH_ANH",
                MaKhau = khauSapXep.FirstOrDefault()?.MaKhau
            });
        }

        return batch;
    }

    // ==================== Xoá / huỷ ====================

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        // Nạp kèm dòng sản phẩm + khâu để EF xoá lan cả khi CSDL không tự cascade.
        var lenh = await QueryDayDu().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lenh is null) return KetQuaThaoTac.Loi("Không tìm thấy lệnh sản xuất.");
        if (lenh.TrangThai == TrangThaiLenhSX.HoanThanh)
            return KetQuaThaoTac.Loi(
                "Không xoá được lệnh đã thực hiện (đã phát sinh giao dịch kho). Dùng \"Huỷ lệnh\" "
                + "để hệ thống ghi bút toán đảo trả lại tồn kho.");
        if (lenh.TrangThai == TrangThaiLenhSX.DaHuy)
            return KetQuaThaoTac.Loi(
                "Không xoá được lệnh đã huỷ - lệnh và bút toán đảo được giữ lại để truy xuất.");

        _db.LenhSanXuats.Remove(lenh);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã xoá lệnh \"{lenh.MaLenh}\".");
    }

    public async Task<KetQuaThaoTac> HuyAsync(int id, string? lyDo, CancellationToken ct = default)
    {
        lyDo = lyDo?.Trim();
        if (string.IsNullOrWhiteSpace(lyDo)) return KetQuaThaoTac.Loi("Vui lòng nhập lý do huỷ.");
        if (lyDo.Length > 500) lyDo = lyDo[..500];

        var lenh = await QueryDayDu().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lenh is null) return KetQuaThaoTac.Loi("Không tìm thấy lệnh sản xuất.");
        if (lenh.TrangThai == TrangThaiLenhSX.MoiTao)
            return KetQuaThaoTac.Loi("Lệnh chưa thực hiện thì dùng nút \"Xoá\", không cần huỷ.");
        if (lenh.TrangThai == TrangThaiLenhSX.DaHuy)
            return KetQuaThaoTac.Loi("Lệnh này đã huỷ rồi.");

        // Đảo đúng các dòng sổ kho do chính lệnh này sinh ra (giữ nguyên lô + hạn dùng, không tính
        // lại theo định mức - định mức có thể đã bị sửa sau khi lệnh chạy).
        var dongGoc = await _db.KhoGiaoDichs
            .Where(g => g.ChungTu == lenh.MaLenh
                        && (g.Loai == LoaiGiaoDichKho.XuatSanXuat || g.Loai == LoaiGiaoDichKho.NhapThanhPham))
            .ToListAsync(ct);
        if (dongGoc.Count == 0)
            return KetQuaThaoTac.Loi("Không tìm thấy giao dịch kho của lệnh này để đảo.");

        // Thành phẩm của MỌI lô phải còn nguyên mới thu hồi được.
        foreach (var sp in lenh.SanPham)
        {
            var canThuHoi = dongGoc
                .Where(g => g.Loai == LoaiGiaoDichKho.NhapThanhPham && g.MaLo == sp.MaLoThanhPham)
                .Sum(g => g.SoLuong);
            if (canThuHoi <= 0) continue;
            var tonLoTp = await TonLoAsync(sp.MaThanhPham, lenh.MaKho, sp.MaLoThanhPham, ct);
            if (tonLoTp < canThuHoi)
                return KetQuaThaoTac.Loi(
                    $"Không huỷ được: lô thành phẩm \"{sp.MaLoThanhPham}\" chỉ còn {tonLoTp:0.###}/"
                    + $"{canThuHoi:0.###} - phần đã xuất bán hoặc dùng tiếp không thu hồi được. "
                    + "Hãy dùng Kiểm kê / Điều chỉnh tồn để xử lý.");
        }

        var now = DateTime.UtcNow;
        foreach (var goc in dongGoc)
        {
            _db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = goc.MaSanPham, MaKho = goc.MaKho, MaLo = goc.MaLo,
                SoLuong = -goc.SoLuong, HanSuDung = goc.HanSuDung,
                Loai = LoaiGiaoDichKho.HoanTacSanXuat, ChungTu = lenh.MaLenh,
                GhiChu = $"Đảo do huỷ lệnh sản xuất {lenh.MaLenh}: {lyDo}",
                ThoiGianUtc = now
            });
        }

        // Lô sản xuất sinh kèm: xoá trong app, đồng thời gỡ khỏi hàng đợi nếu chưa gửi đi.
        var canhBaoHnC = new List<string>();
        var daXoaLo = new List<string>();
        var tenantId = _db.TenantInfo?.Id;
        foreach (var sp in lenh.SanPham.Where(s => !string.IsNullOrWhiteSpace(s.MaLoDaTao)))
        {
            var batch = await _db.Batches.FirstOrDefaultAsync(b => b.MaLo == sp.MaLoDaTao, ct);
            if (batch is not null) { _db.Batches.Remove(batch); daXoaLo.Add(sp.MaLoDaTao!); }

            var hangDoi = await _db.SyncOutboxItems
                .Where(o => o.TenantId == tenantId && o.EntityType == "Batch" && o.EntityKey == sp.MaLoDaTao)
                .ToListAsync(ct);
            // Đã gửi thành công (hoặc đang gửi) thì HanoiCheck đã có dữ liệu - không thu hồi được.
            if (hangDoi.Any(o => o.Status is SyncOutboxStatus.Success or SyncOutboxStatus.Processing))
                canhBaoHnC.Add(sp.MaLoDaTao!);
            _db.SyncOutboxItems.RemoveRange(
                hangDoi.Where(o => o.Status is not (SyncOutboxStatus.Success or SyncOutboxStatus.Processing)));
        }

        lenh.TrangThai = TrangThaiLenhSX.DaHuy;
        lenh.ThoiGianHuyUtc = now;
        lenh.LyDoHuy = lyDo;
        await _db.SaveChangesAsync(ct);

        var thongBao = $"Đã huỷ lệnh \"{lenh.MaLenh}\": trả lại nguyên liệu và thu hồi thành phẩm của "
                       + $"{lenh.SanPham.Count} lô.";
        if (canhBaoHnC.Count > 0)
            thongBao += $" CẢNH BÁO: lô {string.Join(", ", canhBaoHnC)} đã gửi sang HanoiCheck, hệ thống "
                        + "không thu hồi được - cần xử lý thủ công phía HanoiCheck.";
        var xoaChuaGui = daXoaLo.Except(canhBaoHnC).ToList();
        if (xoaChuaGui.Count > 0)
            thongBao += $" Đã xoá lô {string.Join(", ", xoaChuaGui)} khỏi app (chưa gửi sang HanoiCheck).";
        return KetQuaThaoTac.Ok(thongBao);
    }

    // ==================== Tiện ích kho ====================

    /// <summary>Nhu cầu nguyên liệu = định lượng × số lượng SX × (1 + hao hụt%).</summary>
    private static decimal NhuCau(DinhMucNguyenLieu dm, decimal soLuong) =>
        dm.SoLuong * soLuong * (1 + dm.HaoHutPhanTram / 100m);

    /// <summary>Tồn hiện tại của một lô cụ thể (sản phẩm + kho + lô).</summary>
    private async Task<decimal> TonLoAsync(string maSanPham, string maKho, string maLo, CancellationToken ct) =>
        await _db.KhoGiaoDichs
            .Where(g => g.MaSanPham == maSanPham && g.MaKho == maKho && g.MaLo == maLo)
            .SumAsync(g => (decimal?)g.SoLuong, ct) ?? 0m;

    /// <summary>Tồn hiện tại của một nguyên liệu trong một kho (tổng mọi lô).</summary>
    private async Task<decimal> TonAsync(string maSanPham, string maKho, CancellationToken ct) =>
        await _db.KhoGiaoDichs
            .Where(g => g.MaSanPham == maSanPham && g.MaKho == maKho)
            .SumAsync(g => (decimal?)g.SoLuong, ct) ?? 0m;

    /// <summary>Các lô còn tồn của (sản phẩm, kho), sắp theo FEFO: hết hạn sớm trước, lô không HSD sau cùng.</summary>
    private async Task<List<(string MaLo, DateOnly? Hsd, decimal Ton)>> LayLoFefoAsync(
        string maSanPham, string maKho, CancellationToken ct)
    {
        var gd = await _db.KhoGiaoDichs
            .Where(g => g.MaSanPham == maSanPham && g.MaKho == maKho)
            .ToListAsync(ct);

        return gd
            .GroupBy(g => g.MaLo)
            .Select(g => (
                MaLo: g.Key,
                Hsd: g.Where(x => x.HanSuDung.HasValue).Max(x => x.HanSuDung),
                Ton: g.Sum(x => x.SoLuong)))
            .Where(x => x.Ton > 0)
            .OrderBy(x => x.Hsd ?? DateOnly.MaxValue).ThenBy(x => x.MaLo)
            .ToList();
    }
}
