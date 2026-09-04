using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.HanoiCheck;

/// <inheritdoc cref="IHanoiCheckStandardFoodsClient"/>
public sealed class HanoiCheckStandardFoodsClient : IHanoiCheckStandardFoodsClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HanoiCheckStandardFoodsClient> _logger;

    public HanoiCheckStandardFoodsClient(IHttpClientFactory httpFactory,
                                         ILogger<HanoiCheckStandardFoodsClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<StandardFoodsResult> LayDanhMucAsync(
        string baseUrl, string accessToken, CancellationToken ct = default)
    {
        var url = baseUrl.TrimEnd('/') + "/api/supplier/standard-foods";
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return StandardFoodsResult.Loi("Base URL không hợp lệ.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var http = _httpFactory.CreateClient(HanoiCheckSyncClient.HttpClientName);
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return StandardFoodsResult.Loi($"HanoiCheck trả về HTTP {(int)response.StatusCode}.");
            }

            var parsed = JsonSerializer.Deserialize<StandardFoodsResponse>(body);
            if (parsed?.Data is null)
            {
                return StandardFoodsResult.Loi("Phản hồi standard-foods không đúng định dạng.");
            }

            return StandardFoodsResult.Ok(parsed.Data);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return StandardFoodsResult.Loi("Hết thời gian chờ khi lấy danh mục thực phẩm chuẩn.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Lỗi mạng khi lấy standard-foods.");
            return StandardFoodsResult.Loi("Lỗi mạng khi lấy danh mục thực phẩm chuẩn: " + ex.Message);
        }
    }
}
