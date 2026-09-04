using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Services.NhatKyDongBo;

/// <inheritdoc cref="ISyncNhatKyService"/>
public sealed class SyncNhatKyService : ISyncNhatKyService
{
    private readonly AppDbContext _db;
    private readonly IMultiTenantContextAccessor _tenantAccessor;

    public SyncNhatKyService(AppDbContext db, IMultiTenantContextAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    private string TenantId =>
        _tenantAccessor.MultiTenantContext?.TenantInfo?.Id
        ?? throw new InvalidOperationException("Không xác định được cơ sở đang đăng nhập.");

    public async Task<IReadOnlyList<SyncOutboxItem>> LayDanhSachAsync(
        SyncOutboxStatus? loc = null, int gioiHan = 200, CancellationToken ct = default)
    {
        // PHẢI lọc theo cơ sở đang đăng nhập: SyncOutbox không có global query filter.
        var query = _db.SyncOutboxItems.AsNoTracking().Where(o => o.TenantId == TenantId);

        if (loc.HasValue) query = query.Where(o => o.Status == loc.Value);

        return await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(gioiHan)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<SyncOutboxStatus, int>> DemTheoTrangThaiAsync(
        CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var thongKe = await _db.SyncOutboxItems.AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .GroupBy(o => o.Status)
            .Select(g => new { g.Key, SoLuong = g.Count() })
            .ToListAsync(ct);

        return thongKe.ToDictionary(x => x.Key, x => x.SoLuong);
    }

    public async Task<KetQuaThaoTac> GuiLaiAsync(long id, CancellationToken ct = default)
    {
        // Điều kiện TenantId == TenantId là chốt cách ly: cơ sở này không thể gửi lại
        // bản ghi của cơ sở khác dù có đoán đúng id.
        var item = await _db.SyncOutboxItems
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == TenantId, ct);

        if (item is null) return KetQuaThaoTac.Loi("Không tìm thấy bản ghi đồng bộ.");

        if (item.Status is not (SyncOutboxStatus.Failed or SyncOutboxStatus.NeedsManualReview))
        {
            return KetQuaThaoTac.Loi("Chỉ gửi lại được bản ghi đang lỗi hoặc cần xử lý thủ công.");
        }

        // Đặt lại để job nền xử lý ngay: cấp lại đủ số lần thử, xoá lịch hẹn và lỗi cũ.
        item.Status = SyncOutboxStatus.Pending;
        item.Attempts = 0;
        item.NextRetryAtUtc = null;
        item.LastError = null;
        await _db.SaveChangesAsync(ct);

        return KetQuaThaoTac.Ok($"Đã đưa bản ghi \"{item.EntityType} [{item.EntityKey}]\" vào hàng đợi gửi lại.");
    }
}
