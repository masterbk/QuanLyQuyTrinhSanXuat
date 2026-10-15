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
        var (data, loi) = await GetAsync<StandardFoodsResponse>(
            baseUrl, "/api/supplier/standard-foods", accessToken, "danh mục thực phẩm chuẩn", ct);
        if (loi is not null) return StandardFoodsResult.Loi(loi);
        return data?.Data is null
            ? StandardFoodsResult.Loi("Phản hồi standard-foods không đúng định dạng.")
            : StandardFoodsResult.Ok(data.Data);
    }

    public async Task<MeasuresResult> LayDonViTinhAsync(
        string baseUrl, string accessToken, CancellationToken ct = default)
    {
        var (data, loi) = await GetAsync<MeasuresResponse>(
            baseUrl, "/api/supplier/measures", accessToken, "danh mục đơn vị tính", ct);
        if (loi is not null) return MeasuresResult.Loi(loi);
        return data?.Data is null
            ? MeasuresResult.Loi("Phản hồi measures không đúng định dạng.")
            : MeasuresResult.Ok(data.Data);
    }

    // Nhóm tuổi trả { id, name } giống hệt đơn vị tính nên dùng chung kiểu dữ liệu MeasureItem.
    public async Task<MeasuresResult> LayNhomTuoiAsync(
        string baseUrl, string accessToken, CancellationToken ct = default)
    {
        var (data, loi) = await GetAsync<MeasuresResponse>(
            baseUrl, "/api/supplier/student-groups", accessToken, "danh mục nhóm tuổi", ct);
        if (loi is not null) return MeasuresResult.Loi(loi);
        return data?.Data is null
            ? MeasuresResult.Loi("Phản hồi student-groups không đúng định dạng.")
            : MeasuresResult.Ok(data.Data);
    }

    public async Task<MeasuresResult> LayBuaAnAsync(
        string baseUrl, string accessToken, CancellationToken ct = default)
    {
        var (data, loi) = await GetAsync<MeasuresResponse>(
            baseUrl, "/api/supplier/meal-types", accessToken, "danh mục bữa ăn", ct);
        if (loi is not null) return MeasuresResult.Loi(loi);
        return data?.Data is null
            ? MeasuresResult.Loi("Phản hồi meal-types không đúng định dạng.")
            : MeasuresResult.Ok(data.Data);
    }

    /// <summary>GET danh mục dùng chung: chỉ cần Bearer token, không ký HMAC.</summary>
    private async Task<(T? Data, string? Loi)> GetAsync<T>(
        string baseUrl, string path, string accessToken, string tenDanhMuc, CancellationToken ct)
    {
        var url = baseUrl.TrimEnd('/') + path;
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return (default, "Base URL không hợp lệ.");
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
                return (default, $"HanoiCheck trả về HTTP {(int)response.StatusCode}.");
            }

            return (JsonSerializer.Deserialize<T>(body), null);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return (default, $"Hết thời gian chờ khi lấy {tenDanhMuc}.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Lỗi mạng khi lấy {Path}.", path);
            return (default, $"Lỗi mạng khi lấy {tenDanhMuc}: " + ex.Message);
        }
        catch (JsonException)
        {
            return (default, $"Phản hồi {tenDanhMuc} không đúng định dạng.");
        }
    }
}
