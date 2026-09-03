using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.HanoiCheck;

/// <summary>
/// Gọi endpoint lấy token của HanoiCheck. Mỗi lần gọi nhận credential tường minh
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
        if (!Uri.TryCreate(baseUrl?.TrimEnd('/') + "/api/supplier/token", UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return ConnectionTestResult.Loi(
                "Base URL không hợp lệ. Ví dụ đúng: https://hanoicheck.example.vn");
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(uri, new
            {
                grant_type = "client_credentials",
                client_id = clientId,
                client_secret = clientSecret
            }, ct);

            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // Không ghi secret vào log, chỉ ghi mã lỗi và phản hồi của HnC.
                _logger.LogWarning("Kiểm tra kết nối HanoiCheck thất bại. HTTP {Status}", (int)response.StatusCode);

                return ConnectionTestResult.Loi(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized =>
                        "HanoiCheck từ chối (401): client_id hoặc client_secret không đúng. "
                        + $"Phản hồi: {Rutgon(body)}",
                    HttpStatusCode.NotFound =>
                        "Không tìm thấy endpoint (404). Kiểm tra lại Base URL.",
                    HttpStatusCode.TooManyRequests =>
                        "Bị giới hạn tần suất (429). Endpoint token chỉ cho 60 request/phút, thử lại sau.",
                    _ => $"HanoiCheck trả về HTTP {(int)response.StatusCode}. Phản hồi: {Rutgon(body)}"
                });
            }

            var token = JsonSerializer.Deserialize<HanoiCheckTokenResponse>(body);

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return ConnectionTestResult.Loi(
                    "HanoiCheck trả về HTTP 200 nhưng không có access_token. Phản hồi: " + Rutgon(body));
            }

            return ConnectionTestResult.Ok(token.ExpiresIn);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return ConnectionTestResult.Loi("Hết thời gian chờ khi kết nối tới HanoiCheck. Kiểm tra Base URL và mạng.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Không kết nối được tới HanoiCheck.");
            return ConnectionTestResult.Loi("Không kết nối được tới HanoiCheck: " + ex.Message);
        }
    }

    private static string Rutgon(string s) =>
        string.IsNullOrWhiteSpace(s) ? "(rỗng)" : (s.Length > 300 ? s[..300] + "..." : s);
}
