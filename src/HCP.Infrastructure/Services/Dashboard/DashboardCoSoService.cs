using HCP.Domain;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.NhatKyDongBo;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.Dashboard;

/// <inheritdoc cref="IDashboardCoSoService"/>
public sealed class DashboardCoSoService : IDashboardCoSoService
{
    private readonly AppDbContext _db;
    private readonly ISyncNhatKyService _nhatKy;

    public DashboardCoSoService(AppDbContext db, ISyncNhatKyService nhatKy)
    {
        _db = db;
        _nhatKy = nhatKy;
    }

    public async Task<DashboardCoSo> LayAsync(int soNgayCanhBao = 30, CancellationToken ct = default)
    {
        // Outbox: tái dùng dịch vụ nhật ký (đã lọc theo cơ sở đang đăng nhập).
        var outbox = await _nhatKy.DemTheoTrangThaiAsync(ct);

        int Dem(params SyncOutboxStatus[] tt) => tt.Sum(t => outbox.GetValueOrDefault(t));

        var canhBao = await LayCanhBaoGiayToAsync(soNgayCanhBao, ct);
        var canhBaoKho = await LayCanhBaoTonKhoAsync(soNgayCanhBao, ct);

        return new DashboardCoSo(
            OutboxTheoTrangThai: outbox,
            TongBanGhi: outbox.Values.Sum(),
            SoDangCho: Dem(SyncOutboxStatus.Pending, SyncOutboxStatus.AwaitingCredential),
            SoLoi: Dem(SyncOutboxStatus.Failed, SyncOutboxStatus.NeedsManualReview),
            SoThanhCong: Dem(SyncOutboxStatus.Success),
            CanhBaoGiayTo: canhBao,
            CanhBaoTonKho: canhBaoKho);
    }

    /// <summary>
    /// Cảnh báo tồn kho của cơ sở: lô sắp/đã hết hạn (trong <paramref name="soNgay"/> ngày) và
    /// sản phẩm có tổng tồn dưới mức tối thiểu đã khai. KhoGiaoDich và Products đều lọc theo tenant.
    /// </summary>
    private async Task<IReadOnlyList<CanhBaoTonKho>> LayCanhBaoTonKhoAsync(int soNgay, CancellationToken ct)
    {
        var giaoDich = await _db.KhoGiaoDichs.AsNoTracking().ToListAsync(ct);
        if (giaoDich.Count == 0) return Array.Empty<CanhBaoTonKho>();

        var sanPham = await _db.Products.AsNoTracking().ToListAsync(ct);
        var spTheoMa = sanPham.GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First());

        var homNay = GioVietNam.HomNay;
        var nguong = homNay.AddDays(soNgay);
        var ds = new List<CanhBaoTonKho>();

        // Tồn theo (sản phẩm, kho, lô).
        var tonTheoLo = giaoDich
            .GroupBy(g => new { g.MaSanPham, g.MaKho, g.MaLo })
            .Select(g => new
            {
                g.Key.MaSanPham,
                g.Key.MaKho,
                g.Key.MaLo,
                Hsd = g.Where(x => x.HanSuDung.HasValue).Max(x => x.HanSuDung),
                Ton = g.Sum(x => x.SoLuong)
            })
            .Where(x => x.Ton > 0)
            .ToList();

        // 1) Cảnh báo hạn dùng theo lô.
        foreach (var t in tonTheoLo.Where(x => x.Hsd.HasValue && x.Hsd.Value <= nguong))
        {
            var p = spTheoMa.GetValueOrDefault(t.MaSanPham);
            ds.Add(new CanhBaoTonKho(
                Loai: t.Hsd!.Value < homNay ? "Đã hết hạn" : "Sắp hết hạn",
                MaSanPham: t.MaSanPham,
                TenSanPham: p?.TenSanPham ?? t.MaSanPham,
                MaKho: t.MaKho,
                MaLo: t.MaLo,
                HanSuDung: t.Hsd,
                SoLuongTon: t.Ton,
                DonViTinh: p?.DonViTinh));
        }

        // 2) Cảnh báo tồn thấp: tổng tồn theo (sản phẩm, kho) < mức tối thiểu đã khai.
        var tonTheoSpKho = tonTheoLo
            .GroupBy(x => new { x.MaSanPham, x.MaKho })
            .Select(g => new { g.Key.MaSanPham, g.Key.MaKho, Ton = g.Sum(x => x.Ton) });

        foreach (var t in tonTheoSpKho)
        {
            var p = spTheoMa.GetValueOrDefault(t.MaSanPham);
            if (p?.TonToiThieu is not { } min || min <= 0) continue;
            if (t.Ton >= min) continue;
            ds.Add(new CanhBaoTonKho(
                Loai: "Tồn thấp",
                MaSanPham: t.MaSanPham,
                TenSanPham: p.TenSanPham,
                MaKho: t.MaKho,
                MaLo: null,
                HanSuDung: null,
                SoLuongTon: t.Ton,
                DonViTinh: p.DonViTinh));
        }

        // Ưu tiên hiển thị: đã hết hạn -> tồn thấp -> sắp hết hạn; trong nhóm hạn dùng, hạn gần nhất trước.
        int Uu(string loai) => loai switch { "Đã hết hạn" => 0, "Tồn thấp" => 1, _ => 2 };
        return ds
            .OrderBy(c => Uu(c.Loai))
            .ThenBy(c => c.HanSuDung ?? DateOnly.MaxValue)
            .ThenBy(c => c.TenSanPham)
            .ToList();
    }

    /// <summary>
    /// Quét giấy tờ của cơ sở sắp hết hạn (trong <paramref name="soNgay"/> ngày) hoặc đã hết hạn.
    /// SubSuppliers và Staff đều bị lọc theo tenant nên chỉ thấy dữ liệu của cơ sở hiện hành.
    /// </summary>
    private async Task<IReadOnlyList<CanhBaoGiayTo>> LayCanhBaoGiayToAsync(int soNgay, CancellationToken ct)
    {
        var homNay = GioVietNam.HomNay;
        var nguong = homNay.AddDays(soNgay);
        var ds = new List<CanhBaoGiayTo>();

        var ncc = await _db.SubSuppliers.AsNoTracking()
            .Where(s => (s.AttpNgayHetHan != null && s.AttpNgayHetHan <= nguong)
                        || (s.HopDongNgayHetHan != null && s.HopDongNgayHetHan <= nguong))
            .ToListAsync(ct);

        foreach (var s in ncc)
        {
            if (s.AttpNgayHetHan is { } attp && attp <= nguong)
                ds.Add(new CanhBaoGiayTo("Nhà cung ứng", s.Ten, s.MaNccDauVao,
                    "Giấy chứng nhận ATTP", attp, attp < homNay));
            if (s.HopDongNgayHetHan is { } hd && hd <= nguong)
                ds.Add(new CanhBaoGiayTo("Nhà cung ứng", s.Ten, s.MaNccDauVao,
                    "Hợp đồng cung ứng", hd, hd < homNay));
        }

        var nhanSu = await _db.Staff.AsNoTracking()
            .Where(s => s.KskNgayHetHan != null && s.KskNgayHetHan <= nguong)
            .ToListAsync(ct);

        foreach (var s in nhanSu)
        {
            var het = s.KskNgayHetHan!.Value;
            ds.Add(new CanhBaoGiayTo("Nhân sự", s.HoTen, s.MaNhanSu,
                "Giấy khám sức khoẻ", het, het < homNay));
        }

        // Đã hết hạn lên trước, rồi tới sắp hết hạn gần nhất.
        return ds.OrderBy(c => c.NgayHetHan).ToList();
    }
}
