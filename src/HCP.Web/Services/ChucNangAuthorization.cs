using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace HCP.Web.Services;

/// <summary>Yêu cầu cơ sở của người dùng được mở module <see cref="Ma"/> (xem <see cref="ChucNang"/>).</summary>
public sealed record ChucNangRequirement(string Ma) : IAuthorizationRequirement;

/// <summary>
/// Đọc module của cơ sở từ ngữ cảnh tenant (Finbuckle nạp bản ghi Tenant mới ở mỗi request/kết nối, nên quản trị
/// nền tảng đổi xong thì người dùng chỉ cần tải lại trang). Thiếu ngữ cảnh (hiếm) thì tra theo claim cơ sở.
/// </summary>
public sealed class ChucNangHandler : AuthorizationHandler<ChucNangRequirement>
{
    private readonly IMultiTenantContextAccessor<Tenant> _tenant;
    private readonly IMultiTenantStore<Tenant> _store;

    public ChucNangHandler(IMultiTenantContextAccessor<Tenant> tenant, IMultiTenantStore<Tenant> store)
    {
        _tenant = tenant;
        _store = store;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ChucNangRequirement req)
    {
        var coSo = _tenant.MultiTenantContext?.TenantInfo;
        if (coSo is null)
        {
            var ma = context.User.FindFirst(AppClaimTypes.TenantIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(ma)) coSo = await _store.TryGetByIdentifierAsync(ma);
        }
        if (coSo?.CoChucNang(req.Ma) == true) context.Succeed(req);
    }
}

/// <summary>Trạng thái module của cơ sở cho giao diện (menu, nút) - đọc từ ngữ cảnh tenant hiện tại.</summary>
public sealed class TrangThaiChucNang
{
    private readonly IMultiTenantContextAccessor<Tenant> _tenant;

    public TrangThaiChucNang(IMultiTenantContextAccessor<Tenant> tenant) => _tenant = tenant;

    public bool Co(string ma) => _tenant.MultiTenantContext?.TenantInfo?.CoChucNang(ma) == true;
}
