using System.Text.Json;
using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;

namespace HCP.Infrastructure.Sync;

/// <inheritdoc cref="ISyncOutboxWriter"/>
public sealed class SyncOutboxWriter : ISyncOutboxWriter
{
    private readonly AppDbContext _db;
    private readonly IMultiTenantContextAccessor _tenantAccessor;

    public SyncOutboxWriter(AppDbContext db, IMultiTenantContextAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    public async Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
    {
        var tenantId = _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            // Enqueue phải nằm trong ngữ cảnh cơ sở đã đăng nhập. Thiếu là lỗi lập trình,
            // không được âm thầm ghi bản ghi thiếu TenantId (job sẽ không biết gửi bằng credential nào).
            throw new InvalidOperationException(
                "Không xác định được cơ sở khi ghi hàng đợi đồng bộ (thiếu tenant context).");
        }

        _db.SyncOutboxItems.Add(new SyncOutboxItem
        {
            TenantId = tenantId,
            EntityType = entityType,
            EntityKey = entityKey,
            PayloadJson = JsonSerializer.Serialize(payload, HnCPayloadMapper.Json),
            Status = SyncOutboxStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}
