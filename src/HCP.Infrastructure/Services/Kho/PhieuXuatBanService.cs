using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.Kho;

/// <inheritdoc cref="IPhieuXuatBanService"/>
public sealed class PhieuXuatBanService : IPhieuXuatBanService
{
    private readonly AppDbContext _db;

    public PhieuXuatBanService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PhieuXuatBan>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.PhieuXuatBans.AsNoTracking().Include(p => p.ChiTiet)
            .OrderByDescending(p => p.NgayXuat).ThenByDescending(p => p.Id)
            .ToListAsync(ct);

    public Task<PhieuXuatBan?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.PhieuXuatBans.Include(p => p.ChiTiet).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<TonThanhPhamDto>> KiemTraTonAsync(
        IEnumerable<PhieuXuatBanChiTiet> chiTiet, string maKho, CancellationToken ct = default)
    {
        // Gộp các dòng trùng mã thành phẩm (nhu cầu tổng cộng của mỗi thành phẩm).
        var nhuCau = chiTiet
            .Where(c => !string.IsNullOrWhiteSpace(c.MaThanhPham) && c.SoLuong > 0)
            .GroupBy(c => c.MaThanhPham.Trim())
            .ToDictionary(g => g.Key, g => g.Sum(x => x.SoLuong));
        if (nhuCau.Count == 0) return Array.Empty<TonThanhPhamDto>();

        var sanPham = await _db.Products.AsNoTracking().ToListAsync(ct);
        var spTheoMa = sanPham.GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First());

        var ket = new List<TonThanhPhamDto>();
        foreach (var (ma, can) in nhuCau)
        {
            var ton = await TonAsync(ma, maKho, ct);
            var p = spTheoMa.GetValueOrDefault(ma);
            ket.Add(new TonThanhPhamDto(ma, p?.TenSanPham ?? ma, p?.DonViTinh, can, ton, ton >= can));
        }
        return ket;
    }

    public async Task<KetQuaThaoTac> TaoAsync(PhieuXuatBan phieu, CancellationToken ct = default)
    {
        phieu.MaPhieu = phieu.MaPhieu?.Trim() ?? "";
        phieu.MaKhachHang = phieu.MaKhachHang?.Trim() ?? "";
        phieu.MaKho = phieu.MaKho?.Trim() ?? "";
        phieu.GhiChu = phieu.GhiChu?.Trim();

        if (string.IsNullOrWhiteSpace(phieu.MaPhieu)) return KetQuaThaoTac.Loi("Vui lòng nhập mã phiếu.");
        if (await _db.PhieuXuatBans.AnyAsync(p => p.MaPhieu == phieu.MaPhieu, ct))
            return KetQuaThaoTac.Loi($"Mã phiếu \"{phieu.MaPhieu}\" đã tồn tại.");
        if (string.IsNullOrWhiteSpace(phieu.MaKhachHang)) return KetQuaThaoTac.Loi("Vui lòng chọn khách hàng.");
        if (!await _db.KhachHangs.AnyAsync(k => k.MaKhachHang == phieu.MaKhachHang, ct))
            return KetQuaThaoTac.Loi("Khách hàng không hợp lệ.");
        if (!await _db.Warehouses.AnyAsync(k => k.MaKho == phieu.MaKho, ct))
            return KetQuaThaoTac.Loi("Vui lòng chọn kho hợp lệ.");

        // Gộp dòng trùng mã + loại dòng rỗng; kiểm tra là thành phẩm.
        var dong = (phieu.ChiTiet ?? new())
            .Where(c => !string.IsNullOrWhiteSpace(c.MaThanhPham) && c.SoLuong > 0)
            .GroupBy(c => c.MaThanhPham.Trim())
            .Select(g => new PhieuXuatBanChiTiet { MaThanhPham = g.Key, SoLuong = g.Sum(x => x.SoLuong) })
            .ToList();
        if (dong.Count == 0) return KetQuaThaoTac.Loi("Phiếu chưa có dòng thành phẩm nào hợp lệ.");

        foreach (var c in dong)
        {
            var sp = await _db.Products.FirstOrDefaultAsync(p => p.MaSanPham == c.MaThanhPham, ct);
            if (sp is null) return KetQuaThaoTac.Loi($"Không tìm thấy sản phẩm \"{c.MaThanhPham}\".");
            if (sp.LoaiSanPham != LoaiSanPham.ThanhPham)
                return KetQuaThaoTac.Loi($"\"{sp.TenSanPham}\" không phải thành phẩm, không bán ở đây.");
        }

        phieu.ChiTiet = dong;
        phieu.TrangThai = TrangThaiXuatBan.MoiTao;
        _db.PhieuXuatBans.Add(phieu);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã tạo phiếu xuất bán \"{phieu.MaPhieu}\". Bấm \"Thực hiện\" để trừ kho.");
    }

    public async Task<KetQuaThaoTac> ThucHienAsync(int id, CancellationToken ct = default)
    {
        var phieu = await _db.PhieuXuatBans.Include(p => p.ChiTiet)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (phieu is null) return KetQuaThaoTac.Loi("Không tìm thấy phiếu xuất bán.");
        if (phieu.TrangThai == TrangThaiXuatBan.HoanThanh)
            return KetQuaThaoTac.Loi("Phiếu này đã thực hiện rồi.");
        if (phieu.ChiTiet.Count == 0)
            return KetQuaThaoTac.Loi("Phiếu không có dòng thành phẩm.");

        // Gộp nhu cầu theo thành phẩm, gom lô FEFO, kiểm tra đủ TRƯỚC khi trừ.
        var nhuCau = phieu.ChiTiet
            .GroupBy(c => c.MaThanhPham)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.SoLuong));

        var thieu = new List<string>();
        var keHoachTru = new List<(string MaSp, string MaLo, DateOnly? Hsd, decimal SoLuong)>();

        foreach (var (ma, can) in nhuCau)
        {
            var lots = await LayLoFefoAsync(ma, phieu.MaKho, ct);
            var tongTon = lots.Sum(x => x.Ton);
            if (tongTon < can)
            {
                thieu.Add($"{ma} (cần {can:0.###}, còn {tongTon:0.###})");
                continue;
            }

            var conCanTru = can;
            foreach (var lot in lots)
            {
                if (conCanTru <= 0) break;
                var tru = Math.Min(conCanTru, lot.Ton);
                keHoachTru.Add((ma, lot.MaLo, lot.Hsd, tru));
                conCanTru -= tru;
            }
        }

        if (thieu.Count > 0)
            return KetQuaThaoTac.Loi("Không đủ tồn thành phẩm: " + string.Join("; ", thieu));

        var now = DateTime.UtcNow;
        foreach (var t in keHoachTru)
        {
            _db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = t.MaSp, MaKho = phieu.MaKho, MaLo = t.MaLo,
                SoLuong = -t.SoLuong, HanSuDung = t.Hsd,
                Loai = LoaiGiaoDichKho.XuatBan, ChungTu = phieu.MaPhieu, ThoiGianUtc = now
            });
        }

        phieu.TrangThai = TrangThaiXuatBan.HoanThanh;
        phieu.ThoiGianHoanThanhUtc = now;
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã xuất bán phiếu \"{phieu.MaPhieu}\", trừ tồn thành phẩm theo lô (FEFO).");
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var phieu = await _db.PhieuXuatBans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (phieu is null) return KetQuaThaoTac.Loi("Không tìm thấy phiếu xuất bán.");
        if (phieu.TrangThai == TrangThaiXuatBan.HoanThanh)
            return KetQuaThaoTac.Loi("Không xoá được phiếu đã thực hiện (đã phát sinh giao dịch kho).");

        _db.PhieuXuatBans.Remove(phieu);
        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã xoá phiếu \"{phieu.MaPhieu}\".");
    }

    /// <summary>Tồn hiện tại của một sản phẩm trong một kho (tổng mọi lô).</summary>
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
