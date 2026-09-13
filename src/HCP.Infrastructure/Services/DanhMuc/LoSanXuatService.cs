using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.MaTuSinh;
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

    /// <summary>Album ảnh chung của lô: 1-3 ảnh (đặc tả v2.2).</summary>
    public const int SoAnhLoToiDa = 3;

    /// <summary>Tệp minh chứng mỗi khâu: tối đa 3 (đặc tả v2.2).</summary>
    public const int SoTepMoiKhauToiDa = 3;

    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;
    private readonly IMaTuSinhService _maTuSinh;

    public LoSanXuatService(AppDbContext db, ISyncOutboxWriter outbox, IMaTuSinhService maTuSinh)
    {
        _db = db;
        _outbox = outbox;
        _maTuSinh = maTuSinh;
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
        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        // Mã lô LO-yyyyMMdd-001 theo ngày sản xuất (chưa có thì ngày nhập) - chung dãy với lô trong lệnh SX.
        var ngay = entity.NgaySanXuat ?? (entity.NgayNhap == default ? null : entity.NgayNhap);
        entity.MaLo = await _maTuSinh.SinhAsync(LoaiMaTuSinh.LoSanXuat, ngay, ct);

        _db.Batches.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(entity, ct);

        return KetQuaThaoTac.Ok($"Đã thêm lô \"{entity.TenLo}\" (mã {entity.MaLo}).").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(Batch entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy lô cần sửa.");

        // Mã lô KHÔNG sửa được: là khoá đối chiếu với HanoiCheck và được sổ kho/đơn hàng tham chiếu.
        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        hienTai.MaSanPham = entity.MaSanPham;
        hienTai.TenLo = entity.TenLo;
        hienTai.NgayNhap = entity.NgayNhap;
        hienTai.NgaySanXuat = entity.NgaySanXuat;
        hienTai.HanSuDung = entity.HanSuDung;
        hienTai.DiaChiThuMua = entity.DiaChiThuMua;
        hienTai.MaCoSo = entity.MaCoSo;
        hienTai.MaNccDauVao = entity.MaNccDauVao;
        hienTai.GhiChu = entity.GhiChu;
        hienTai.DongBoHnC = entity.DongBoHnC;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        // Thay toàn bộ các bảng con: đơn giản và tránh sai lệch khi vừa thêm vừa xoá.
        _db.BatchWarehouses.RemoveRange(hienTai.DanhSachKho);
        _db.BatchSteps.RemoveRange(hienTai.DanhSachKhau);
        _db.BatchFiles.RemoveRange(hienTai.DanhSachFile);

        hienTai.DanhSachKho = entity.DanhSachKho.Select(w => new BatchWarehouse { MaKho = w.MaKho }).ToList();
        hienTai.DanhSachKhau = entity.DanhSachKhau.Select(SaoChepKhau).ToList();
        hienTai.DanhSachFile = entity.DanhSachFile.Select(SaoChepFile).ToList();

        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(hienTai, ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật lô \"{hienTai.TenLo}\".").KemGhiChu(dongBo);
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

        // File theo đặc tả HanoiCheck v2.2:
        //  - File KHÔNG gắn bước SX = ảnh chung của lô (album): bắt buộc 1-3 ảnh, đuôi ảnh, không Google Drive.
        //  - File có mã bước SX = tệp minh chứng của đúng khâu đó: ảnh/PDF/Word/Google Drive, tối đa 3 tệp/khâu.
        // Các quy định file của HanoiCheck chỉ áp khi lô này thật sự gửi HanoiCheck (công tắc tổng bật + tick đồng bộ).
        var apHnC = b.DongBoHnC && await _outbox.DangBatAsync(ct);
        var maBuocCuaLo = b.DanhSachKhau.Select(s => s.MaBuocSx.Trim()).ToHashSet(StringComparer.Ordinal);
        foreach (var f in b.DanhSachFile)
        {
            if (string.IsNullOrWhiteSpace(f.MaFile) || string.IsNullOrWhiteSpace(f.TenFile)
                || string.IsNullOrWhiteSpace(f.DuongDan) || string.IsNullOrWhiteSpace(f.Loai))
            {
                return "Mỗi file phải có đủ mã file, tên, đường dẫn và loại.";
            }

            if (!apHnC) continue;

            if (HnCPayloadMapper.LaAnhLo(f))
            {
                if (!HnCPayloadMapper.LaDuongDanAnhLo(f.DuongDan))
                    return $"File \"{f.TenFile}\": ảnh chung của lô phải là đường dẫn ảnh (.jpg, .jpeg, .png, .gif, "
                           + ".webp), không dùng Google Drive. Tệp minh chứng khác thì chọn Mã bước SX.";
            }
            else
            {
                if (!maBuocCuaLo.Contains(f.MaBuocSx!.Trim()))
                    return $"File \"{f.TenFile}\": mã bước SX \"{f.MaBuocSx}\" không có trong các khâu của lô.";
                if (!HnCPayloadMapper.LaDuongDanTepKhau(f.DuongDan))
                    return $"File \"{f.TenFile}\": tệp minh chứng phải là ảnh, PDF, Word (.pdf, .doc, .docx) "
                           + "hoặc đường dẫn Google Drive.";
            }
        }

        if (!apHnC) return null;

        // HanoiCheck thực tế chặn (422 "Nhà cung cấp phụ không cung ứng danh mục của thực phẩm", dù tài liệu v2.2 ghi
        // "không kiểm tra"): nhom_thuc_pham của NCC đầu vào của lô phải chứa ma_loai_sp của thực phẩm.
        if (!string.IsNullOrWhiteSpace(b.MaNccDauVao))
        {
            var maNcc = b.MaNccDauVao.Trim();
            var maSp = b.MaSanPham.Trim();
            var ncc = await _db.SubSuppliers.AsNoTracking().Include(s => s.NhomThucPham)
                .FirstOrDefaultAsync(s => s.MaNccDauVao == maNcc, ct);
            var maLoai = (await _db.Products.Where(p => p.MaSanPham == maSp)
                .Select(p => p.MaLoaiSp).FirstOrDefaultAsync(ct))?.Trim();

            if (ncc is not null && !string.IsNullOrWhiteSpace(maLoai)
                && !ncc.NhomThucPham.Any(g => g.MaNhom.Trim() == maLoai))
            {
                var idLoai = int.TryParse(maLoai, out var id) ? id : -1;
                var tenLoai = await _db.StandardFoodCategories.AsNoTracking()
                    .Where(c => c.Id == idLoai || c.Code == maLoai).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? maLoai;
                return $"Nhà cung ứng \"{ncc.Ten}\" ({maNcc}) chưa khai cung ứng danh mục \"{tenLoai}\" của thực phẩm "
                       + $"{maSp} - HanoiCheck sẽ từ chối lô. Bổ sung danh mục này vào nhóm thực phẩm của nhà cung ứng "
                       + "(và để NCC đồng bộ xong), hoặc bỏ trống Nhà cung ứng đầu vào của lô.";
            }
        }

        var soAnhLo = b.DanhSachFile.Count(HnCPayloadMapper.LaAnhLo);
        if (soAnhLo is < 1 or > SoAnhLoToiDa)
            return $"Lô phải có từ 1 đến {SoAnhLoToiDa} ảnh chung (đang có {soAnhLo}) - HanoiCheck bắt buộc album ảnh "
                   + "của lô. Ảnh chung là file để trống Mã bước SX.";

        var buocNhieuTep = b.DanhSachFile.Where(f => !HnCPayloadMapper.LaAnhLo(f))
            .GroupBy(f => f.MaBuocSx!.Trim()).FirstOrDefault(g => g.Count() > SoTepMoiKhauToiDa);
        if (buocNhieuTep is not null)
            return $"Bước \"{buocNhieuTep.Key}\" có {buocNhieuTep.Count()} tệp, tối đa {SoTepMoiKhauToiDa} tệp mỗi khâu.";

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
