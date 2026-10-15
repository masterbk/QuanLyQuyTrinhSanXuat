using System.Net.Http.Headers;
using System.Text.Json;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.HanoiCheck;

/// <summary>
/// GET /supplier/schools - trường học cơ sở được phân công phục vụ trong năm học hiện tại (đặc tả v2.6,
/// Phần V mục 5). Dữ liệu riêng từng cơ sở nên BẮT BUỘC ký HMAC bằng credential của chính cơ sở đó.
/// </summary>
public interface IHanoiCheckTruongHocClient
{
    Task<MeasuresResult> LayTruongAsync(string tenantId, CancellationToken ct = default);
}

/// <inheritdoc cref="IHanoiCheckTruongHocClient"/>
public sealed class HanoiCheckTruongHocClient : IHanoiCheckTruongHocClient
{
    /// <summary>PATH dùng để ký - không kèm query.</summary>
    private const string Path = "/api/supplier/schools";

    private readonly AppDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ITenantTokenManager _tokenManager;
    private readonly IHmacSigner _signer;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HanoiCheckTruongHocClient> _logger;

    public HanoiCheckTruongHocClient(AppDbContext db, ISecretProtector protector, ITenantTokenManager tokenManager,
                                     IHmacSigner signer, IHttpClientFactory httpFactory,
                                     ILogger<HanoiCheckTruongHocClient> logger)
    {
        _db = db;
        _protector = protector;
        _tokenManager = tokenManager;
        _signer = signer;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<MeasuresResult> LayTruongAsync(string tenantId, CancellationToken ct = default)
    {
        var token = await _tokenManager.LayAccessTokenAsync(tenantId, ct);
        if (token.TrangThai != TokenTrangThai.CoToken)
            return MeasuresResult.Loi(token.ThongBao ?? "Chưa cấu hình hoặc chưa xác thực kết nối HanoiCheck.");

        var cauHinh = await _db.TenantHnCCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (cauHinh is null) return MeasuresResult.Loi("Chưa cấu hình kết nối HanoiCheck.");

        var hmacSecret = _protector.TryUnprotect(cauHinh.HmacSecretEncrypted);
        if (hmacSecret is null)
            return MeasuresResult.Loi("Không giải mã được hmac_secret. Cơ sở cần nhập lại secret.");

        // GET không có thân -> ký trên chuỗi rỗng.
        var headers = _signer.Ky(hmacSecret, "GET", Path, string.Empty);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, cauHinh.BaseUrl.TrimEnd('/') + Path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            request.Headers.TryAddWithoutValidation("X-Timestamp", headers.Timestamp);
            request.Headers.TryAddWithoutValidation("X-Nonce", headers.Nonce);
            request.Headers.TryAddWithoutValidation("X-Signature", headers.Signature);

            var http = _httpFactory.CreateClient(HanoiCheckSyncClient.HttpClientName);
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return MeasuresResult.Loi($"HanoiCheck trả về HTTP {(int)response.StatusCode} khi lấy danh sách trường.");

            var data = JsonSerializer.Deserialize<MeasuresResponse>(body)?.Data;
            return data is null
                ? MeasuresResult.Loi("Phản hồi danh sách trường không đúng định dạng.")
                : MeasuresResult.Ok(data);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return MeasuresResult.Loi("Hết thời gian chờ khi lấy danh sách trường.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Lỗi mạng khi lấy danh sách trường cơ sở {TenantId}.", tenantId);
            return MeasuresResult.Loi("Lỗi mạng khi lấy danh sách trường: " + ex.Message);
        }
        catch (JsonException)
        {
            return MeasuresResult.Loi("Phản hồi danh sách trường không đúng định dạng.");
        }
    }
}
