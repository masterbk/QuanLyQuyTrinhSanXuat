using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý danh mục Cơ sở sản xuất/chế biến của tenant đang đăng nhập.
/// Đồng bộ sang HanoiCheck qua POST /supplier/facilities/merge.
/// </summary>
public class CoSoSanXuatService : IDanhMucService<Facility>
{
    private readonly AppDbContext _db;

    public CoSoSanXuatService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Facility>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.Facilities.AsNoTracking().OrderBy(f => f.MaCoSo).ToListAsync(ct);

    public Task<Facility?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.Facilities.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(Facility entity, CancellationToken ct = default)
    {
        entity.MaCoSo = entity.MaCoSo.Trim();

        if (await _db.Facilities.AnyAsync(f => f.MaCoSo == entity.MaCoSo, ct))
        {
            return KetQuaThaoTac.Loi($"Mã cơ sở \"{entity.MaCoSo}\" đã tồn tại.");
        }

        entity.TenCoSo = entity.TenCoSo.Trim();
        entity.DiaChi = entity.DiaChi?.Trim();

        _db.Facilities.Add(entity);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã thêm cơ sở \"{entity.TenCoSo}\".");
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(Facility entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy cơ sở cần sửa.");

        var maMoi = entity.MaCoSo.Trim();

        if (await _db.Facilities.AnyAsync(f => f.MaCoSo == maMoi && f.Id != entity.Id, ct))
        {
            return KetQuaThaoTac.Loi($"Mã cơ sở \"{maMoi}\" đã được dùng cho cơ sở khác.");
        }

        hienTai.MaCoSo = maMoi;
        hienTai.TenCoSo = entity.TenCoSo.Trim();
        hienTai.DiaChi = entity.DiaChi?.Trim();
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật cơ sở \"{hienTai.TenCoSo}\".");
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var coSo = await LayTheoIdAsync(id, ct);
        if (coSo is null) return KetQuaThaoTac.Loi("Không tìm thấy cơ sở cần xoá.");

        _db.Facilities.Remove(coSo);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá cơ sở \"{coSo.TenCoSo}\".");
    }
}
