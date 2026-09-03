using System.Security.Claims;
using HCP.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HCP.Infrastructure.Identity;

/// <summary>
/// Gắn claim "tenantId" vào danh tính khi đăng nhập.
///
/// Đây là mắt xích nối Identity với Finbuckle: ClaimStrategy đọc chính claim này
/// để xác định người dùng thuộc cơ sở nào, từ đó mọi truy vấn được lọc theo cơ sở đó.
/// Tài khoản quản trị nền tảng không có claim này (không thuộc cơ sở nào).
/// </summary>
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppUserClaimsPrincipalFactory(UserManager<ApplicationUser> userManager,
                                         RoleManager<IdentityRole> roleManager,
                                         IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (!string.IsNullOrWhiteSpace(user.TenantId))
        {
            identity.AddClaim(new Claim(AppClaimTypes.TenantId, user.TenantId));
        }

        if (!string.IsNullOrWhiteSpace(user.HoTen))
        {
            identity.AddClaim(new Claim("hoTen", user.HoTen));
        }

        return identity;
    }
}
