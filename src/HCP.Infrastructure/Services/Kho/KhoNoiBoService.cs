using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.Kho;

/// <inheritdoc cref="IKhoNoiBoService"/>
public sealed class KhoNoiBoService : IKhoNoiBoService
{
    private readonly AppDbContext _db;

    public KhoNoiBoService(AppDbContext db) => _db = db;

    public async Task<KetQuaThaoTac> NhapNguyenLieuAsync(NhapKhoRequest req, CancellationToken ct = default)
    {
        req.MaSanPham = req.MaSanPham?.Trim() ?? "";
        req.MaKho = req.MaKho?.Trim() ?? "";
        req.MaLo = req.MaLo?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(req.MaSanPham)) return KetQuaThaoTac.Loi("Vui lòng chọn nguyên liệu.");
        if (string.IsNullOrWhiteSpace(req.MaKho)) return KetQuaThaoTac.Loi("Vui lòng chọn kho.");
        if (string.IsNullOrWhiteSpace(req.MaLo)) return KetQuaThaoTac.Loi("Vui lòng nhập mã lô.");
        if (req.SoLuong <= 0) return KetQuaThaoTac.Loi("Số lượng nhập phải lớn hơn 0.");

        var sp = await _db.Products.FirstOrDefaultAsync(p => p.MaSanPham == req.MaSanPham, ct);
        if (sp is null) return KetQuaThaoTac.Loi($"Không tìm thấy sản phẩm \"{req.MaSanPham}\".");
        if (sp.LoaiSanPham != LoaiSanPham.NguyenLieu)
            return KetQuaThaoTac.Loi($"\"{sp.TenSanPham}\" không phải nguyên liệu. Chỉ nhập kho nguyên liệu ở đây.");

        if (!await _db.Warehouses.AnyAsync(k => k.MaKho == req.MaKho, ct))
            return KetQuaThaoTac.Loi($"Không tìm thấy kho \"{req.MaKho}\".");

        _db.KhoGiaoDichs.Add(new KhoGiaoDich
        {
            MaSanPham = req.MaSanPham,
            MaKho = req.MaKho,
            MaLo = req.MaLo,
            SoLuong = req.SoLuong,                 // nhập: dương
            HanSuDung = req.HanSuDung,
            Loai = LoaiGiaoDichKho.NhapNguyenLieu,
            ChungTu = "NK-" + DateTime.Now.ToString("yyMMdd-HHmmss"),
            MaNccDauVao = string.IsNullOrWhiteSpace(req.MaNccDauVao) ? null : req.MaNccDauVao.Trim(),
            GhiChu = req.GhiChu?.Trim(),
            ThoiGianUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok(
            $"Đã nhập {req.SoLuong:0.###} {sp.DonViTinh} \"{sp.TenSanPham}\" (lô {req.MaLo}) vào kho.");
    }

    public async Task<IReadOnlyList<TonKhoDto>> LayTonAsync(CancellationToken ct = default)
    {
        var giaoDich = await _db.KhoGiaoDichs.AsNoTracking().ToListAsync(ct);
        var sanPham = await _db.Products.AsNoTracking().ToListAsync(ct);
        var kho = await _db.Warehouses.AsNoTracking().ToListAsync(ct);

        var spTheoMa = sanPham
            .GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First());
        var khoTheoMa = kho
            .GroupBy(k => k.MaKho).ToDictionary(g => g.Key, g => g.First());

        return giaoDich
            .GroupBy(x => new { x.MaSanPham, x.MaKho, x.MaLo })
            .Select(g =>
            {
                var p = spTheoMa.GetValueOrDefault(g.Key.MaSanPham);
                return new TonKhoDto(
                    MaSanPham: g.Key.MaSanPham,
                    TenSanPham: p?.TenSanPham ?? g.Key.MaSanPham,
                    LoaiSanPham: p?.LoaiSanPham ?? LoaiSanPham.NguyenLieu,
                    DonViTinh: p?.DonViTinh,
                    MaKho: g.Key.MaKho,
                    TenKho: khoTheoMa.GetValueOrDefault(g.Key.MaKho)?.TenKho ?? g.Key.MaKho,
                    MaLo: g.Key.MaLo,
                    HanSuDung: g.Where(x => x.HanSuDung.HasValue).Max(x => x.HanSuDung),
                    SoLuongTon: g.Sum(x => x.SoLuong));
            })
            .Where(t => t.SoLuongTon != 0)
            .OrderBy(t => t.TenSanPham).ThenBy(t => t.MaKho).ThenBy(t => t.HanSuDung)
            .ToList();
    }

    public async Task<IReadOnlyList<KhoGiaoDich>> LayLichSuAsync(int gioiHan = 100, CancellationToken ct = default) =>
        await _db.KhoGiaoDichs.AsNoTracking()
            .OrderByDescending(x => x.ThoiGianUtc).ThenByDescending(x => x.Id)
            .Take(gioiHan)
            .ToListAsync(ct);
}
