using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.Kho;

/// <inheritdoc cref="ILenhSanXuatService"/>
public sealed class LenhSanXuatService : ILenhSanXuatService
{
    private readonly AppDbContext _db;

    public LenhSanXuatService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<LenhSanXuat>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.LenhSanXuats.AsNoTracking()
            .OrderByDescending(l => l.NgaySanXuat).ThenByDescending(l => l.Id)
            .ToListAsync(ct);

    public Task<LenhSanXuat?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.LenhSanXuats.Include(l => l.TieuHao).FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<NguyenLieuCanDto>> TinhNguyenLieuCanAsync(
        string maThanhPham, decimal soLuong, string maKho, CancellationToken ct = default)
    {
        var tp = await _db.Products
            .Include(p => p.DanhSachDinhMuc)
            .FirstOrDefaultAsync(p => p.MaSanPham == maThanhPham, ct);
        if (tp is null || tp.DanhSachDinhMuc.Count == 0) return Array.Empty<NguyenLieuCanDto>();

        var sanPham = await _db.Products.AsNoTracking().ToListAsync(ct);
        var tenTheoMa = sanPham.GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First());

        var ket = new List<NguyenLieuCanDto>();
        foreach (var dm in tp.DanhSachDinhMuc)
        {
            var can = dm.SoLuong * soLuong;
            var ton = await TonAsync(dm.MaNguyenLieu, maKho, ct);
            var p = tenTheoMa.GetValueOrDefault(dm.MaNguyenLieu);
            ket.Add(new NguyenLieuCanDto(dm.MaNguyenLieu, p?.TenSanPham ?? dm.MaNguyenLieu,
                p?.DonViTinh, can, ton, ton >= can));
        }
        return ket;
    }

    public async Task<KetQuaThaoTac> TaoAsync(LenhSanXuat lenh, CancellationToken ct = default)
    {
        lenh.MaLenh = lenh.MaLenh?.Trim() ?? "";
        lenh.MaThanhPham = lenh.MaThanhPham?.Trim() ?? "";
        lenh.MaKho = lenh.MaKho?.Trim() ?? "";
        lenh.MaLoThanhPham = lenh.MaLoThanhPham?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(lenh.MaLenh)) return KetQuaThaoTac.Loi("Vui lòng nhập mã lệnh.");
        if (await _db.LenhSanXuats.AnyAsync(l => l.MaLenh == lenh.MaLenh, ct))
            return KetQuaThaoTac.Loi($"Mã lệnh \"{lenh.MaLenh}\" đã tồn tại.");
        if (lenh.SoLuong <= 0) return KetQuaThaoTac.Loi("Số lượng sản xuất phải lớn hơn 0.");
        if (string.IsNullOrWhiteSpace(lenh.MaLoThanhPham)) return KetQuaThaoTac.Loi("Vui lòng nhập mã lô thành phẩm.");

        var tp = await _db.Products.Include(p => p.DanhSachDinhMuc)
            .FirstOrDefaultAsync(p => p.MaSanPham == lenh.MaThanhPham, ct);
        if (tp is null || tp.LoaiSanPham != LoaiSanPham.ThanhPham)
            return KetQuaThaoTac.Loi("Vui lòng chọn thành phẩm hợp lệ.");
        if (tp.DanhSachDinhMuc.Count == 0)
            return KetQuaThaoTac.Loi($"\"{tp.TenSanPham}\" chưa có định mức. Khai định mức trước khi sản xuất.");
        if (!await _db.Warehouses.AnyAsync(k => k.MaKho == lenh.MaKho, ct))
            return KetQuaThaoTac.Loi("Vui lòng chọn kho hợp lệ.");

        lenh.TrangThai = TrangThaiLenhSX.MoiTao;
        lenh.GhiChu = lenh.GhiChu?.Trim();
        _db.LenhSanXuats.Add(lenh);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã tạo lệnh sản xuất \"{lenh.MaLenh}\". Bấm \"Thực hiện\" để trừ nguyên liệu.");
    }

    public async Task<KetQuaThaoTac> ThucHienAsync(int id, CancellationToken ct = default)
    {
        var lenh = await _db.LenhSanXuats.Include(l => l.TieuHao).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lenh is null) return KetQuaThaoTac.Loi("Không tìm thấy lệnh sản xuất.");
        if (lenh.TrangThai == TrangThaiLenhSX.HoanThanh)
            return KetQuaThaoTac.Loi("Lệnh này đã thực hiện rồi.");

        var tp = await _db.Products.Include(p => p.DanhSachDinhMuc)
            .FirstOrDefaultAsync(p => p.MaSanPham == lenh.MaThanhPham, ct);
        if (tp is null || tp.DanhSachDinhMuc.Count == 0)
            return KetQuaThaoTac.Loi("Thành phẩm không còn định mức, không thể thực hiện.");

        // Tính nhu cầu + gom lô FEFO cho từng nguyên liệu, kiểm tra đủ TRƯỚC khi trừ.
        var thieu = new List<string>();
        var keHoachTru = new List<(string MaNL, string MaLo, DateOnly? Hsd, decimal SoLuong)>();

        foreach (var dm in tp.DanhSachDinhMuc)
        {
            var can = dm.SoLuong * lenh.SoLuong;
            var lots = await LayLoFefoAsync(dm.MaNguyenLieu, lenh.MaKho, ct);
            var tongTon = lots.Sum(x => x.Ton);
            if (tongTon < can)
            {
                thieu.Add($"{dm.MaNguyenLieu} (cần {can:0.###}, còn {tongTon:0.###})");
                continue;
            }

            var conCanTru = can;
            foreach (var lot in lots)
            {
                if (conCanTru <= 0) break;
                var tru = Math.Min(conCanTru, lot.Ton);
                keHoachTru.Add((dm.MaNguyenLieu, lot.MaLo, lot.Hsd, tru));
                conCanTru -= tru;
            }
        }

        if (thieu.Count > 0)
            return KetQuaThaoTac.Loi("Không đủ nguyên liệu: " + string.Join("; ", thieu));

        var now = DateTime.UtcNow;

        // Trừ nguyên liệu (âm) theo kế hoạch FEFO + ghi tiêu hao.
        foreach (var t in keHoachTru)
        {
            _db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = t.MaNL, MaKho = lenh.MaKho, MaLo = t.MaLo,
                SoLuong = -t.SoLuong, HanSuDung = t.Hsd,
                Loai = LoaiGiaoDichKho.XuatSanXuat, ChungTu = lenh.MaLenh, ThoiGianUtc = now
            });
            lenh.TieuHao.Add(new LenhSanXuatTieuHao
            {
                MaNguyenLieu = t.MaNL, MaLo = t.MaLo, SoLuong = t.SoLuong
            });
        }

        // Nhập thành phẩm (dương).
        _db.KhoGiaoDichs.Add(new KhoGiaoDich
        {
            MaSanPham = lenh.MaThanhPham, MaKho = lenh.MaKho, MaLo = lenh.MaLoThanhPham,
            SoLuong = lenh.SoLuong, HanSuDung = lenh.HanSuDungThanhPham,
            Loai = LoaiGiaoDichKho.NhapThanhPham, ChungTu = lenh.MaLenh, ThoiGianUtc = now
        });

        lenh.TrangThai = TrangThaiLenhSX.HoanThanh;
        lenh.ThoiGianHoanThanhUtc = now;

        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok(
            $"Đã sản xuất {lenh.SoLuong:0.###} \"{tp.TenSanPham}\" (lô {lenh.MaLoThanhPham}), trừ nguyên liệu theo định mức.");
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var lenh = await _db.LenhSanXuats.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lenh is null) return KetQuaThaoTac.Loi("Không tìm thấy lệnh sản xuất.");
        if (lenh.TrangThai == TrangThaiLenhSX.HoanThanh)
            return KetQuaThaoTac.Loi("Không xoá được lệnh đã thực hiện (đã phát sinh giao dịch kho).");

        _db.LenhSanXuats.Remove(lenh);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã xoá lệnh \"{lenh.MaLenh}\".");
    }

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
