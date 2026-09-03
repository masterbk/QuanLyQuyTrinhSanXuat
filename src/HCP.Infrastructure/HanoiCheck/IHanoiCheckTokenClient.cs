namespace HCP.Infrastructure.HanoiCheck;

public interface IHanoiCheckTokenClient
{
    /// <summary>
    /// Gọi POST {baseUrl}/api/supplier/token bằng credential của MỘT cơ sở cụ thể.
    /// Không cần ký HMAC (đặc tả mục 1.5).
    /// </summary>
    Task<ConnectionTestResult> KiemTraKetNoiAsync(
        string baseUrl, string clientId, string clientSecret, CancellationToken ct = default);
}
