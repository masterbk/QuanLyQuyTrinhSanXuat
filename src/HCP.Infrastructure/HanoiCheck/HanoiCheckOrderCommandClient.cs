using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.HanoiCheck;

/// <inheritdoc cref="IHanoiCheckOrderCommandClient"/>
public sealed class HanoiCheckOrderCommandClient : IHanoiCheckOrderCommandClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = null };

    private readonly AppDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ITenantTokenManager _tokenManager;
    private readonly IHmacSigner _signer;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HanoiCheckOrderCommandClient> _logger;

    public HanoiCheckOrderCommandClient(AppDbContext db,
                                        ISecretProtector protector,
                                        ITenantTokenManager tokenManager,
                                        IHmacSigner signer,
                                        IHttpClientFactory httpFactory,
                                        ILogger<HanoiCheckOrderCommandClient> logger)
    {
        _db = db;
        _protector = protector;
        _tokenManager = tokenManager;
        _signer = signer;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public Task<OrderCommandResult> XuLyDonAsync(string tenantId, string maDon, ProcessOrderRequest noiDung,
                                                 CancellationToken ct = default)
    {
        var payload = new ProcessPayload
        {
            MaNguoiGiao = noiDung.MaNguoiGiao,
            GhiChu = noiDung.GhiChu,
            DanhSachAnh = noiDung.DanhSachAnh?.Select(a => new ProcessPayloadAnh { DuongDan = a }).ToList(),
            // chi_tiet rỗng bị HanoiCheck từ chối 422 ("phải có ít nhất 1 dòng") - không có dòng nào thì BỎ HẲN khoá,
            // lúc đó dịch vụ chỉ cập nhật người giao / ghi chú / ảnh.
            ChiTiet = noiDung.ChiTiet is not { Count: > 0 }
                ? null
                : noiDung.ChiTiet.Select(l => new ProcessPayloadLine
                {
                    TraceCode = l.TraceCode,
                    MaThucPham = l.MaThucPham,
                    PhanBo = l.PhanBo.Select(p => new ProcessPayloadAllocation
                    {
                        MaLo = p.MaLo, MaKho = p.MaKho, SoLuong = p.SoLuong
                    }).ToList()
                }).ToList()
        };
        var path = "/api/supplier/orders/" + maDon + "/process";
        return GuiAsync(tenantId, path, JsonSerializer.Serialize(payload, JsonOpts), ct);
    }

    public Task<OrderCommandResult> DoiTrangThaiAsync(string tenantId, string maDon, string trangThai, string? ghiChu,
                                                      CancellationToken ct = default)
    {
        var payload = new StatusPayload { TrangThai = trangThai, GhiChu = ghiChu };
        var path = "/api/supplier/orders/" + maDon + "/status";
        return GuiAsync(tenantId, path, JsonSerializer.Serialize(payload, JsonOpts), ct);
    }

    /// <summary>Gọi 1 trong 2 dịch vụ ghi của mục Đơn hàng - đồng bộ, trả 200 ngay (KHÔNG qua outbox 202).</summary>
    private async Task<OrderCommandResult> GuiAsync(string tenantId, string path, string payloadJson, CancellationToken ct)
    {
        var token = await _tokenManager.LayAccessTokenAsync(tenantId, ct);
        switch (token.TrangThai)
        {
            case TokenTrangThai.ChuaCauHinh:
                return OrderCommandResult.ChuaCauHinhKq(token.ThongBao ?? "Chưa cấu hình kết nối.");
            case TokenTrangThai.Loi:
                return OrderCommandResult.Loi(token.ThongBao ?? "Không lấy được token.");
        }

        var cauHinh = await _db.TenantHnCCredentials.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (cauHinh is null) return OrderCommandResult.ChuaCauHinhKq("Chưa cấu hình kết nối.");

        var hmacSecret = _protector.TryUnprotect(cauHinh.HmacSecretEncrypted);
        if (hmacSecret is null)
            return OrderCommandResult.Loi("Không giải mã được hmac_secret đã lưu. Cơ sở cần nhập lại secret.");

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

            var http = _httpFactory.CreateClient(HanoiCheckSyncClient.HttpClientName);
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode) return OrderCommandResult.Ok();

            if (response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.NotFound
                                     or HttpStatusCode.Forbidden)
            {
                return OrderCommandResult.Loi(TrichThongBaoLoi(body) ?? $"HanoiCheck từ chối yêu cầu (HTTP {(int)response.StatusCode}).");
            }

            _logger.LogWarning("HnC trả về HTTP {StatusCode} khi gọi {Path} cho cơ sở {TenantId}.",
                (int)response.StatusCode, path, tenantId);
            return OrderCommandResult.Loi($"HanoiCheck trả về HTTP {(int)response.StatusCode}. Thử lại sau ít phút.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return OrderCommandResult.Loi("Hết thời gian chờ khi gửi tới HanoiCheck.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Lỗi mạng khi gọi {Path} cho cơ sở {TenantId}.", path, tenantId);
            return OrderCommandResult.Loi("Lỗi mạng khi gửi tới HanoiCheck: " + ex.Message);
        }
    }

    /// <summary>Dựng thông báo lỗi dễ đọc từ body {"message","errors":{...}} - trả null nếu không đọc được.</summary>
    private static string? TrichThongBaoLoi(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
                return message;

            var chiTiet = errors.EnumerateObject()
                .Select(p => $"{p.Name}: {string.Join(" ", p.Value.EnumerateArray().Select(v => v.GetString()))}")
                .ToList();
            return chiTiet.Count == 0 ? message : $"{message} ({string.Join(" | ", chiTiet)})";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // --- DTO nội bộ khớp đúng tên trường snake_case của đặc tả (Phần IV mục 11 k, l) ---

    private sealed class ProcessPayloadAnh
    {
        [JsonPropertyName("duong_dan")] public string DuongDan { get; set; } = string.Empty;
    }

    private sealed class ProcessPayloadAllocation
    {
        [JsonPropertyName("ma_lo")] public string MaLo { get; set; } = string.Empty;
        [JsonPropertyName("ma_kho")] public string MaKho { get; set; } = string.Empty;
        [JsonPropertyName("so_luong")] public decimal SoLuong { get; set; }
    }

    private sealed class ProcessPayloadLine
    {
        [JsonPropertyName("trace_code"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TraceCode { get; set; }

        /// <summary>Dùng thay trace_code với đơn cũ chưa lấy được mã truy vết (đặc tả cho phép, mã phải duy nhất trong đơn).</summary>
        [JsonPropertyName("ma_thuc_pham"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MaThucPham { get; set; }

        [JsonPropertyName("phan_bo")] public List<ProcessPayloadAllocation> PhanBo { get; set; } = new();
    }

    private sealed class ProcessPayload
    {
        // Không gửi khoá này (thay vì gửi null) khi CHƯA muốn đụng tới người giao - gửi null theo đặc tả
        // nghĩa là CHỦ ĐỘNG bỏ người giao đã gán, không phải "chưa có".
        [JsonPropertyName("ma_nguoi_giao"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MaNguoiGiao { get; set; }
        [JsonPropertyName("ghi_chu")] public string? GhiChu { get; set; }
        [JsonPropertyName("danh_sach_anh"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ProcessPayloadAnh>? DanhSachAnh { get; set; }
        [JsonPropertyName("chi_tiet"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ProcessPayloadLine>? ChiTiet { get; set; }
    }

    private sealed class StatusPayload
    {
        [JsonPropertyName("trang_thai")] public string TrangThai { get; set; } = string.Empty;
        [JsonPropertyName("ghi_chu")] public string? GhiChu { get; set; }
    }
}
