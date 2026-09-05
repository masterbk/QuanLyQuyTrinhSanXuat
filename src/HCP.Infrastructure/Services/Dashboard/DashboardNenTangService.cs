using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.Dashboard;

/// <inheritdoc cref="IDashboardNenTangService"/>
public sealed class DashboardNenTangService : IDashboardNenTangService
{
    private readonly AppDbContext _db;
    private readonly TenantStoreDbContext _tenantStore;

    public DashboardNenTangService(AppDbContext db, TenantStoreDbContext tenantStore)
    {
        _db = db;
        _tenantStore = tenantStore;
    }

    public async Task<DashboardNenTang> LayAsync(CancellationToken ct = default)
    {
        var coSo = await _tenantStore.TenantInfo.AsNoTracking()
            .Select(t => new { t.Id, t.Name, t.TrangThai })
            .ToListAsync(ct);
        var tenTheoId = coSo
            .Where(t => t.Id is not null)
            .ToDictionary(t => t.Id!, t => t.Name ?? t.Id!);

        // SyncOutbox không lọc theo tenant -> quản trị nền tảng thấy toàn hệ thống.
        var outbox = (await _db.SyncOutboxItems.AsNoTracking()
                .GroupBy(o => o.Status)
                .Select(g => new { g.Key, SoLuong = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.SoLuong);

        var loiTheoCoSo = await _db.SyncOutboxItems.AsNoTracking()
            .Where(o => o.Status == SyncOutboxStatus.Failed
                        || o.Status == SyncOutboxStatus.NeedsManualReview)
            .GroupBy(o => o.TenantId)
            .Select(g => new { TenantId = g.Key, SoLoi = g.Count() })
            .OrderByDescending(x => x.SoLoi)
            .Take(10)
            .ToListAsync(ct);

        var topLoi = loiTheoCoSo
            .Select(x => new CoSoLoi(x.TenantId, tenTheoId.GetValueOrDefault(x.TenantId, x.TenantId), x.SoLoi))
            .ToList();

        var bayNgayTruoc = DateTime.UtcNow.AddDays(-7);
        var soLoiGanDay = await _db.SystemLogs.AsNoTracking()
            .CountAsync(l => l.Level != "Information" && l.TimestampUtc >= bayNgayTruoc, ct);

        return new DashboardNenTang(
            SoCoSoActive: coSo.Count(t => t.TrangThai == TenantStatus.Active),
            SoCoSoChoDuyet: coSo.Count(t => t.TrangThai == TenantStatus.PendingApproval),
            SoCoSoSuspended: coSo.Count(t => t.TrangThai == TenantStatus.Suspended),
            OutboxToanHeThong: outbox,
            TongLoi: outbox.GetValueOrDefault(SyncOutboxStatus.Failed)
                     + outbox.GetValueOrDefault(SyncOutboxStatus.NeedsManualReview),
            TopCoSoLoi: topLoi,
            SoLoiGanDay: soLoiGanDay);
    }
}
