using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý danh mục Khâu sản xuất của cơ sở đang đăng nhập.
/// Đồng bộ sang HanoiCheck qua POST /supplier/steps/merge.
/// </summary>
public class KhauSanXuatService : IDanhMucService<ProductionStep>
{
    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;
    private readonly IMaTuSinhService _maTuSinh;

    public KhauSanXuatService(AppDbContext db, ISyncOutboxWriter outbox, IMaTuSinhService maTuSinh)
    {
        _db = db;
        _outbox = outbox;
        _maTuSinh = maTuSinh;
    }

    public async Task<IReadOnlyList<ProductionStep>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.ProductionSteps.AsNoTracking().OrderBy(s => s.MaKhau).ToListAsync(ct);

    public Task<ProductionStep?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.ProductionSteps.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(ProductionStep entity, CancellationToken ct = default)
    {
        entity.TenKhau = entity.TenKhau.Trim();
        entity.GhiChu = entity.GhiChu?.Trim();

        // Mã do hệ thống cấp (KHAU-0001...), bỏ qua mọi mã gửi lên.
        entity.MaKhau = await _maTuSinh.SinhAsync(LoaiMaTuSinh.Khau, ct: ct);

        _db.ProductionSteps.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(entity, ct);

        return KetQuaThaoTac.Ok($"Đã thêm khâu \"{entity.TenKhau}\" (mã {entity.MaKhau}).").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(ProductionStep entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy khâu sản xuất cần sửa.");

        // Mã khâu KHÔNG sửa được: quy trình, lô, lệnh sản xuất và HanoiCheck đều tham chiếu theo mã.
        hienTai.TenKhau = entity.TenKhau.Trim();
        hienTai.GhiChu = entity.GhiChu?.Trim();
        hienTai.DongBoHnC = entity.DongBoHnC;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(hienTai, ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật khâu \"{hienTai.TenKhau}\".").KemGhiChu(dongBo);
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var khau = await LayTheoIdAsync(id, ct);
        if (khau is null) return KetQuaThaoTac.Loi("Không tìm thấy khâu sản xuất cần xoá.");

        // Không cho xoá khâu đang được quy trình sử dụng: HanoiCheck sẽ trả 422
        // "Có mã khâu không tồn tại" khi đồng bộ quy trình đó.
        var soQuyTrinhDungKhau = await _db.ProcessStepLines
            .CountAsync(l => l.MaKhau == khau.MaKhau, ct);

        if (soQuyTrinhDungKhau > 0)
        {
            return KetQuaThaoTac.Loi(
                $"Không xoá được: khâu \"{khau.TenKhau}\" đang được dùng trong {soQuyTrinhDungKhau} quy trình sản xuất.");
        }

        _db.ProductionSteps.Remove(khau);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá khâu \"{khau.TenKhau}\".");
    }
}
