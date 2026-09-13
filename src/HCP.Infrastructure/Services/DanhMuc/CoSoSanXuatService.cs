using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý danh mục Cơ sở sản xuất/chế biến của tenant đang đăng nhập.
/// Đồng bộ sang HanoiCheck qua POST /supplier/facilities/merge.
/// </summary>
public class CoSoSanXuatService : IDanhMucService<Facility>
{
    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;
    private readonly IMaTuSinhService _maTuSinh;

    public CoSoSanXuatService(AppDbContext db, ISyncOutboxWriter outbox, IMaTuSinhService maTuSinh)
    {
        _db = db;
        _outbox = outbox;
        _maTuSinh = maTuSinh;
    }

    public async Task<IReadOnlyList<Facility>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.Facilities.AsNoTracking().OrderBy(f => f.MaCoSo).ToListAsync(ct);

    public Task<Facility?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.Facilities.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(Facility entity, CancellationToken ct = default)
    {
        entity.TenCoSo = entity.TenCoSo.Trim();
        entity.DiaChi = entity.DiaChi?.Trim();

        // Mã do hệ thống cấp (CS-0001...), bỏ qua mọi mã gửi lên.
        entity.MaCoSo = await _maTuSinh.SinhAsync(LoaiMaTuSinh.CoSo, ct: ct);

        _db.Facilities.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(entity, ct);

        return KetQuaThaoTac.Ok($"Đã thêm cơ sở \"{entity.TenCoSo}\" (mã {entity.MaCoSo}).").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(Facility entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy cơ sở cần sửa.");

        // Mã cơ sở KHÔNG sửa được: là khoá đối chiếu với HanoiCheck và được lô/khâu tham chiếu.
        hienTai.TenCoSo = entity.TenCoSo.Trim();
        hienTai.DiaChi = entity.DiaChi?.Trim();
        hienTai.DongBoHnC = entity.DongBoHnC;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(hienTai, ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật cơ sở \"{hienTai.TenCoSo}\".").KemGhiChu(dongBo);
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
