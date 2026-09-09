using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.HanoiCheck;

/// <inheritdoc cref="IHanoiCheckOrderQueryClient"/>
public sealed class HanoiCheckOrderQueryClient : IHanoiCheckOrderQueryClient
{
    /// <summary>PATH dùng để ký - KHÔNG kèm query string (đặc tả: chữ ký chỉ tính đường dẫn).</summary>
    private const string Path = "/api/supplier/orders";
    private const int PerPage = 100;
    private const int MaxTrang = 200; // chặn an toàn, tránh lặp vô hạn nếu HnC phân trang lạ.

    private readonly AppDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ITenantTokenManager _tokenManager;
    private readonly IHmacSigner _signer;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HanoiCheckOrderQueryClient> _logger;

    public HanoiCheckOrderQueryClient(AppDbContext db,
                                      ISecretProtector protector,
                                      ITenantTokenManager tokenManager,
                                      IHmacSigner signer,
                                      IHttpClientFactory httpFactory,
                                      ILogger<HanoiCheckOrderQueryClient> logger)
    {
        _db = db;
        _protector = protector;
        _tokenManager = tokenManager;
        _signer = signer;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<OrderQueryResult> LayDanhSachAsync(
        string tenantId, OrderQueryFilter filter, CancellationToken ct = default)
    {
        var token = await _tokenManager.LayAccessTokenAsync(tenantId, ct);
        switch (token.TrangThai)
        {
            case TokenTrangThai.ChuaCauHinh:
                return OrderQueryResult.ChuaCauHinhKq(token.ThongBao ?? "Chưa cấu hình kết nối.");
            case TokenTrangThai.Loi:
                return OrderQueryResult.Loi(token.ThongBao ?? "Không lấy được token.");
        }

        var cauHinh = await _db.TenantHnCCredentials.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (cauHinh is null) return OrderQueryResult.ChuaCauHinhKq("Chưa cấu hình kết nối.");

        var hmacSecret = _protector.TryUnprotect(cauHinh.HmacSecretEncrypted);
        if (hmacSecret is null)
            return OrderQueryResult.Loi("Không giải mã được hmac_secret. Cơ sở cần nhập lại secret.");

        var baseUrl = cauHinh.BaseUrl.TrimEnd('/');
        var loc = BaseQuery(filter);
        var http = _httpFactory.CreateClient(HanoiCheckSyncClient.HttpClientName);
        var all = new List<OrderListItem>();

        try
        {
            for (var page = 1; page <= MaxTrang; page++)
            {
                var query = $"{loc}&page={page}&per_page={PerPage}";
                var url = $"{baseUrl}{Path}?{query}";

                // GET không có thân -> ký trên chuỗi rỗng; PATH không kèm query.
                var headers = _signer.Ky(hmacSecret, "GET", Path, string.Empty);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                request.Headers.TryAddWithoutValidation("X-Timestamp", headers.Timestamp);
                request.Headers.TryAddWithoutValidation("X-Nonce", headers.Nonce);
                request.Headers.TryAddWithoutValidation("X-Signature", headers.Signature);

                using var response = await http.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                    return OrderQueryResult.Loi($"HanoiCheck trả về HTTP {(int)response.StatusCode} khi tra cứu đơn hàng.");

                OrderListResponse? parsed;
                try { parsed = JsonSerializer.Deserialize<OrderListResponse>(body); }
                catch (JsonException) { return OrderQueryResult.Loi("Phản hồi danh sách đơn không đúng định dạng JSON."); }

                var data = parsed?.Data ?? new List<OrderListItem>();
                all.AddRange(data);

                // Hết dữ liệu: trang trả về ít hơn per_page, hoặc đã đủ tổng số.
                if (data.Count < PerPage) break;
                if (parsed?.Pagination is { } pg && all.Count >= pg.Total) break;
            }

            return OrderQueryResult.Ok(all);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return OrderQueryResult.Loi("Hết thời gian chờ khi tra cứu đơn hàng.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Lỗi mạng khi tra cứu đơn hàng cơ sở {TenantId}.", tenantId);
            return OrderQueryResult.Loi("Lỗi mạng khi tra cứu đơn hàng: " + ex.Message);
        }
    }

    public async Task<OrderDetailResult> LayChiTietAsync(string tenantId, string maDon, CancellationToken ct = default)
    {
        var token = await _tokenManager.LayAccessTokenAsync(tenantId, ct);
        switch (token.TrangThai)
        {
            case TokenTrangThai.ChuaCauHinh:
                return OrderDetailResult.ChuaCauHinhKq(token.ThongBao ?? "Chưa cấu hình kết nối.");
            case TokenTrangThai.Loi:
                return OrderDetailResult.Loi(token.ThongBao ?? "Không lấy được token.");
        }

        var cauHinh = await _db.TenantHnCCredentials.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (cauHinh is null) return OrderDetailResult.ChuaCauHinhKq("Chưa cấu hình kết nối.");

        var hmacSecret = _protector.TryUnprotect(cauHinh.HmacSecretEncrypted);
        if (hmacSecret is null)
            return OrderDetailResult.Loi("Không giải mã được hmac_secret. Cơ sở cần nhập lại secret.");

        // PATH ký gồm cả mã đơn, không kèm query. Mã đơn dùng chung cho URL và chuỗi ký.
        var path = "/api/supplier/orders/" + maDon;
        var url = cauHinh.BaseUrl.TrimEnd('/') + path;
        var headers = _signer.Ky(hmacSecret, "GET", path, string.Empty);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            request.Headers.TryAddWithoutValidation("X-Timestamp", headers.Timestamp);
            request.Headers.TryAddWithoutValidation("X-Nonce", headers.Nonce);
            request.Headers.TryAddWithoutValidation("X-Signature", headers.Signature);

            var http = _httpFactory.CreateClient(HanoiCheckSyncClient.HttpClientName);
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return OrderDetailResult.Loi($"HanoiCheck trả về HTTP {(int)response.StatusCode} khi lấy chi tiết đơn {maDon}.");

            OrderDetailResponse? parsed;
            try { parsed = JsonSerializer.Deserialize<OrderDetailResponse>(body); }
            catch (JsonException) { return OrderDetailResult.Loi("Phản hồi chi tiết đơn không đúng định dạng JSON."); }

            if (parsed?.Data is null) return OrderDetailResult.Loi("Chi tiết đơn không có dữ liệu.");
            return OrderDetailResult.Ok(parsed.Data);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return OrderDetailResult.Loi("Hết thời gian chờ khi lấy chi tiết đơn.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Lỗi mạng khi lấy chi tiết đơn {MaDon} cơ sở {TenantId}.", maDon, tenantId);
            return OrderDetailResult.Loi("Lỗi mạng khi lấy chi tiết đơn: " + ex.Message);
        }
    }

    /// <summary>Dựng phần query (chưa gồm page/per_page) từ bộ lọc.</summary>
    private static string BaseQuery(OrderQueryFilter f)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Status)) parts.Add("status=" + Uri.EscapeDataString(f.Status));
        if (f.OrderDateFrom is { } from) parts.Add("order_date_from=" + from.ToString("yyyy-MM-dd"));
        if (f.OrderDateTo is { } to) parts.Add("order_date_to=" + to.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrWhiteSpace(f.Keyword)) parts.Add("keyword=" + Uri.EscapeDataString(f.Keyword));
        return parts.Count == 0 ? "_=1" : string.Join("&", parts);
    }
}
