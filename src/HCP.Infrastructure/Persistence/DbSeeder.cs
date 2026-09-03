using HCP.Domain.Constants;
using HCP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.Persistence;

/// <summary>
/// Khởi tạo dữ liệu nền: các role và tài khoản quản trị nền tảng đầu tiên.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Đã tạo role {Role}.", role);
            }
        }

        var email = config["PlatformAdmin:Email"];
        var matKhau = config["PlatformAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(matKhau))
        {
            logger.LogWarning(
                "Chưa cấu hình PlatformAdmin:Email / PlatformAdmin:Password - bỏ qua tạo tài khoản quản trị nền tảng.");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null) return;

        // TenantId = null: tài khoản cấp nền tảng, không thuộc cơ sở nào.
        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            HoTen = "Quản trị nền tảng",
            TenantId = null,
            EmailConfirmed = true,
            DangHoatDong = true
        };

        var ketQua = await userManager.CreateAsync(admin, matKhau);

        if (ketQua.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, AppRoles.PlatformSuperAdmin);
            logger.LogInformation("Đã tạo tài khoản quản trị nền tảng {Email}.", email);
        }
        else
        {
            logger.LogError("Không tạo được tài khoản quản trị nền tảng: {Loi}",
                string.Join("; ", ketQua.Errors.Select(e => e.Description)));
        }
    }
}
