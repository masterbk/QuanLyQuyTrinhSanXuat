using Microsoft.AspNetCore.Identity;

namespace HCP.Infrastructure.Identity;

/// <summary>
/// Người dùng đăng nhập hệ thống.
///
/// TenantId == null  -> tài khoản cấp nền tảng (PlatformSuperAdmin), không thuộc cơ sở nào.
/// TenantId != null  -> nhân sự của một cơ sở sản xuất (TenantAdmin / TenantStaff).
///
/// TenantId được đưa vào claim khi đăng nhập; Finbuckle ClaimStrategy đọc claim đó
/// để thiết lập tenant context cho mọi truy vấn trong request.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? TenantId { get; set; }

    public string HoTen { get; set; } = string.Empty;

    public bool DangHoatDong { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
}
