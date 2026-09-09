using HCP.Domain.Constants;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Quản lý Đơn hàng (kèm xuất kho) của cơ sở. Đồng bộ qua POST /supplier/orders/merge.
///
/// Hai loại: food (dòng dùng ma_loai_sp, được xuất kho) và dish (dòng dùng ma_mon_an, KHÔNG
/// được xuất kho). Service chuẩn hoá và kiểm tra theo đúng loại để tránh HnC trả 422.
/// </summary>
public class DonHangService : IDanhMucService<Order>
{
    private readonly AppDbContext _db;
    private readonly ISyncOutboxWriter _outbox;

    public DonHangService(AppDbContext db, ISyncOutboxWriter outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    private IQueryable<Order> QueryDayDu() => _db.Orders
        .Include(o => o.Images)
        .Include(o => o.ChiTiet)
        .Include(o => o.XuatKho);

    public async Task<IReadOnlyList<Order>> LayTatCaAsync(CancellationToken ct = default) =>
        await QueryDayDu().AsNoTracking()
            .OrderByDescending(o => o.NgayDonHang).ThenByDescending(o => o.Id).ToListAsync(ct);

    public Task<Order?> LayTheoIdAsync(int id, CancellationToken ct = default) =>
        QueryDayDu().FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<KetQuaThaoTac> ThemAsync(Order entity, CancellationToken ct = default)
    {
        entity.MaDonHang = entity.MaDonHang.Trim();

        if (await _db.Orders.AnyAsync(o => o.MaDonHang == entity.MaDonHang, ct))
        {
            return KetQuaThaoTac.Loi($"Mã đơn hàng \"{entity.MaDonHang}\" đã tồn tại.");
        }

        ChuanHoa(entity);
        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        _db.Orders.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _outbox.ThemAsync("Order", entity.MaDonHang, HnCPayloadMapper.DonHang(entity), ct);

        return KetQuaThaoTac.Ok($"Đã thêm đơn hàng \"{entity.MaDonHang}\".");
    }

    public async Task<KetQuaThaoTac> CapNhatAsync(Order entity, CancellationToken ct = default)
    {
        var hienTai = await LayTheoIdAsync(entity.Id, ct);
        if (hienTai is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng cần sửa.");

        var maMoi = entity.MaDonHang.Trim();

        if (await _db.Orders.AnyAsync(o => o.MaDonHang == maMoi && o.Id != entity.Id, ct))
        {
            return KetQuaThaoTac.Loi($"Mã đơn hàng \"{maMoi}\" đã được dùng cho đơn khác.");
        }

        ChuanHoa(entity);
        var loi = await KiemTraAsync(entity, ct);
        if (loi is not null) return KetQuaThaoTac.Loi(loi);

        hienTai.MaDonHang = maMoi;
        hienTai.LoaiDonHang = entity.LoaiDonHang;
        hienTai.MaTruong = entity.MaTruong;
        hienTai.DiaChiNhan = entity.DiaChiNhan;
        hienTai.DiemGiao = entity.DiemGiao;
        hienTai.MaNguoiGiao = entity.MaNguoiGiao;
        hienTai.TrangThai = entity.TrangThai;
        hienTai.NgayDonHang = entity.NgayDonHang;
        hienTai.GhiChu = entity.GhiChu;
        hienTai.UpdatedAtUtc = DateTime.UtcNow;

        _db.OrderImages.RemoveRange(hienTai.Images);
        _db.OrderLines.RemoveRange(hienTai.ChiTiet);
        _db.OrderExports.RemoveRange(hienTai.XuatKho);

        hienTai.Images = entity.Images.Select(i => new OrderImage { PathFile = i.PathFile, SortOrder = i.SortOrder }).ToList();
        hienTai.ChiTiet = entity.ChiTiet.Select(SaoChepDong).ToList();
        hienTai.XuatKho = entity.XuatKho.Select(SaoChepXuatKho).ToList();

        await _db.SaveChangesAsync(ct);

        await _outbox.ThemAsync("Order", hienTai.MaDonHang, HnCPayloadMapper.DonHang(hienTai), ct);

        return KetQuaThaoTac.Ok($"Đã cập nhật đơn hàng \"{hienTai.MaDonHang}\".");
    }

    public async Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default)
    {
        var dh = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (dh is null) return KetQuaThaoTac.Loi("Không tìm thấy đơn hàng cần xoá.");

        _db.Orders.Remove(dh); // bảng con xoá theo cascade.
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã xoá đơn hàng \"{dh.MaDonHang}\".");
    }

    private async Task<string?> KiemTraAsync(Order o, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(o.MaTruong))
            return "Vui lòng nhập mã trường.";
        if (!LoaiDonHang.TatCa.Contains(o.LoaiDonHang))
            return "Loại đơn hàng không hợp lệ.";
        if (!TrangThaiDonHang.HopLe(o.TrangThai))
            return "Trạng thái đơn hàng không hợp lệ.";
        if (o.ChiTiet.Count == 0)
            return "Đơn hàng phải có ít nhất một dòng đặt hàng.";
        if (o.ChiTiet.Any(l => l.SoLuong < 0))
            return "Số lượng đặt không được âm.";

        var laDonMon = o.LoaiDonHang == LoaiDonHang.Dish;

        if (laDonMon)
        {
            if (o.ChiTiet.Any(l => string.IsNullOrWhiteSpace(l.MaMonAn)))
                return "Đơn món ăn: mỗi dòng phải chọn món ăn.";

            var monTonTai = await _db.Dishes.Select(d => d.MaMonAn).ToListAsync(ct);
            var monSai = o.ChiTiet.Select(l => l.MaMonAn!.Trim()).Except(monTonTai).ToList();
            if (monSai.Count > 0)
                return "Món ăn không có trong danh mục: " + string.Join(", ", monSai);

            // Đơn dish không được gửi xuat_kho (HnC trả 422). ChuanHoa đã tự xoá xuat_kho cho
            // đơn dish nên tới đây danh sách luôn rỗng - không cần chặn thêm.
        }
        else
        {
            // Đơn food: dòng lưu MÃ THÀNH PHẨM (SKU) nội bộ. ma_loai_sp KHÔNG nhập tay mà được
            // SUY từ thành phẩm - nên chỉ cần chọn đúng thành phẩm là đủ để sau này đẩy lên HnC.
            if (o.ChiTiet.Any(l => string.IsNullOrWhiteSpace(l.MaSanPham)))
                return "Đơn thực phẩm: mỗi dòng phải chọn thành phẩm.";

            var skus = o.ChiTiet.Select(l => l.MaSanPham!.Trim()).Distinct().ToList();
            var loaiTheoSku = await _db.Products
                .Where(p => skus.Contains(p.MaSanPham))
                .Select(p => new { p.MaSanPham, p.MaLoaiSp })
                .ToDictionaryAsync(p => p.MaSanPham, p => p.MaLoaiSp, ct);

            foreach (var l in o.ChiTiet)
            {
                var sku = l.MaSanPham!.Trim();
                if (!loaiTheoSku.TryGetValue(sku, out var maLoai))
                    return $"Thành phẩm \"{sku}\" không có trong danh mục thực phẩm.";
                if (string.IsNullOrWhiteSpace(maLoai))
                    return $"Thành phẩm \"{sku}\" chưa khai Mã loại SP (ma_loai_sp) - bổ sung ở màn Thực phẩm để đẩy đơn lên HanoiCheck.";
                l.MaLoaiSp = maLoai;  // suy ma_loai_sp từ thành phẩm cho đúng trường HnC cần
            }
        }

        // Xuất kho (chỉ có ở đơn food): kho/thực phẩm/lô phải tồn tại.
        if (o.XuatKho.Count > 0)
        {
            var khoTonTai = await _db.Warehouses.Select(w => w.MaKho).ToListAsync(ct);
            var spTonTai = await _db.Products.Select(p => p.MaSanPham).ToListAsync(ct);
            var loTonTai = await _db.Batches.Select(b => b.MaLo).ToListAsync(ct);
            foreach (var x in o.XuatKho)
            {
                if (string.IsNullOrWhiteSpace(x.MaSanPham) || string.IsNullOrWhiteSpace(x.MaKho)
                    || string.IsNullOrWhiteSpace(x.MaLo))
                    return "Mỗi dòng xuất kho phải có mã thực phẩm, mã kho và mã lô.";
                if (x.SoLuong < 0)
                    return "Số lượng xuất kho không được âm.";
                if (!khoTonTai.Contains(x.MaKho.Trim()))
                    return $"Kho \"{x.MaKho}\" không có trong danh mục.";
                if (!spTonTai.Contains(x.MaSanPham.Trim()))
                    return $"Thực phẩm \"{x.MaSanPham}\" không có trong danh mục.";
                if (!loTonTai.Contains(x.MaLo.Trim()))
                    return $"Lô \"{x.MaLo}\" không có trong danh mục lô sản xuất.";
            }
        }

        // Ảnh: tối đa 3, sort_order 1..3 không trùng.
        if (o.Images.Count > 3)
            return "Chỉ được tối đa 3 ảnh cho đơn hàng.";
        if (o.Images.Select(i => i.SortOrder).Distinct().Count() != o.Images.Count)
            return "Thứ tự ảnh (sort_order) không được trùng nhau.";

        if (!string.IsNullOrWhiteSpace(o.MaNguoiGiao))
        {
            var ng = o.MaNguoiGiao.Trim();
            if (!await _db.Staff.AnyAsync(s => s.MaNhanSu == ng, ct))
                return $"Người giao \"{ng}\" không có trong danh mục nhân sự.";
        }

        return null;
    }

    /// <summary>Chuẩn hoá theo loại đơn: xoá khoá không phù hợp, và bỏ xuất kho nếu là đơn món ăn.</summary>
    private static void ChuanHoa(Order o)
    {
        o.MaTruong = o.MaTruong.Trim();
        o.DiaChiNhan = o.DiaChiNhan?.Trim();
        o.DiemGiao = o.DiemGiao?.Trim();
        o.MaNguoiGiao = string.IsNullOrWhiteSpace(o.MaNguoiGiao) ? null : o.MaNguoiGiao.Trim();
        o.GhiChu = o.GhiChu?.Trim();

        var laDonMon = o.LoaiDonHang == LoaiDonHang.Dish;
        foreach (var l in o.ChiTiet)
        {
            // Đơn món: chỉ giữ ma_mon_an. Đơn food: giữ MÃ THÀNH PHẨM (SKU); ma_loai_sp để trống
            // ở đây, sẽ được KiemTraAsync suy từ thành phẩm đã chọn.
            if (laDonMon) { l.MaLoaiSp = null; l.MaSanPham = null; l.MaMonAn = l.MaMonAn?.Trim(); }
            else { l.MaMonAn = null; l.MaSanPham = l.MaSanPham?.Trim(); }
            l.PathFile = string.IsNullOrWhiteSpace(l.PathFile) ? null : l.PathFile.Trim();
        }

        if (laDonMon) o.XuatKho.Clear();
    }

    private static OrderLine SaoChepDong(OrderLine l) => new()
    {
        MaSanPham = l.MaSanPham,
        MaLoaiSp = l.MaLoaiSp,
        MaMonAn = l.MaMonAn,
        SoLuong = l.SoLuong,
        PathFile = l.PathFile
    };

    private static OrderExport SaoChepXuatKho(OrderExport x) => new()
    {
        MaXuatKho = x.MaXuatKho?.Trim(),
        MaLoaiSp = x.MaLoaiSp?.Trim(),
        MaSanPham = x.MaSanPham.Trim(),
        MaKho = x.MaKho.Trim(),
        MaLo = x.MaLo.Trim(),
        SoLuong = x.SoLuong
    };
}
