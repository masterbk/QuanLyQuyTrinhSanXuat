using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.SuatAn;

/// <summary>Một món trong thực đơn, kèm bữa ăn phục vụ.</summary>
public sealed record MonTrongThucDon(string MaMonAn, string TenMonAn, int BuoiAnId, string TenBuoiAn, int ThuTuBuoiAn);

/// <summary>Một thực đơn áp dụng cho ngày đang xem.</summary>
public sealed record ThucDonTrongNgay(
    int Id,
    string MaThucDon,
    string TenThucDon,
    string TenNhomTuoi,
    IReadOnlyList<string> TenTruong,   // rỗng = dùng chung mọi trường
    IReadOnlyList<MonTrongThucDon> DanhSachMon);

/// <summary>Số suất của một món trong ngày.</summary>
public sealed record SuatTheoMon(string MaMonAn, string TenMonAn, decimal SoSuat);

/// <summary>Số suất phải nấu cho một khách hàng (trường) trong ngày.</summary>
public sealed record SuatTheoKhach(
    string MaKhachHang,
    string TenKhachHang,
    decimal TongSuat,
    IReadOnlyList<SuatTheoMon> DanhSachMon);

/// <summary>
/// Bản tin suất ăn của một ngày: hôm nay nấu món gì (thực đơn theo thứ) và phải nấu bao nhiêu suất
/// cho từng trường (gộp từ đơn hàng bán có dòng món ăn, theo ngày hẹn giao).
/// </summary>
public sealed record SuatAnNgay(
    DateOnly Ngay,
    int Thu,                                    // 0 = Chủ nhật, 1 = Thứ 2 ... (đúng quy ước thu_ap_dung của HanoiCheck)
    string TenThu,
    IReadOnlyList<ThucDonTrongNgay> ThucDon,
    IReadOnlyList<SuatTheoKhach> TheoKhachHang,
    decimal TongSuat,
    IReadOnlyList<SuatTheoMon> TongTheoMon);

public interface ISuatAnNgayService
{
    /// <summary>Thực đơn + số suất của một ngày cho cơ sở đang đăng nhập.</summary>
    Task<SuatAnNgay> LayTheoNgayAsync(DateOnly ngay, CancellationToken ct = default);
}

/// <inheritdoc cref="ISuatAnNgayService"/>
public sealed class SuatAnNgayService : ISuatAnNgayService
{
    private readonly AppDbContext _db;

    public SuatAnNgayService(AppDbContext db) => _db = db;

    public async Task<SuatAnNgay> LayTheoNgayAsync(DateOnly ngay, CancellationToken ct = default)
    {
        // DayOfWeek của .NET trùng quy ước thu_ap_dung của HanoiCheck: Chủ nhật = 0, Thứ 2 = 1...
        var thu = (int)ngay.DayOfWeek;

        var thucDon = await LayThucDonAsync(thu, ct);
        var (theoKhach, tongTheoMon, tongSuat) = await DemSuatAsync(ngay, ct);

        return new SuatAnNgay(ngay, thu, Menu.TenThu(thu), thucDon, theoKhach, tongSuat, tongTheoMon);
    }

    private async Task<IReadOnlyList<ThucDonTrongNgay>> LayThucDonAsync(int thu, CancellationToken ct)
    {
        var menus = await _db.Menus.AsNoTracking()
            .Include(m => m.DanhSachMon)
            .Where(m => m.ThuApDung == thu && m.TrangThai)
            .OrderBy(m => m.MaThucDon)
            .ToListAsync(ct);

        if (menus.Count == 0) return Array.Empty<ThucDonTrongNgay>();

        var tenMon = await _db.Dishes.AsNoTracking()
            .ToDictionaryAsync(d => d.MaMonAn, d => d.TenMonAn, ct);
        var buaAn = await _db.BuaAnHnCs.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b, ct);
        var nhomTuoi = await _db.NhomTuoiHnCs.AsNoTracking().ToDictionaryAsync(n => n.Id, n => n.Ten, ct);
        var truong = await _db.TruongHocHnCs.AsNoTracking().ToDictionaryAsync(t => t.TruongId, t => t.Ten, ct);

        return menus.Select(m => new ThucDonTrongNgay(
            Id: m.Id,
            MaThucDon: m.MaThucDon,
            TenThucDon: m.TenThucDon,
            TenNhomTuoi: nhomTuoi.GetValueOrDefault(m.NhomTuoiId) ?? $"Nhóm tuổi {m.NhomTuoiId}",
            TenTruong: m.DanhSachTruong.Select(id => truong.GetValueOrDefault(id) ?? $"Trường #{id}").ToList(),
            DanhSachMon: m.DanhSachMon
                .Select(i => new MonTrongThucDon(
                    MaMonAn: i.MaMonAn,
                    TenMonAn: tenMon.GetValueOrDefault(i.MaMonAn) ?? i.MaMonAn,
                    BuoiAnId: i.BuoiAnId,
                    TenBuoiAn: buaAn.GetValueOrDefault(i.BuoiAnId)?.Ten ?? $"Bữa #{i.BuoiAnId}",
                    ThuTuBuoiAn: buaAn.GetValueOrDefault(i.BuoiAnId)?.ThuTu ?? int.MaxValue))
                .OrderBy(x => x.ThuTuBuoiAn).ThenBy(x => x.TenMonAn)
                .ToList()))
            .ToList();
    }

    /// <summary>
    /// Số suất gộp từ ĐƠN HÀNG BÁN có dòng món ăn, lấy theo ngày hẹn giao. Đơn đã huỷ không tính.
    /// Dùng đơn chứ không dùng thực đơn vì thực đơn chỉ nói nấu món gì, còn số suất là do trường đặt.
    /// </summary>
    private async Task<(IReadOnlyList<SuatTheoKhach>, IReadOnlyList<SuatTheoMon>, decimal)> DemSuatAsync(
        DateOnly ngay, CancellationToken ct)
    {
        var dong = await _db.DonHangBans.AsNoTracking()
            .Where(d => d.NgayGiao == ngay && d.TrangThai != TrangThaiDonHangBan.DaHuy)
            .SelectMany(d => d.Dong.Where(l => l.LaMonAn)
                .Select(l => new { d.MaKhachHang, l.MaThanhPham, l.SoLuong }))
            .ToListAsync(ct);

        if (dong.Count == 0)
            return (Array.Empty<SuatTheoKhach>(), Array.Empty<SuatTheoMon>(), 0m);

        var tenMon = await _db.Dishes.AsNoTracking().ToDictionaryAsync(d => d.MaMonAn, d => d.TenMonAn, ct);
        var tenKhach = await _db.KhachHangs.AsNoTracking()
            .ToDictionaryAsync(k => k.MaKhachHang, k => k.TenKhachHang, ct);

        string TenMon(string ma) => tenMon.GetValueOrDefault(ma) ?? ma;

        var theoKhach = dong
            .GroupBy(x => x.MaKhachHang)
            .Select(g => new SuatTheoKhach(
                MaKhachHang: g.Key,
                TenKhachHang: tenKhach.GetValueOrDefault(g.Key) ?? g.Key,
                TongSuat: g.Sum(x => x.SoLuong),
                DanhSachMon: g.GroupBy(x => x.MaThanhPham)
                    .Select(m => new SuatTheoMon(m.Key, TenMon(m.Key), m.Sum(x => x.SoLuong)))
                    .OrderBy(m => m.TenMonAn).ToList()))
            .OrderByDescending(k => k.TongSuat).ThenBy(k => k.TenKhachHang)
            .ToList();

        var tongTheoMon = dong
            .GroupBy(x => x.MaThanhPham)
            .Select(g => new SuatTheoMon(g.Key, TenMon(g.Key), g.Sum(x => x.SoLuong)))
            .OrderByDescending(m => m.SoSuat).ThenBy(m => m.TenMonAn)
            .ToList();

        return (theoKhach, tongTheoMon, dong.Sum(x => x.SoLuong));
    }
}
