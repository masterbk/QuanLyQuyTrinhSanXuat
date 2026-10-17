using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
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

    /// <inheritdoc />
    public Task<Product?> LayDeSuaAsync(int id, CancellationToken ct = default) =>
        _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

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

        // Chỉ thành phẩm mới đồng bộ sang HanoiCheck (foods/merge); nguyên liệu quản lý nội bộ.
        var dongBo = await _outbox.GuiAsync(entity, ct);   // chỉ thành phẩm được gửi

        return KetQuaThaoTac.Ok($"Đã thêm \"{entity.TenSanPham}\".").KemGhiChu(dongBo);
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
        hienTai.LoaiSanPham = entity.LoaiSanPham;
        hienTai.DonViTinh = entity.DonViTinh;
        hienTai.TonToiThieu = entity.TonToiThieu;
        hienTai.DonViTinhHnCId = entity.DonViTinhHnCId;
        hienTai.QuyDoiGam = entity.QuyDoiGam;
        hienTai.HeSoThaiBoPhanTram = entity.HeSoThaiBoPhanTram;
        hienTai.HeSoThaiBoGam = entity.HeSoThaiBoGam;
        hienTai.DongBoHnC = entity.DongBoHnC;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var dongBo = await _outbox.GuiAsync(hienTai, ct);   // chỉ thành phẩm được gửi

        return KetQuaThaoTac.Ok($"Đã cập nhật \"{hienTai.TenSanPham}\".").KemGhiChu(dongBo);
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
        // Mã loại (danh mục thực phẩm chuẩn) chỉ bắt buộc với THÀNH PHẨM (gửi HnC). Nguyên liệu
        // quản lý nội bộ nên không cần.
        // Chỉ bắt buộc khi thành phẩm này thật sự gửi HanoiCheck (công tắc tổng bật + tick đồng bộ).
        if (p.LoaiSanPham == LoaiSanPham.ThanhPham && p.DongBoHnC && await _outbox.DangBatAsync(ct))
        {
            if (string.IsNullOrWhiteSpace(p.MaLoaiSp))
                return "Vui lòng chọn loại thực phẩm (mã danh mục thực phẩm chuẩn).";

            // HanoiCheck thực tế bắt buộc ma_quy_trinh (422 "Mã quy trình sản xuất mẫu là bắt buộc"),
            // dù tài liệu v2.2 ghi "không bắt buộc".
            if (string.IsNullOrWhiteSpace(p.MaQuyTrinh))
                return "Vui lòng chọn quy trình sản xuất - HanoiCheck bắt buộc quy trình mẫu cho thực phẩm đồng bộ. "
                       + "Nếu thực phẩm này không gửi HanoiCheck thì bỏ tick \"Đồng bộ HanoiCheck\".";
        }

        // Cấu hình đơn vị tính (food_config): measure_id bắt buộc khi có food_config, nên các chỉ số còn lại
        // chỉ có nghĩa khi đã chọn đơn vị.
        if (p.DonViTinhHnCId is null && (p.QuyDoiGam ?? p.HeSoThaiBoPhanTram ?? p.HeSoThaiBoGam) is not null)
            return "Vui lòng chọn đơn vị tính HanoiCheck trước khi khai quy đổi gam / hệ số thải bỏ.";
        if (p.QuyDoiGam is <= 0)
            return "Quy đổi ra gam phải lớn hơn 0.";
        if (p.HeSoThaiBoPhanTram is < 0 or > 100)
            return "Hệ số thải bỏ (%) phải từ 0 đến 100.";
        if (p.HeSoThaiBoGam is < 0)
            return "Hệ số thải bỏ (gam) không được âm.";

        if (!string.IsNullOrWhiteSpace(p.MaQuyTrinh))
        {
            var maQuyTrinh = p.MaQuyTrinh.Trim();
            var loaiQuyTrinh = await _db.ProductionProcesses.Where(q => q.MaQuyTrinh == maQuyTrinh)
                .Select(q => q.LoaiApDung).FirstOrDefaultAsync(ct);
            if (loaiQuyTrinh is null)
            {
                return $"Quy trình \"{maQuyTrinh}\" không có trong danh mục quy trình của cơ sở.";
            }

            // HanoiCheck: thực phẩm chỉ gắn quy trình loại food.
            if (loaiQuyTrinh == LoaiQuyTrinh.MonAn)
            {
                return $"Quy trình \"{maQuyTrinh}\" là quy trình chế biến món ăn, không dùng cho thực phẩm.";
            }
        }

        return null;
    }

    private static void ChuanHoa(Product p)
    {
        p.TenSanPham = p.TenSanPham.Trim();
        p.MaLoaiSp = p.MaLoaiSp?.Trim() ?? "";
        p.MaThucPhamChuan = p.MaThucPhamChuan?.Trim();
        p.Gtin = p.Gtin?.Trim();
        p.QuocGia = p.QuocGia?.Trim();
        p.MoTa = p.MoTa?.Trim();
        p.MaQuyTrinh = string.IsNullOrWhiteSpace(p.MaQuyTrinh) ? null : p.MaQuyTrinh.Trim();
    }
}
