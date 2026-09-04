using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.HanoiCheck;

/// <summary>
/// Gọi endpoint lấy/làm mới token của HanoiCheck. Mỗi lần gọi nhận credential tường minh
/// theo tham số - KHÔNG giữ credential ở field để tránh dùng nhầm secret giữa các cơ sở.
/// </summary>
public class HanoiCheckTokenClient : IHanoiCheckTokenClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HanoiCheckTokenClient> _logger;

    public HanoiCheckTokenClient(HttpClient http, ILogger<HanoiCheckTokenClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ConnectionTestResult> KiemTraKetNoiAsync(
        string baseUrl, string clientId, string clientSecret, CancellationToken ct = default)
    {
        var ketQua = await LayTokenAsync(baseUrl, clientId, clientSecret, ct);

        if (ketQua.ThanhCong)
        {
            return ConnectionTestResult.Ok(ketQua.Token!.ExpiresIn);
        }

        // Diễn giải mã lỗi thành thông báo rõ ràng cho người nhập ở màn cài đặt.
        return ConnectionTestResult.Loi(ketQua.HttpStatusCode switch
        {
            (int)HttpStatusCode.Unauthorized =>
                "HanoiCheck từ chối (401): client_id hoặc client_secret không đúng. " + ketQua.ThongBao,
            (int)HttpStatusCode.NotFound =>
                "Không tìm thấy endpoint (404). Kiểm tra lại Base URL.",
            (int)HttpStatusCode.TooManyRequests =>
                "Bị giới hạn tần suất (429). Endpoint token chỉ cho 60 request/phút, thử lại sau.",
            _ => ketQua.ThongBao ?? "Không kết nối được tới HanoiCheck."
        });
    }

    public Task<TokenFetchResult> LayTokenAsync(
        string baseUrl, string clientId, string clientSecret, CancellationToken ct = default) =>
        GoiEndpointTokenAsync(baseUrl, "/api/supplier/token", new
        {
            grant_type = "client_credentials",
            client_id = clientId,
            client_secret = clientSecret
        }, ct);

    public Task<TokenFetchResult> LamMoiTokenAsync(
        string baseUrl, string clientId, string clientSecret, string refreshToken,
        CancellationToken ct = default) =>
        GoiEndpointTokenAsync(baseUrl, "/api/supplier/refresh_token", new
        {
            grant_type = "refresh_token",
            refresh_token = refreshToken,
            client_id = clientId,
            client_secret = clientSecret
        }, ct);

    private async Task<TokenFetchResult> GoiEndpointTokenAsync(
        string baseUrl, string duongDan, object payload, CancellationToken ct)
    {
        if (!Uri.TryCreate(baseUrl?.TrimEnd('/') + duongDan, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return TokenFetchResult.Loi(
                "Base URL không hợp lệ. Ví dụ đúng: https://hanoicheck.example.vn");
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(uri, payload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // Không ghi secret vào log, chỉ ghi mã lỗi.
                _logger.LogWarning("Lấy token HanoiCheck thất bại. HTTP {Status}", (int)response.StatusCode);
                return TokenFetchResult.Loi(
                    $"HanoiCheck trả về HTTP {(int)response.StatusCode}. Phản hồi: {Rutgon(body)}",
                    (int)response.StatusCode);
            }

            var token = JsonSerializer.Deserialize<HanoiCheckTokenResponse>(body);

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return TokenFetchResult.Loi(
                    "HanoiCheck trả về HTTP 200 nhưng không có access_token. Phản hồi: " + Rutgon(body),
                    (int)response.StatusCode);
            }

            return TokenFetchResult.Ok(token);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return TokenFetchResult.Loi("Hết thời gian chờ khi kết nối tới HanoiCheck. Kiểm tra Base URL và mạng.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Không kết nối được tới HanoiCheck.");
            return TokenFetchResult.Loi("Không kết nối được tới HanoiCheck: " + ex.Message);
        }
    }

    private static string Rutgon(string s) =>
        string.IsNullOrWhiteSpace(s) ? "(rỗng)" : (s.Length > 300 ? s[..300] + "..." : s);
}
