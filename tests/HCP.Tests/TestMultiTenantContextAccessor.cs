using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Entities.Infrastructure;

namespace HCP.Tests;

/// <summary>
/// Accessor giả lập cho test: cho phép chuyển qua lại giữa các tenant
/// để kiểm chứng dữ liệu có thực sự bị cách ly hay không.
/// </summary>
public class TestMultiTenantContextAccessor : IMultiTenantContextAccessor
{
    private IMultiTenantContext _context = new MultiTenantContext<Tenant>();

    public IMultiTenantContext MultiTenantContext => _context;

    /// <summary>Đặt tenant hiện hành (mô phỏng người dùng của cơ sở đó đang đăng nhập).</summary>
    public void SetTenant(string tenantId)
    {
        _context = new MultiTenantContext<Tenant>
        {
            TenantInfo = new Tenant { Id = tenantId, Identifier = tenantId, Name = tenantId }
        };
    }

    /// <summary>Không có tenant nào (vd tài khoản platform admin, hoặc job nền chưa gán tenant).</summary>
    public void ClearTenant() => _context = new MultiTenantContext<Tenant>();
}
