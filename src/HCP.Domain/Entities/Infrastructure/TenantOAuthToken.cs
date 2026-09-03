namespace HCP.Domain.Entities.Infrastructure;

/// <summary>
/// Access/refresh token hiện hành của MỘT cơ sở khi gọi HanoiCheck. Mỗi tenant 1 dòng.
/// Theo đặc tả: access_token mặc định sống 3600s, refresh_token 14 ngày.
/// </summary>
public class TenantOAuthToken
{
    public string TenantId { get; set; } = string.Empty;
    public Tenant? Tenant { get; set; }

    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
