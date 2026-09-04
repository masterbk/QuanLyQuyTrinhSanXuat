using System.Net;
using System.Net.Http.Headers;
using System.Text;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.HanoiCheck;

/// <inheritdoc cref="IHanoiCheckSyncClient"/>
public sealed class HanoiCheckSyncClient : IHanoiCheckSyncClient
{
    public const string HttpClientName = "HanoiCheckSync";

    private readonly AppDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ITenantTokenManager _tokenManager;
    private readonly IHmacSigner _signer;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HanoiCheckSyncClient> _logger;

    public HanoiCheckSyncClient(AppDbContext db,
                                ISecretProtector protector,
                                ITenantTokenManager tokenManager,
                                IHmacSigner signer,
                                IHttpClientFactory httpFactory,
                                ILogger<HanoiCheckSyncClient> logger)
    {
        _db = db;
        _protector = protector;
        _tokenManager = tokenManager;
        _signer = signer;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<SyncSendResult> GuiMergeAsync(
        string tenantId, string entityType, string payloadJson, CancellationToken ct = default)
    {
        var path = SyncEndpoints.DuongDan(entityType);
        if (path is null)
        {
            // Lỗi lập trình (EntityType sai) - retry vô ích, cần sửa code.
            return SyncSendResult.LoiDuLieu(
                $"EntityType \"{entityType}\" không có endpoint tương ứng.", null, null, null);
        }

        var token = await _tokenManager.LayAccessTokenAsync(tenantId, ct);
        switch (token.TrangThai)
        {
            case TokenTrangThai.ChuaCauHinh:
                return SyncSendResult.ChuaCauHinh(token.ThongBao ?? "Chưa cấu hình kết nối.");
            case TokenTrangThai.Loi:
                return SyncSendResult.LoiTamThoi(token.ThongBao ?? "Không lấy được token.", token.HttpStatusCode, path);
        }

        var cauHinh = await _db.TenantHnCCredentials
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (cauHinh is null)
        {
            return SyncSendResult.ChuaCauHinh("Chưa cấu hình kết nối.");
        }

        var hmacSecret = _protector.TryUnprotect(cauHinh.HmacSecretEncrypted);
        if (hmacSecret is null)
        {
            return SyncSendResult.LoiTamThoi(
                "Không giải mã được hmac_secret đã lưu. Cơ sở cần nhập lại secret.", null, path);
        }

        // Body ký PHẢI trùng từng byte với body gửi đi. Ký trên chính chuỗi payloadJson,
        // rồi gửi cũng chính chuỗi đó dưới dạng UTF-8.
        var headers = _signer.Ky(hmacSecret, "POST", path, payloadJson);
        var url = cauHinh.BaseUrl.TrimEnd('/') + path;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            request.Headers.TryAddWithoutValidation("X-Timestamp", headers.Timestamp);
            request.Headers.TryAddWithoutValidation("X-Nonce", headers.Nonce);
            request.Headers.TryAddWithoutValidation("X-Signature", headers.Signature);

            var http = _httpFactory.CreateClient(HttpClientName);
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return SyncSendResult.ThanhCong((int)response.StatusCode, path, body);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity) // 422
            {
                _logger.LogWarning("HnC từ chối dữ liệu (422) cơ sở {TenantId}, {EntityType}.", tenantId, entityType);
                return SyncSendResult.LoiDuLieu(
                    "HanoiCheck báo lỗi dữ liệu (422). Xem chi tiết trong nhật ký, sửa dữ liệu rồi gửi lại.",
                    422, path, body);
            }

            return SyncSendResult.LoiTamThoi(
                $"HanoiCheck trả về HTTP {(int)response.StatusCode}.", (int)response.StatusCode, path, body);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return SyncSendResult.LoiTamThoi("Hết thời gian chờ khi gửi tới HanoiCheck.", null, path);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Lỗi mạng khi gửi tới HanoiCheck cơ sở {TenantId}.", tenantId);
            return SyncSendResult.LoiTamThoi("Lỗi mạng khi gửi tới HanoiCheck: " + ex.Message, null, path);
        }
    }
}
