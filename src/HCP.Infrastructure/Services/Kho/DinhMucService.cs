using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.Kho;

/// <inheritdoc cref="IDinhMucService"/>
public sealed class DinhMucService : IDinhMucService
{
    private readonly AppDbContext _db;

    public DinhMucService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DinhMucNguyenLieu>> LayTheoThanhPhamAsync(
        int productId, CancellationToken ct = default) =>
        await _db.DinhMucNguyenLieus.AsNoTracking()
            .Where(d => d.ProductId == productId)
            .OrderBy(d => d.MaNguyenLieu)
            .ToListAsync(ct);

    public async Task<KetQuaThaoTac> LuuAsync(int productId, IEnumerable<DinhMucNguyenLieu> dongDinhMuc,
                                              CancellationToken ct = default)
    {
        var tp = await _db.Products
            .Include(p => p.DanhSachDinhMuc)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (tp is null) return KetQuaThaoTac.Loi("Không tìm thấy thành phẩm.");
        if (tp.LoaiSanPham != LoaiSanPham.ThanhPham)
            return KetQuaThaoTac.Loi("Chỉ khai định mức cho thành phẩm.");

        var dsMoi = dongDinhMuc
            .Where(d => !string.IsNullOrWhiteSpace(d.MaNguyenLieu))
            .Select(d => new { Ma = d.MaNguyenLieu.Trim(), d.SoLuong })
            .ToList();

        var maTrung = dsMoi.GroupBy(x => x.Ma).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (maTrung.Count > 0)
            return KetQuaThaoTac.Loi("Nguyên liệu bị lặp trong định mức: " + string.Join(", ", maTrung));

        if (dsMoi.Any(x => x.SoLuong <= 0))
            return KetQuaThaoTac.Loi("Định lượng mỗi nguyên liệu phải lớn hơn 0.");

        // Mỗi mã phải là một nguyên liệu đã khai.
        var nguyenLieuHopLe = await _db.Products
            .Where(p => p.LoaiSanPham == LoaiSanPham.NguyenLieu)
            .Select(p => p.MaSanPham).ToListAsync(ct);
        var maSai = dsMoi.Select(x => x.Ma).Except(nguyenLieuHopLe).ToList();
        if (maSai.Count > 0)
            return KetQuaThaoTac.Loi("Không phải nguyên liệu hợp lệ: " + string.Join(", ", maSai));

        // Thay thế toàn bộ.
        _db.DinhMucNguyenLieus.RemoveRange(tp.DanhSachDinhMuc);
        tp.DanhSachDinhMuc = dsMoi
            .Select(x => new DinhMucNguyenLieu { MaNguyenLieu = x.Ma, SoLuong = x.SoLuong })
            .ToList();

        await _db.SaveChangesAsync(ct);
        return KetQuaThaoTac.Ok($"Đã lưu định mức ({dsMoi.Count} nguyên liệu) cho \"{tp.TenSanPham}\".");
    }
}
