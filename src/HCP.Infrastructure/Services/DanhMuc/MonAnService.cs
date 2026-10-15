using System.Text.RegularExpressions;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý Món ăn của cơ sở (công thức, khâu chế biến, ảnh món và tệp minh chứng).
/// Đồng bộ sang HanoiCheck qua POST /supplier/dishes/merge (đặc tả v2.6, dịch vụ 9).
/// </summary>
public class MonAnService : IDanhMucService<Dish>
{
    private const int MaxNguoiThucHien = 20;
    private const int SoNguyenLieuToiDa = 100;
    public const int SoKhauToiDa = 50;
    public const int SoAnhMonToiDa = 3;
    public const int SoTepMoiKhauToiDa = 3;

    /// <summary>Đặc tả Phần III mục 4: mã nghiệp vụ chỉ gồm chữ cái, số, "_" và "-".</summary>
    private static readonly Regex MaHopLe = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;

    public MonAnService(AppDbContext db, ISyncOutboxWriter outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    // AsSplitQuery(): 3 collection cùng lúc - gộp 1 câu JOIN sẽ nhân dòng theo tích số, tách câu truy
    // vấn cho rẻ hơn (khớp cảnh báo MultipleCollectionInclude của EF Core).
    private IQueryable<Dish> QueryDayDu() => _db.Dishes
        .Include(d => d.DanhSachNguyenLieu)
        .Include(d => d.DanhSachKhau)
        .Include(d => d.DanhSachFile)
        .AsSplitQuery();

    public async Task<IReadOnlyList<Dish>> LayTatCaAsync(CancellationToken ct = default) =>
        await QueryDayDu().AsNoTracking().OrderBy(d => d.MaMonAn).ToListAsync(ct);

    public Task<Dish?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        QueryDayDu().FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(Dish entity, CancellationToken ct = default)
    {
        entity.MaMonAn = entity.MaMonAn.Trim();

        if (await _db.Dishes.AnyAsync(d => d.MaMonAn == entity.MaMonAn, ct))
        {
            return KetQuaThaoTac.Loi($"Mã món ăn \"{entity.MaMonAn}\" đã tồn tại.");
        }

        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        _db.Dishes.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(entity, ct);

        return KetQuaThaoTac.Ok($"Đã thêm món ăn \"{entity.TenMonAn}\".").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(Dish entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy món ăn cần sửa.");

        entity.MaMonAn = entity.MaMonAn.Trim();
        var maMoi = entity.MaMonAn;

        if (await _db.Dishes.AnyAsync(d => d.MaMonAn == maMoi && d.Id != entity.Id, ct))
        {
            return KetQuaThaoTac.Loi($"Mã món ăn \"{maMoi}\" đã được dùng cho món khác.");
        }

        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        hienTai.MaMonAn = maMoi;
        hienTai.TenMonAn = entity.TenMonAn;
        hienTai.NhomTuoiId = entity.NhomTuoiId;
        hienTai.MoTa = entity.MoTa;
        hienTai.MaQuyTrinh = entity.MaQuyTrinh;
        hienTai.DongBoHnC = entity.DongBoHnC;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        _db.DishIngredients.RemoveRange(hienTai.DanhSachNguyenLieu);
        _db.DishSteps.RemoveRange(hienTai.DanhSachKhau);
        _db.DishFiles.RemoveRange(hienTai.DanhSachFile);

        hienTai.DanhSachNguyenLieu = entity.DanhSachNguyenLieu.Select(SaoChepNguyenLieu).ToList();
        hienTai.DanhSachKhau = entity.DanhSachKhau.Select(SaoChepKhau).ToList();
        hienTai.DanhSachFile = entity.DanhSachFile.Select(SaoChepFile).ToList();

        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(hienTai, ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật món ăn \"{hienTai.TenMonAn}\".").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var mon = await _db.Dishes.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (mon is null) return KetQuaThaoTac.Loi("Không tìm thấy món ăn cần xoá.");

        _db.Dishes.Remove(mon); // bảng con xoá theo cascade.
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá món ăn \"{mon.TenMonAn}\".");
    }

    /// <summary>
    /// Ràng buộc để tránh HanoiCheck trả 422 (đặc tả v2.6, dịch vụ 9). Phần dữ liệu cơ bản luôn kiểm tra;
    /// các quy định riêng của HanoiCheck (album ảnh, khâu bắt buộc, quy trình loại món ăn, nhóm tuổi...) chỉ áp
    /// khi món này thật sự gửi HanoiCheck (công tắc tổng bật + tick đồng bộ), giống Lô nhập kho.
    /// </summary>
    private async Task<string?> KiemTraAsync(Dish d, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(d.TenMonAn))
            return "Vui lòng nhập tên món ăn.";

        var nguyenLieu = d.DanhSachNguyenLieu
            .Where(i => !string.IsNullOrWhiteSpace(i.MaNguyenLieu)).ToList();
        if (nguyenLieu.Count == 0)
            return "Món ăn phải có ít nhất một nguyên liệu.";
        if (nguyenLieu.Count > SoNguyenLieuToiDa)
            return $"Công thức tối đa {SoNguyenLieuToiDa} nguyên liệu.";

        var maTrung = nguyenLieu.GroupBy(i => i.MaNguyenLieu.Trim())
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (maTrung.Count > 0)
            return "Nguyên liệu bị lặp: " + string.Join(", ", maTrung);

        if (nguyenLieu.Any(i => i.DinhLuong <= 0))
            return "Định lượng nguyên liệu phải lớn hơn 0.";

        var maNguyenLieu = nguyenLieu.Select(i => i.MaNguyenLieu.Trim()).ToList();
        var sanPham = await _db.Products.AsNoTracking()
            .Where(p => maNguyenLieu.Contains(p.MaSanPham))
            .Select(p => new { p.MaSanPham, p.LoaiSanPham, p.DongBoHnC })
            .ToListAsync(ct);
        var spSai = maNguyenLieu.Except(sanPham.Select(p => p.MaSanPham)).ToList();
        if (spSai.Count > 0)
            return "Nguyên liệu không có trong danh mục thực phẩm: " + string.Join(", ", spSai);

        QuyTrinhGon? quyTrinh = null;
        if (!string.IsNullOrWhiteSpace(d.MaQuyTrinh))
        {
            var mq = d.MaQuyTrinh.Trim();
            quyTrinh = await _db.ProductionProcesses.AsNoTracking().Where(q => q.MaQuyTrinh == mq)
                .Select(q => new QuyTrinhGon(q.TenQuyTrinh, q.LoaiApDung)).FirstOrDefaultAsync(ct);
            if (quyTrinh is null)
                return $"Quy trình \"{mq}\" không có trong danh mục.";
            if (quyTrinh.LoaiApDung != LoaiQuyTrinh.MonAn)
                return $"Quy trình \"{quyTrinh.Ten}\" là quy trình dùng cho thực phẩm. Món ăn phải chọn quy trình "
                       + "loại \"Món ăn\" (sửa ở màn Quy trình sản xuất).";
        }

        if (d.DanhSachKhau.Count > SoKhauToiDa)
            return $"Món ăn tối đa {SoKhauToiDa} khâu chế biến.";

        var maKhauCuaMon = new HashSet<string>(StringComparer.Ordinal);
        if (d.DanhSachKhau.Count > 0)
        {
            var khauTonTai = await _db.ProductionSteps.Select(s => s.MaKhau).ToListAsync(ct);
            foreach (var khau in d.DanhSachKhau)
            {
                if (string.IsNullOrWhiteSpace(khau.MaKhau))
                    return "Vui lòng chọn khâu cho mọi dòng khâu chế biến.";
                if (!khauTonTai.Contains(khau.MaKhau.Trim()))
                    return $"Mã khâu \"{khau.MaKhau}\" không có trong danh mục khâu.";
                if (khau.ThuTu is < 0 or > 99999)
                    return "Thứ tự khâu phải trong khoảng 0–99999.";
                if (khau.NguoiThucHien.Count > MaxNguoiThucHien)
                    return $"Mỗi khâu chỉ được tối đa {MaxNguoiThucHien} người thực hiện.";
                maKhauCuaMon.Add(khau.MaKhau.Trim());
            }
        }

        foreach (var f in d.DanhSachFile)
        {
            if (string.IsNullOrWhiteSpace(f.MaFile) || string.IsNullOrWhiteSpace(f.TenFile)
                || string.IsNullOrWhiteSpace(f.DuongDan) || string.IsNullOrWhiteSpace(f.Loai))
            {
                return "Mỗi ảnh/tệp phải có đủ mã file, tên, đường dẫn và loại.";
            }

            if (!HnCPayloadMapper.LaAnhMon(f) && !maKhauCuaMon.Contains(f.MaKhau!.Trim()))
                return $"Tệp \"{f.TenFile}\": khâu \"{f.MaKhau}\" không có trong các khâu của món.";
        }

        if (!d.DongBoHnC || !await _outbox.DangBatAsync(ct)) return null;

        // ---------- Quy định riêng của HanoiCheck ----------

        if (!MaHopLe.IsMatch(d.MaMonAn))
            return "Mã món ăn chỉ được chứa chữ cái không dấu, số, dấu gạch dưới (_) và gạch ngang (-) - "
                   + "HanoiCheck từ chối mã có dấu tiếng Việt hoặc khoảng trắng.";

        if (d.NhomTuoiId <= 0)
            return "Vui lòng chọn nhóm tuổi của món ăn.";
        var nhomTuoi = await _db.NhomTuoiHnCs.AsNoTracking().Select(n => n.Id).ToListAsync(ct);
        if (nhomTuoi.Count > 0 && !nhomTuoi.Contains(d.NhomTuoiId))
            return $"Nhóm tuổi {d.NhomTuoiId} không có trong danh mục nhóm tuổi của HanoiCheck. Vui lòng chọn lại.";

        if (quyTrinh is null)
            return "Vui lòng chọn quy trình chế biến (loại \"Món ăn\") - HanoiCheck bắt buộc quy trình cho món ăn. "
                   + "Nếu món này không gửi HanoiCheck thì bỏ tick \"Đồng bộ HanoiCheck\".";

        // Nguyên liệu phải là thực phẩm đã gửi HanoiCheck (dịch vụ 7). Nguyên liệu nội bộ không gửi đi nên HnC không biết.
        var khongGuiHnC = sanPham.Where(p => p.LoaiSanPham != LoaiSanPham.ThanhPham).Select(p => p.MaSanPham).ToList();
        if (khongGuiHnC.Count > 0)
            return "Nguyên liệu của món phải là thực phẩm khai với HanoiCheck (loại \"Thành phẩm\" ở màn Thực phẩm), "
                   + "không dùng nguyên liệu nội bộ: " + string.Join(", ", khongGuiHnC);

        if (nguyenLieu.Any(i => i.DinhLuong != decimal.Round(i.DinhLuong, 5) || i.DinhLuong > 9999999.99999m))
            return "Định lượng tối đa 5 chữ số thập phân và không vượt quá 9.999.999,99999.";

        if (d.DanhSachKhau.Count == 0)
            return "Món ăn phải có ít nhất một khâu chế biến - HanoiCheck bắt buộc. Có thể bấm \"Lấy khâu từ quy trình\".";

        var soAnh = d.DanhSachFile.Count(HnCPayloadMapper.LaAnhMon);
        if (soAnh is < 1 or > SoAnhMonToiDa)
            return $"Món ăn phải có từ 1 đến {SoAnhMonToiDa} ảnh món (đang có {soAnh}) - HanoiCheck bắt buộc album "
                   + "ảnh của món. Ảnh món là file để trống Khâu.";

        foreach (var f in d.DanhSachFile)
        {
            if (!HnCPayloadMapper.LaDuongDanTepKhau(f.DuongDan))
                return $"File \"{f.TenFile}\": đường dẫn phải là ảnh hoặc PDF (.jpg, .jpeg, .png, .gif, .webp, .pdf), "
                       + "không dùng Word hay Google Drive.";
        }

        var khauNhieuTep = d.DanhSachFile.Where(f => !HnCPayloadMapper.LaAnhMon(f))
            .GroupBy(f => f.MaKhau!.Trim()).FirstOrDefault(g => g.Count() > SoTepMoiKhauToiDa);
        if (khauNhieuTep is not null)
            return $"Khâu \"{khauNhieuTep.Key}\" có {khauNhieuTep.Count()} tệp, tối đa {SoTepMoiKhauToiDa} tệp mỗi khâu.";

        return null;
    }

    private sealed record QuyTrinhGon(string Ten, string LoaiApDung);

    private static void ChuanHoa(Dish d)
    {
        d.TenMonAn = d.TenMonAn.Trim();
        d.MoTa = d.MoTa?.Trim();
        d.MaQuyTrinh = string.IsNullOrWhiteSpace(d.MaQuyTrinh) ? null : d.MaQuyTrinh.Trim();
    }

    private static DishIngredient SaoChepNguyenLieu(DishIngredient i) => new()
    {
        MaNguyenLieu = i.MaNguyenLieu.Trim(),
        DinhLuong = i.DinhLuong
    };

    private static DishStep SaoChepKhau(DishStep s) => new()
    {
        MaKhau = s.MaKhau.Trim(),
        ThuTu = s.ThuTu,
        ThoiGian = s.ThoiGian,
        NguoiThucHienCsv = s.NguoiThucHienCsv,
        DiaChi = s.DiaChi,
        TrangThai = s.TrangThai,
        MaQrTruyVet = s.MaQrTruyVet,
        GhiChu = s.GhiChu,
        MaCoSo = string.IsNullOrWhiteSpace(s.MaCoSo) ? null : s.MaCoSo.Trim()
    };

    private static DishFile SaoChepFile(DishFile f) => new()
    {
        MaFile = f.MaFile.Trim(),
        TenFile = f.TenFile.Trim(),
        DuongDan = f.DuongDan.Trim(),
        Loai = f.Loai.Trim(),
        MaKhau = string.IsNullOrWhiteSpace(f.MaKhau) ? null : f.MaKhau.Trim()
    };
}
