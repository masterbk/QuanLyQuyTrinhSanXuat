namespace HCP.Infrastructure.HanoiCheck;

public interface IHanoiCheckTokenClient
{
    /// <summary>
    /// Gọi POST {baseUrl}/api/supplier/token bằng credential của MỘT cơ sở cụ thể.
    /// Không cần ký HMAC (đặc tả mục 1.5). Dùng cho nút "Kiểm tra kết nối" trên UI.
    /// </summary>
    Task<ConnectionTestResult> KiemTraKetNoiAsync(
        string baseUrl, string clientId, string clientSecret, CancellationToken ct = default);

    /// <summary>
    /// Lấy access token mới bằng client_credentials (đặc tả mục 1.5). Trả về cả token để lưu lại.
    /// </summary>
    Task<TokenFetchResult> LayTokenAsync(
        string baseUrl, string clientId, string clientSecret, CancellationToken ct = default);

    /// <summary>
    /// Làm mới access token bằng refresh_token (đặc tả mục 1.6). refresh_token mặc định sống 14 ngày.
    /// </summary>
    Task<TokenFetchResult> LamMoiTokenAsync(
        string baseUrl, string clientId, string clientSecret, string refreshToken,
        CancellationToken ct = default);
}
