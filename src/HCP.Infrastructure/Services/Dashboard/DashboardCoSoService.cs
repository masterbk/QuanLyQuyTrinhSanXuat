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

        return new DashboardCoSo(
            OutboxTheoTrangThai: outbox,
            TongBanGhi: outbox.Values.Sum(),
            SoDangCho: Dem(SyncOutboxStatus.Pending, SyncOutboxStatus.AwaitingCredential),
            SoLoi: Dem(SyncOutboxStatus.Failed, SyncOutboxStatus.NeedsManualReview),
            SoThanhCong: Dem(SyncOutboxStatus.Success),
            CanhBaoGiayTo: canhBao);
    }

    /// <summary>
    /// Quét giấy tờ của cơ sở sắp hết hạn (trong <paramref name="soNgay"/> ngày) hoặc đã hết hạn.
    /// SubSuppliers và Staff đều bị lọc theo tenant nên chỉ thấy dữ liệu của cơ sở hiện hành.
    /// </summary>
    private async Task<IReadOnlyList<CanhBaoGiayTo>> LayCanhBaoGiayToAsync(int soNgay, CancellationToken ct)
    {
        var homNay = DateOnly.FromDateTime(DateTime.Today);
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
