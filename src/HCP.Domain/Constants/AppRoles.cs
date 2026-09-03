namespace HCP.Domain.Constants;

/// <summary>
/// Vai trò 2 tầng: tầng nền tảng (công ty vận hành) và tầng tenant (từng cơ sở sản xuất).
/// </summary>
public static class AppRoles
{
    /// <summary>Quản trị nền tảng: duyệt/khoá tenant, giám sát đồng bộ toàn hệ thống.
    /// KHÔNG sửa dữ liệu nghiệp vụ của tenant.</summary>
    public const string PlatformSuperAdmin = "PlatformSuperAdmin";

    /// <summary>Quản trị của một cơ sở: quản lý người dùng nội bộ, cấu hình credential HanoiCheck.</summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>Nhân sự nhập liệu của một cơ sở.</summary>
    public const string TenantStaff = "TenantStaff";

    public static readonly string[] All = [PlatformSuperAdmin, TenantAdmin, TenantStaff];
}
