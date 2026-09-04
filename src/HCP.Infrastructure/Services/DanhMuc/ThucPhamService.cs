using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý danh mục Thực phẩm/SKU của cơ sở đang đăng nhập.
/// Đồng bộ sang HanoiCheck qua POST /supplier/foods/merge.
/// </summary>
public class ThucPhamService : IDanhMucService<Product>
{
    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;

    public ThucPhamService(AppDbContext db, ISyncOutboxWriter outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<IReadOnlyList<Product>> LayTatCaAsync(CancellationToken ct = default) =>
        await _db.Products.AsNoTracking().OrderBy(p => p.MaSanPham).ToListAsync(ct);

    public Task<Product?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(Product entity, CancellationToken ct = default)
    {
        entity.MaSanPham = entity.MaSanPham.Trim();

        if (await _db.Products.AnyAsync(p => p.MaSanPham == entity.MaSanPham, ct))
        {
            return KetQuaThaoTac.Loi($"Mã thực phẩm \"{entity.MaSanPham}\" đã tồn tại.");
        }

        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        _db.Products.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _outbox.ThemAsync("Product", entity.MaSanPham, HnCPayloadMapper.ThucPham(entity), ct);

        return KetQuaThaoTac.Ok($"Đã thêm thực phẩm \"{entity.TenSanPham}\".");
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(Product entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy thực phẩm cần sửa.");

        var maMoi = entity.MaSanPham.Trim();

        if (await _db.Products.AnyAsync(p => p.MaSanPham == maMoi && p.Id != entity.Id, ct))
        {
            return KetQuaThaoTac.Loi($"Mã thực phẩm \"{maMoi}\" đã được dùng cho sản phẩm khác.");
        }

        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        ChuanHoa(entity);

        hienTai.MaSanPham = maMoi;
        hienTai.TenSanPham = entity.TenSanPham;
        hienTai.MaLoaiSp = entity.MaLoaiSp;
        hienTai.MaThucPhamChuan = entity.MaThucPhamChuan;
        hienTai.Gtin = entity.Gtin;
        hienTai.QuocGia = entity.QuocGia;
        hienTai.MoTa = entity.MoTa;
        hienTai.MaQuyTrinh = entity.MaQuyTrinh;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _outbox.ThemAsync("Product", hienTai.MaSanPham, HnCPayloadMapper.ThucPham(hienTai), ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật thực phẩm \"{hienTai.TenSanPham}\".");
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var sp = await LayTheoIdAsync(id, ct);
        if (sp is null) return KetQuaThaoTac.Loi("Không tìm thấy thực phẩm cần xoá.");

        _db.Products.Remove(sp);
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá thực phẩm \"{sp.TenSanPham}\".");
    }

    /// <summary>
    /// Kiểm tra ràng buộc để không bị HanoiCheck trả 422: bắt buộc mã loại (danh mục thực phẩm
    /// chuẩn); nếu có khai mã quy trình thì quy trình đó phải tồn tại trong cơ sở.
    /// </summary>
    private async Task<string?> KiemTraAsync(Product p, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.MaLoaiSp))
        {
            return "Vui lòng chọn loại thực phẩm (mã danh mục thực phẩm chuẩn).";
        }

        if (!string.IsNullOrWhiteSpace(p.MaQuyTrinh))
        {
            var maQuyTrinh = p.MaQuyTrinh.Trim();
            if (!await _db.ProductionProcesses.AnyAsync(q => q.MaQuyTrinh == maQuyTrinh, ct))
            {
                return $"Quy trình \"{maQuyTrinh}\" không có trong danh mục quy trình của cơ sở.";
            }
        }

        return null;
    }

    private static void ChuanHoa(Product p)
    {
        p.TenSanPham = p.TenSanPham.Trim();
        p.MaLoaiSp = p.MaLoaiSp.Trim();
        p.MaThucPhamChuan = p.MaThucPhamChuan?.Trim();
        p.Gtin = p.Gtin?.Trim();
        p.QuocGia = p.QuocGia?.Trim();
        p.MoTa = p.MoTa?.Trim();
        p.MaQuyTrinh = string.IsNullOrWhiteSpace(p.MaQuyTrinh) ? null : p.MaQuyTrinh.Trim();
    }
}
