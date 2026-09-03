using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý Quy trình sản xuất (chuỗi khâu có thứ tự) của cơ sở đang đăng nhập.
/// Đồng bộ sang HanoiCheck qua POST /supplier/processes/merge.
/// </summary>
public class QuyTrinhSanXuatService : IDanhMucService<ProductionProcess>
{
    private readonly AppDbContext _db;

    public QuyTrinhSanXuatService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProductionProcess>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.ProductionProcesses
            .AsNoTracking()
            .Include(p => p.DanhSachKhau)
            .OrderBy(p => p.MaQuyTrinh)
            .ToListAsync(ct);

    public Task<ProductionProcess?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.ProductionProcesses
            .Include(p => p.DanhSachKhau)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(ProductionProcess entity, CancellationToken ct = default)
    {
        entity.MaQuyTrinh = entity.MaQuyTrinh.Trim();

        if (await _db.ProductionProcesses.AnyAsync(p => p.MaQuyTrinh == entity.MaQuyTrinh, ct))
        {
            return KetQuaThaoTac.Loi($"Mã quy trình \"{entity.MaQuyTrinh}\" đã tồn tại.");
        }

        var loi = await KiemTraDanhSachKhauAsync(entity.DanhSachKhau, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        entity.TenQuyTrinh = entity.TenQuyTrinh.Trim();
        ChuanHoaThuTu(entity.DanhSachKhau);

        _db.ProductionProcesses.Add(entity);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã thêm quy trình \"{entity.TenQuyTrinh}\".");
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(ProductionProcess entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy quy trình cần sửa.");

        var maMoi = entity.MaQuyTrinh.Trim();

        if (await _db.ProductionProcesses.AnyAsync(p => p.MaQuyTrinh == maMoi && p.Id != entity.Id, ct))
        {
            return KetQuaThaoTac.Loi($"Mã quy trình \"{maMoi}\" đã được dùng cho quy trình khác.");
        }

        var loi = await KiemTraDanhSachKhauAsync(entity.DanhSachKhau, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        hienTai.MaQuyTrinh = maMoi;
        hienTai.TenQuyTrinh = entity.TenQuyTrinh.Trim();
        hienTai.MaDanhMucThucPham = entity.MaDanhMucThucPham;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        // Thay toàn bộ danh sách khâu: đơn giản và tránh sai lệch thứ tự khi
        // người dùng vừa thêm, vừa xoá, vừa đổi vị trí trong cùng một lần sửa.
        _db.ProcessStepLines.RemoveRange(hienTai.DanhSachKhau);

        var danhSachMoi = entity.DanhSachKhau
            .Select(l => new ProcessStepLine { MaKhau = l.MaKhau.Trim(), ThuTu = l.ThuTu })
            .ToList();

        ChuanHoaThuTu(danhSachMoi);
        hienTai.DanhSachKhau = danhSachMoi;

        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật quy trình \"{hienTai.TenQuyTrinh}\".");
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var quyTrinh = await LayTheoIdAsync(id, ct);
        if (quyTrinh is null) return KetQuaThaoTac.Loi("Không tìm thấy quy trình cần xoá.");

        _db.ProductionProcesses.Remove(quyTrinh);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá quy trình \"{quyTrinh.TenQuyTrinh}\".");
    }

    /// <summary>
    /// Đặc tả yêu cầu danh_sach_khau tối thiểu 1 phần tử và mọi ma_khau phải tồn tại.
    /// Kiểm tra tại đây để người dùng sửa ngay, thay vì chờ HanoiCheck trả 422.
    /// </summary>
    private async Task<string?> KiemTraDanhSachKhauAsync(List<ProcessStepLine> danhSach,
                                                         CancellationToken ct)
    {
        if (danhSach.Count == 0)
        {
            return "Quy trình phải có ít nhất một khâu sản xuất.";
        }

        var maKhauTrongQuyTrinh = danhSach.Select(l => l.MaKhau.Trim()).ToList();

        var maTrung = maKhauTrongQuyTrinh
            .GroupBy(m => m, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (maTrung.Count > 0)
        {
            return "Khâu bị lặp trong quy trình: " + string.Join(", ", maTrung);
        }

        var maKhauHopLe = await _db.ProductionSteps
            .Select(s => s.MaKhau)
            .ToListAsync(ct);

        var maKhongTonTai = maKhauTrongQuyTrinh
            .Except(maKhauHopLe, StringComparer.Ordinal)
            .ToList();

        if (maKhongTonTai.Count > 0)
        {
            return "Mã khâu không có trong danh mục: " + string.Join(", ", maKhongTonTai);
        }

        return null;
    }

    /// <summary>Đánh lại thứ tự 1..n theo đúng vị trí hiện tại của danh sách.</summary>
    private static void ChuanHoaThuTu(List<ProcessStepLine> danhSach)
    {
        for (var i = 0; i < danhSach.Count; i++) danhSach[i].ThuTu = i + 1;
    }
}
