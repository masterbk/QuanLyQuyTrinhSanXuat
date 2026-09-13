using Finbuckle.MultiTenant.Abstractions;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Sync;

/// <summary>
/// Trạng thái công tắc tổng HanoiCheck cho giao diện (menu, ô tick, trường riêng HnC).
/// Nhiều component hỏi cùng lúc trong một lần render nên: đọc MỘT lần cho mỗi phiên (scope) và dùng DbContext riêng,
/// không đụng AppDbContext dùng chung của trang (tránh lỗi "a second operation was started on this context").
/// Đổi công tắc thì trang tải lại (forceLoad) nên scope mới sẽ đọc lại.
/// </summary>
public sealed class TrangThaiHnC
{
    private readonly IMultiTenantContextAccessor _tenantAccessor;
    private readonly DbContextOptions<AppDbContext> _options;
    private Task<bool>? _dangBat;

    public TrangThaiHnC(IMultiTenantContextAccessor tenantAccessor, DbContextOptions<AppDbContext> options)
    {
        _tenantAccessor = tenantAccessor;
        _options = options;
    }

    public Task<bool> DangBatAsync() => _dangBat ??= DocAsync();

    private async Task<bool> DocAsync()
    {
        var tenantId = _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId)) return false;

        await using var db = new AppDbContext(_tenantAccessor, _options);
        return await db.TenantHnCCredentials.AsNoTracking().AnyAsync(c => c.TenantId == tenantId && c.BatDongBo);
    }
}
