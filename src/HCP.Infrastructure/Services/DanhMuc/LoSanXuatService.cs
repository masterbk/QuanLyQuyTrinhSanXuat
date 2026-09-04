using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý Lô sản xuất của cơ sở đang đăng nhập (gồm kho chứa, các khâu, file minh chứng).
/// Đồng bộ sang HanoiCheck qua POST /supplier/batches/merge.
/// </summary>
public class LoSanXuatService : IDanhMucService<Batch>
{
    /// <summary>danh_sach_nguoi_thuc_hien tối đa 20 mã theo đặc tả.</summary>
    private const int MaxNguoiThucHien = 20;

    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;

    public LoSanXuatService(AppDbContext db, ISyncOutboxWriter outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    private IQueryable<Batch> QueryDayDu() => _db.Batches
        .Include(b => b.DanhSachKho)
        .Include(b => b.DanhSachKhau)
        .Include(b => b.DanhSachFile);

    public async Task<IReadOnlyList<Batch>> LayTatCaAsync(CancellationToken ct = default) =>
        await QueryDayDu().AsNoTracking().OrderByDescending(b => b.NgayNhap).ThenBy(b => b.MaLo).ToListAsync(ct);

    public Task<Batch?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        QueryDayDu().FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(Batch entity, CancellationToken ct = default)
    {
        entity.MaLo = entity.MaLo.Trim();

        if (await _db.Batches.AnyAsync(b => b.MaLo == entity.MaLo, ct))
        {
            return KetQuaThaoTac.Loi($"Mã lô \"{entity.MaLo}\" đã tồn tại.");
        }

        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        _db.Batches.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _outbox.ThemAsync("Batch", entity.MaLo, HnCPayloadMapper.LoSanXuat(entity), ct);

        return KetQuaThaoTac.Ok($"Đã thêm lô \"{entity.TenLo}\".");
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(Batch entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy lô cần sửa.");

        var maMoi = entity.MaLo.Trim();

        if (await _db.Batches.AnyAsync(b => b.MaLo == maMoi && b.Id != entity.Id, ct))
        {
            return KetQuaThaoTac.Loi($"Mã lô \"{maMoi}\" đã được dùng cho lô khác.");
        }

        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        hienTai.MaSanPham = entity.MaSanPham;
        hienTai.MaLo = maMoi;
        hienTai.TenLo = entity.TenLo;
        hienTai.NgayNhap = entity.NgayNhap;
        hienTai.NgaySanXuat = entity.NgaySanXuat;
        hienTai.HanSuDung = entity.HanSuDung;
        hienTai.DiaChiThuMua = entity.DiaChiThuMua;
        hienTai.MaCoSo = entity.MaCoSo;
        hienTai.MaNccDauVao = entity.MaNccDauVao;
        hienTai.GhiChu = entity.GhiChu;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        // Thay toàn bộ các bảng con: đơn giản và tránh sai lệch khi vừa thêm vừa xoá.
        _db.BatchWarehouses.RemoveRange(hienTai.DanhSachKho);
        _db.BatchSteps.RemoveRange(hienTai.DanhSachKhau);
        _db.BatchFiles.RemoveRange(hienTai.DanhSachFile);

        hienTai.DanhSachKho = entity.DanhSachKho.Select(w => new BatchWarehouse { MaKho = w.MaKho }).ToList();
        hienTai.DanhSachKhau = entity.DanhSachKhau.Select(SaoChepKhau).ToList();
        hienTai.DanhSachFile = entity.DanhSachFile.Select(SaoChepFile).ToList();

        await _db.SaveChangesAsync(ct);

        await _outbox.ThemAsync("Batch", hienTai.MaLo, HnCPayloadMapper.LoSanXuat(hienTai), ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật lô \"{hienTai.TenLo}\".");
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var lo = await _db.Batches.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (lo is null) return KetQuaThaoTac.Loi("Không tìm thấy lô cần xoá.");

        _db.Batches.Remove(lo); // các bảng con xoá theo cascade.
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá lô \"{lo.TenLo}\".");
    }

    /// <summary>
    /// Kiểm tra ràng buộc để không bị HanoiCheck trả 422: thực phẩm/kho/khâu phải tồn tại,
    /// lô thuộc ít nhất một kho, mã bước không trùng, số người thực hiện ≤ 20.
    /// </summary>
    private async Task<string?> KiemTraAsync(Batch b, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(b.MaSanPham))
            return "Vui lòng chọn thực phẩm của lô.";
        if (string.IsNullOrWhiteSpace(b.TenLo))
            return "Vui lòng nhập tên lô.";

        if (!await _db.Products.AnyAsync(p => p.MaSanPham == b.MaSanPham, ct))
            return $"Thực phẩm \"{b.MaSanPham}\" không có trong danh mục.";

        var khoHopLe = b.DanhSachKho.Select(w => w.MaKho.Trim())
            .Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
        if (khoHopLe.Count == 0)
            return "Lô phải thuộc ít nhất một kho.";

        var khoTonTai = await _db.Warehouses.Select(w => w.MaKho).ToListAsync(ct);
        var khoSai = khoHopLe.Except(khoTonTai).ToList();
        if (khoSai.Count > 0)
            return "Mã kho không có trong danh mục: " + string.Join(", ", khoSai);

        // Các khâu: mã bước duy nhất trong lô, mã khâu phải tồn tại, ≤20 người thực hiện.
        var maBuoc = b.DanhSachKhau.Select(s => s.MaBuocSx.Trim()).ToList();
        var buocTrung = maBuoc.GroupBy(m => m).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (buocTrung.Count > 0)
            return "Mã bước sản xuất bị lặp: " + string.Join(", ", buocTrung);

        if (b.DanhSachKhau.Count > 0)
        {
            var khauTonTai = await _db.ProductionSteps.Select(s => s.MaKhau).ToListAsync(ct);
            foreach (var khau in b.DanhSachKhau)
            {
                if (string.IsNullOrWhiteSpace(khau.MaBuocSx))
                    return "Mỗi khâu phải có mã bước sản xuất.";
                if (!khauTonTai.Contains(khau.MaKhau.Trim()))
                    return $"Mã khâu \"{khau.MaKhau}\" không có trong danh mục khâu.";
                if (khau.ThuTu is < 0 or > 99999)
                    return "Thứ tự bước phải trong khoảng 0–99999.";
                if (khau.NguoiThucHien.Count > MaxNguoiThucHien)
                    return $"Mỗi khâu chỉ được tối đa {MaxNguoiThucHien} người thực hiện.";
            }
        }

        // File: nếu có thì bắt buộc đủ mã/tên/đường dẫn/loại.
        foreach (var f in b.DanhSachFile)
        {
            if (string.IsNullOrWhiteSpace(f.MaFile) || string.IsNullOrWhiteSpace(f.TenFile)
                || string.IsNullOrWhiteSpace(f.DuongDan) || string.IsNullOrWhiteSpace(f.Loai))
            {
                return "Mỗi file phải có đủ mã file, tên, đường dẫn và loại.";
            }
        }

        return null;
    }

    private static void ChuanHoa(Batch b)
    {
        b.MaSanPham = b.MaSanPham.Trim();
        b.TenLo = b.TenLo.Trim();
        b.DiaChiThuMua = b.DiaChiThuMua?.Trim();
        b.MaCoSo = b.MaCoSo?.Trim();
        b.MaNccDauVao = b.MaNccDauVao?.Trim();
        b.GhiChu = b.GhiChu?.Trim();
    }

    private static BatchStep SaoChepKhau(BatchStep s) => new()
    {
        MaBuocSx = s.MaBuocSx.Trim(),
        MaKhau = s.MaKhau.Trim(),
        ThuTu = s.ThuTu,
        MaLoNhap = s.MaLoNhap,
        MaLoNguyenLieu = s.MaLoNguyenLieu,
        MaLoSanXuat = s.MaLoSanXuat,
        ThoiGian = s.ThoiGian,
        NguoiThucHienCsv = s.NguoiThucHienCsv,
        DiaChi = s.DiaChi,
        TrangThai = s.TrangThai,
        MaQrTruyVet = s.MaQrTruyVet,
        GhiChu = s.GhiChu,
        MaCoSo = s.MaCoSo,
        MaNccDauVao = s.MaNccDauVao
    };

    private static BatchFile SaoChepFile(BatchFile f) => new()
    {
        MaFile = f.MaFile.Trim(),
        TenFile = f.TenFile.Trim(),
        DuongDan = f.DuongDan.Trim(),
        Loai = f.Loai.Trim(),
        MaKhau = f.MaKhau,
        MaBuocSx = f.MaBuocSx
    };
}
