using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý Món ăn của cơ sở (công thức, khâu chế biến, file minh chứng).
/// Đồng bộ sang HanoiCheck qua POST /supplier/dishes/merge.
/// </summary>
public class MonAnService : IDanhMucService<Dish>
{
    private const int MaxNguoiThucHien = 20;

    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;

    public MonAnService(AppDbContext db, ISyncOutboxWriter outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    private IQueryable<Dish> QueryDayDu() => _db.Dishes
        .Include(d => d.DanhSachNguyenLieu)
        .Include(d => d.DanhSachKhau)
        .Include(d => d.DanhSachFile);

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

        var maMoi = entity.MaMonAn.Trim();

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
        hienTai.MaCoSo = entity.MaCoSo;
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
    /// Ràng buộc để tránh HanoiCheck trả 422: công thức ≥1 và mỗi nguyên liệu phải là thực phẩm
    /// đã khai (định lượng ≥0); khâu phải tồn tại trong danh mục; ≤20 người/khâu; quy trình (nếu
    /// khai) phải tồn tại.
    /// </summary>
    private async Task<string?> KiemTraAsync(Dish d, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(d.TenMonAn))
            return "Vui lòng nhập tên món ăn.";

        var nguyenLieu = d.DanhSachNguyenLieu
            .Where(i => !string.IsNullOrWhiteSpace(i.MaNguyenLieu)).ToList();
        if (nguyenLieu.Count == 0)
            return "Món ăn phải có ít nhất một nguyên liệu.";

        var maTrung = nguyenLieu.GroupBy(i => i.MaNguyenLieu.Trim())
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (maTrung.Count > 0)
            return "Nguyên liệu bị lặp: " + string.Join(", ", maTrung);

        if (nguyenLieu.Any(i => i.DinhLuong < 0))
            return "Định lượng nguyên liệu không được âm.";

        var spTonTai = await _db.Products.Select(p => p.MaSanPham).ToListAsync(ct);
        var spSai = nguyenLieu.Select(i => i.MaNguyenLieu.Trim()).Except(spTonTai).ToList();
        if (spSai.Count > 0)
            return "Nguyên liệu không có trong danh mục thực phẩm: " + string.Join(", ", spSai);

        if (!string.IsNullOrWhiteSpace(d.MaQuyTrinh))
        {
            var mq = d.MaQuyTrinh.Trim();
            if (!await _db.ProductionProcesses.AnyAsync(q => q.MaQuyTrinh == mq, ct))
                return $"Quy trình \"{mq}\" không có trong danh mục.";
        }

        if (d.DanhSachKhau.Count > 0)
        {
            var khauTonTai = await _db.ProductionSteps.Select(s => s.MaKhau).ToListAsync(ct);
            foreach (var khau in d.DanhSachKhau)
            {
                if (!khauTonTai.Contains(khau.MaKhau.Trim()))
                    return $"Mã khâu \"{khau.MaKhau}\" không có trong danh mục khâu.";
                if (khau.ThuTu is < 0 or > 99999)
                    return "Thứ tự bước phải trong khoảng 0–99999.";
                if (khau.NguoiThucHien.Count > MaxNguoiThucHien)
                    return $"Mỗi khâu chỉ được tối đa {MaxNguoiThucHien} người thực hiện.";
            }
        }

        foreach (var f in d.DanhSachFile)
        {
            if (string.IsNullOrWhiteSpace(f.MaFile) || string.IsNullOrWhiteSpace(f.TenFile)
                || string.IsNullOrWhiteSpace(f.DuongDan) || string.IsNullOrWhiteSpace(f.Loai))
            {
                return "Mỗi file phải có đủ mã file, tên, đường dẫn và loại.";
            }
        }

        return null;
    }

    private static void ChuanHoa(Dish d)
    {
        d.TenMonAn = d.TenMonAn.Trim();
        d.MoTa = d.MoTa?.Trim();
        d.MaCoSo = d.MaCoSo?.Trim();
        d.MaQuyTrinh = string.IsNullOrWhiteSpace(d.MaQuyTrinh) ? null : d.MaQuyTrinh.Trim();
    }

    private static DishIngredient SaoChepNguyenLieu(DishIngredient i) => new()
    {
        MaNguyenLieu = i.MaNguyenLieu.Trim(),
        DinhLuong = i.DinhLuong,
        DonViTinhId = i.DonViTinhId
    };

    private static DishStep SaoChepKhau(DishStep s) => new()
    {
        MaBuocSx = s.MaBuocSx,
        MaKhau = s.MaKhau.Trim(),
        ThuTu = s.ThuTu,
        ThoiGian = s.ThoiGian,
        NguoiThucHienCsv = s.NguoiThucHienCsv,
        DiaChi = s.DiaChi,
        TrangThai = s.TrangThai,
        MaQrTruyVet = s.MaQrTruyVet,
        GhiChu = s.GhiChu,
        MaCoSo = s.MaCoSo,
        MaNccDauVao = s.MaNccDauVao
    };

    private static DishFile SaoChepFile(DishFile f) => new()
    {
        MaFile = f.MaFile.Trim(),
        TenFile = f.TenFile.Trim(),
        DuongDan = f.DuongDan.Trim(),
        Loai = f.Loai.Trim(),
        MaKhau = f.MaKhau,
        MaBuocSx = f.MaBuocSx
    };
}
