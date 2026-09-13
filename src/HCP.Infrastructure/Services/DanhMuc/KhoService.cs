using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý danh mục Kho của cơ sở đang đăng nhập.
/// Đồng bộ sang HanoiCheck qua POST /supplier/warehouses/merge.
/// </summary>
public class KhoService : IDanhMucService<Warehouse>
{
    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;

    public KhoService(AppDbContext db, ISyncOutboxWriter outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<IReadOnlyList<Warehouse>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.Warehouses.AsNoTracking().OrderBy(w => w.MaKho).ToListAsync(ct);

    public Task<Warehouse?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(Warehouse entity, CancellationToken ct = default)
    {
        entity.MaKho = entity.MaKho.Trim();

        // Chặn trùng mã ngay tại đây để báo lỗi thân thiện, thay vì để unique index
        // ném ra DbUpdateException khó hiểu với người dùng.
        if (await _db.Warehouses.AnyAsync(w => w.MaKho == entity.MaKho, ct))
        {
            return KetQuaThaoTac.Loi($"Mã kho \"{entity.MaKho}\" đã tồn tại.");
        }

        entity.TenKho = entity.TenKho.Trim();
        entity.DiaChi = entity.DiaChi.Trim();

        _db.Warehouses.Add(entity);
        await _db.SaveChangesAsync(ct);

        // Lưu xong thì đưa vào hàng đợi để job nền tự đồng bộ sang HanoiCheck.
        var dongBo = await _outbox.GuiAsync(entity, ct);

        return KetQuaThaoTac.Ok($"Đã thêm kho \"{entity.TenKho}\".").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(Warehouse entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy kho cần sửa.");

        var maMoi = entity.MaKho.Trim();

        if (await _db.Warehouses.AnyAsync(w => w.MaKho == maMoi && w.Id != entity.Id, ct))
        {
            return KetQuaThaoTac.Loi($"Mã kho \"{maMoi}\" đã được dùng cho kho khác.");
        }

        hienTai.MaKho = maMoi;
        hienTai.TenKho = entity.TenKho.Trim();
        hienTai.DiaChi = entity.DiaChi.Trim();
        hienTai.DienTich = entity.DienTich;
        hienTai.DongBoHnC = entity.DongBoHnC;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // merge của HnC là upsert theo ma_kho, nên sửa cũng gửi lại cả bản ghi.
        var dongBo = await _outbox.GuiAsync(hienTai, ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật kho \"{hienTai.TenKho}\".").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var kho = await LayTheoIdAsync(id, ct);
        if (kho is null) return KetQuaThaoTac.Loi("Không tìm thấy kho cần xoá.");

        _db.Warehouses.Remove(kho);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá kho \"{kho.TenKho}\".");
    }
}
