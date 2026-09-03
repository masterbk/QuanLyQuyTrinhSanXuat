using System.Text.Json.Serialization;

namespace HCP.Infrastructure.HanoiCheck;

/// <summary>
/// Phản hồi của POST /api/supplier/token và /api/supplier/refresh_token (đặc tả mục 1.5, 1.6).
/// </summary>
public class HanoiCheckTokenResponse
{
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    /// <summary>Số giây access_token còn sống. Mặc định 3600.</summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
}
