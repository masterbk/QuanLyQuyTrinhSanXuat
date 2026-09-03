using System.Security.Claims;
using HCP.Domain.Constants;
using HCP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HCP.Infrastructure.Identity;

/// <summary>
/// Gắn thông tin cơ sở vào danh tính khi đăng nhập.
///
/// Đây là mắt xích nối Identity với Finbuckle. Phát ra HAI claim khác nhau:
///   - tenantIdentifier: Finbuckle ClaimStrategy dùng để tra cứu cơ sở (khớp cột Identifier).
///   - tenantId: Id thật, dùng cho code ứng dụng cần đối chiếu cột TenantId.
///
/// Tài khoản quản trị nền tảng không có claim nào trong hai claim này (không thuộc cơ sở nào).
/// </summary>
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly TenantStoreDbContext _tenantStore;

    public AppUserClaimsPrincipalFactory(UserManager<ApplicationUser> userManager,
                                         RoleManager<IdentityRole> roleManager,
                                         IOptions<IdentityOptions> options,
                                         TenantStoreDbContext tenantStore)
        : base(userManager, roleManager, options)
    {
        _tenantStore = tenantStore;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (!string.IsNullOrWhiteSpace(user.TenantId))
        {
            identity.AddClaim(new Claim(AppClaimTypes.TenantId, user.TenantId));

            var identifier = await _tenantStore.TenantInfo
                .Where(t => t.Id == user.TenantId)
                .Select(t => t.Identifier)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(identifier))
            {
                identity.AddClaim(new Claim(AppClaimTypes.TenantIdentifier, identifier));
            }
        }

        if (!string.IsNullOrWhiteSpace(user.HoTen))
        {
            identity.AddClaim(new Claim("hoTen", user.HoTen));
        }

        return identity;
    }
}
