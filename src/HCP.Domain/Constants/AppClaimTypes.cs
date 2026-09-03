namespace HCP.Domain.Constants;

public static class AppClaimTypes
{
    /// <summary>
    /// Claim chứa TenantId của người dùng đang đăng nhập.
    /// Finbuckle ClaimStrategy đọc claim này để xác định tenant context cho mỗi request.
    /// </summary>
    public const string TenantId = "tenantId";
}
