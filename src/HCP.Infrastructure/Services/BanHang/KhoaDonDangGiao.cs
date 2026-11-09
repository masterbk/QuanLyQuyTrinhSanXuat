using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.BanHang;

/// <summary>
/// Đặc tả HanoiCheck v2.7.1 (Phần III mục 8): dữ liệu đang dùng cho đơn hàng ở "Đang giao" / "Đã giao hàng"
/// mà nhà trường CHƯA xác nhận nhận hàng bị HnC khoá một phần - gửi lại là 422. Chặn ngay tại chỗ sửa/xoá để
/// người dùng thấy thông báo rõ ràng thay vì lỗi đồng bộ im lặng sau đó.
///
/// Khoá tự mở khi đơn chuyển "Giao hàng thành công", "Trả hàng" hoặc "Huỷ".
///
/// Phạm vi khoá (chỉ những gì HnC khoá, phần còn lại sửa bình thường):
/// - Thực phẩm: không đổi đơn vị tính (food_config.measure_id, hoặc đơn vị mặc định của ma_loai_sp).
/// - Lô nhập kho: không đổi ma_san_pham, không bỏ kho đã xuất hàng khỏi danh_sach_kho, không xoá lô.
/// - Món ăn: không thêm/bớt nguyên liệu hay đổi định lượng, không xoá món.
/// - Kho: không xoá kho đang dùng cho đơn.
/// </summary>
public static class KhoaDonDangGiao
{
    /// <summary>Trạng thái trên HanoiCheck cho biết nhà trường đã chốt đơn - khoá mở.</summary>
    private static readonly string[] HnCDaChot = { "GIAO_HANG_THANH_CONG", "TRA_HANG", "HUY", "TU_CHOI" };

    /// <summary>
    /// Đơn HanoiCheck đang khoá dữ liệu: đã xuất kho (chờ giao / đang giao / đã giao) mà trạng thái bên HnC
    /// chưa phải trạng thái chốt. Đơn nội bộ không gửi HnC nên không khoá.
    /// </summary>
    public static IQueryable<DonHangBan> DonDangKhoa(AppDbContext db) =>
        db.DonHangBans.AsNoTracking()
            .Where(d => d.Nguon == NguonDonHang.HanoiCheck && d.MaDonHnC != null
                        && (d.TrangThai == TrangThaiDonHangBan.ChoGiaoHang
                            || d.TrangThai == TrangThaiDonHangBan.DangGiao
                            || d.TrangThai == TrangThaiDonHangBan.DaGiao)
                        && (d.TrangThaiHnC == null || !HnCDaChot.Contains(d.TrangThaiHnC)));

    /// <summary>Mã đơn đang khoá thực phẩm này (bán trực tiếp, hoặc là nguyên liệu đã xuất cho dòng món ăn).</summary>
    public static async Task<string?> DonKhoaThucPhamAsync(AppDbContext db, string? maSanPham, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(maSanPham)) return null;
        var ma = maSanPham.Trim();

        return await (from dong in db.DonHangBanDongs.AsNoTracking()
                      join don in DonDangKhoa(db) on dong.DonHangBanId equals don.Id
                      where (!dong.LaMonAn && dong.MaThanhPham == ma)
                            || dong.XuatLo.Any(x => x.MaNguyenLieu == ma)
                      select don.MaDonHang).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Mã đơn đang khoá lô này (lô đã được xuất kho cho đơn). Truyền <paramref name="maKho"/> để chỉ xét các
    /// đơn xuất lô từ đúng kho đó - dùng khi kiểm việc bỏ một kho khỏi danh_sach_kho của lô.
    /// </summary>
    public static async Task<string?> DonKhoaLoAsync(AppDbContext db, string? maLo, CancellationToken ct,
                                                     string? maKho = null)
    {
        if (string.IsNullOrWhiteSpace(maLo)) return null;
        var ma = maLo.Trim();
        var kho = maKho?.Trim();

        return await (from xuat in db.DonHangBanXuatLos.AsNoTracking()
                      join dong in db.DonHangBanDongs.AsNoTracking() on xuat.DonHangBanDongId equals dong.Id
                      join don in DonDangKhoa(db) on dong.DonHangBanId equals don.Id
                      where xuat.MaLo == ma && (kho == null || don.MaKho == kho)
                      select don.MaDonHang).FirstOrDefaultAsync(ct);
    }

    /// <summary>Mã đơn đang khoá món ăn này.</summary>
    public static async Task<string?> DonKhoaMonAnAsync(AppDbContext db, string? maMonAn, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(maMonAn)) return null;
        var ma = maMonAn.Trim();

        return await (from dong in db.DonHangBanDongs.AsNoTracking()
                      join don in DonDangKhoa(db) on dong.DonHangBanId equals don.Id
                      where dong.LaMonAn && dong.MaThanhPham == ma
                      select don.MaDonHang).FirstOrDefaultAsync(ct);
    }

    /// <summary>Mã đơn đang khoá kho này (kho xuất hàng của đơn).</summary>
    public static async Task<string?> DonKhoaKhoAsync(AppDbContext db, string? maKho, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(maKho)) return null;
        var ma = maKho.Trim();

        return await DonDangKhoa(db).Where(d => d.MaKho == ma).Select(d => d.MaDonHang).FirstOrDefaultAsync(ct);
    }

    /// <summary>Thông báo theo đúng mẫu của HanoiCheck v2.7.1 để người dùng đọc là hiểu ngay.</summary>
    public static string ThongBao(string duLieu, string maDon, string thaoTac) =>
        $"{duLieu} đang thuộc đơn hàng đang giao chưa được trường xác nhận ({maDon}) nên không được {thaoTac}. "
        + "Vui lòng thực hiện sau khi trường xác nhận nhận hàng.";
}
