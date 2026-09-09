using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using DonHangNhanEntity = HCP.Domain.Entities.Business.DonHangNhan;

namespace HCP.Infrastructure.Services.DonHangNhan;

/// <inheritdoc cref="IDonHangNhanService"/>
public sealed class DonHangNhanService : IDonHangNhanService
{
    private readonly AppDbContext _db;
    private readonly IDongBoDonHangJob _job;

    public DonHangNhanService(AppDbContext db, IDongBoDonHangJob job)
    {
        _db = db;
        _job = job;
    }

    private string TenantId => _db.TenantInfo?.Id
        ?? throw new InvalidOperationException("Không xác định được cơ sở đang đăng nhập.");

    public async Task<IReadOnlyList<DonHangNhanEntity>> LayTatCaAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        return await _db.DonHangNhans.AsNoTracking()
            .Include(d => d.Dong)
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.NgayGiao).ThenByDescending(d => d.LanDongBoUtc)
            .ToListAsync(ct);
    }

    public Task<KetQuaDongBoDon> DongBoNgayAsync(CancellationToken ct = default) =>
        _job.DongBoMotCoSoAsync(TenantId, ct);
}
