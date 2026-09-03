using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý danh mục Khâu sản xuất của cơ sở đang đăng nhập.
/// Đồng bộ sang HanoiCheck qua POST /supplier/steps/merge.
/// </summary>
public class KhauSanXuatService : IDanhMucService<ProductionStep>
{
    private readonly AppDbContext _db;

    public KhauSanXuatService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProductionStep>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.ProductionSteps.AsNoTracking().OrderBy(s => s.MaKhau).ToListAsync(ct);

    public Task<ProductionStep?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.ProductionSteps.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(ProductionStep entity, CancellationToken ct = default)
    {
        entity.MaKhau = entity.MaKhau.Trim();

        if (await _db.ProductionSteps.AnyAsync(s => s.MaKhau == entity.MaKhau, ct))
        {
            return KetQuaThaoTac.Loi($"Mã khâu \"{entity.MaKhau}\" đã tồn tại.");
        }

        entity.TenKhau = entity.TenKhau.Trim();
        entity.GhiChu = entity.GhiChu?.Trim();

        _db.ProductionSteps.Add(entity);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã thêm khâu \"{entity.TenKhau}\".");
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(ProductionStep entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy khâu sản xuất cần sửa.");

        var maMoi = entity.MaKhau.Trim();

        if (await _db.ProductionSteps.AnyAsync(s => s.MaKhau == maMoi && s.Id != entity.Id, ct))
        {
            return KetQuaThaoTac.Loi($"Mã khâu \"{maMoi}\" đã được dùng cho khâu khác.");
        }

        // Đổi mã khâu sẽ làm các quy trình đang tham chiếu mã cũ trở nên sai.
        // Cập nhật đồng thời để không gửi sang HanoiCheck mã khâu không tồn tại (lỗi 422).
        if (!string.Equals(hienTai.MaKhau, maMoi, StringComparison.Ordinal))
        {
            var maCu = hienTai.MaKhau;
            var dongLienQuan = await _db.ProcessStepLines
                .Where(l => l.MaKhau == maCu)
                .ToListAsync(ct);

            foreach (var dong in dongLienQuan) dong.MaKhau = maMoi;
        }

        hienTai.MaKhau = maMoi;
        hienTai.TenKhau = entity.TenKhau.Trim();
        hienTai.GhiChu = entity.GhiChu?.Trim();
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật khâu \"{hienTai.TenKhau}\".");
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
