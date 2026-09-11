using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.Logging;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HCP.Infrastructure.HanoiCheck;

/// <inheritdoc cref="IKiemTraApiHnCService"/>
public sealed class KiemTraApiHnCService : IKiemTraApiHnCService
{
    private static readonly IReadOnlyList<string> TrangThaiDon = new[]
    {
        "CHO_XAC_NHAN", "TU_CHOI", "DANG_CHUAN_BI", "DANG_GIAO", "DA_GIAO",
        "GIAO_HANG_THANH_CONG", "TRA_HANG", "HUY"
    };

    /// <summary>Theo bộ Postman chính thức của HanoiCheck (thư mục 2 và 3).</summary>
    private static readonly IReadOnlyList<ApiHnCDinhNghia> Api = new[]
    {
        new ApiHnCDinhNghia("standard-foods", "Danh mục thực phẩm chuẩn",
            "/api/supplier/standard-foods", CanKy: false,
            "Danh mục thực phẩm dùng chung { id, name, measure_name, ... }. id là nguồn của ma_loai_sp (gửi dạng chuỗi).",
            Array.Empty<ThamSoApiHnC>()),

        new ApiHnCDinhNghia("student-groups", "Danh mục nhóm tuổi",
            "/api/supplier/student-groups", CanKy: false,
            "Nhóm tuổi đang hoạt động { id, name } - nguồn tra nhom_tuoi_id của Món ăn và Thực đơn.",
            Array.Empty<ThamSoApiHnC>()),

        new ApiHnCDinhNghia("schools", "Trường học được phân công",
            "/api/supplier/schools", CanKy: true,
            "Trường cơ sở được phân công phục vụ trong năm học hiện tại { id, name }.",
            Array.Empty<ThamSoApiHnC>()),

        new ApiHnCDinhNghia("orders", "Danh sách đơn hàng",
            "/api/supplier/orders", CanKy: true,
            "Đơn các trường đặt cho cơ sở, đơn mới xếp trước. Trả data[] + pagination.",
            new[]
            {
                new ThamSoApiHnC("page", "Trang cần lấy, từ 1.", MacDinh: "1"),
                new ThamSoApiHnC("per_page", "Số đơn mỗi trang, 1-100 (trống = 15).", MacDinh: "20"),
                new ThamSoApiHnC("keyword", "Tìm theo mã đơn, tên trường, tên thực phẩm/món ăn."),
                new ThamSoApiHnC("code", "Lọc theo mã đơn, khớp một phần."),
                new ThamSoApiHnC("status", "Lọc theo trạng thái đơn.", LuaChon: TrangThaiDon),
                new ThamSoApiHnC("school_id", "Số định danh trường (id ở API Trường học), không phải mã trường."),
                new ThamSoApiHnC("transporter_code", "ma_nhan_su của nhân viên giao hàng."),
                new ThamSoApiHnC("order_date_from", "Từ ngày giao, dạng YYYY-MM-DD."),
                new ThamSoApiHnC("order_date_to", "Đến ngày giao, dạng YYYY-MM-DD."),
            }),

        new ApiHnCDinhNghia("order-detail", "Chi tiết đơn hàng",
            "/api/supplier/orders/{order_code}", CanKy: true,
            "Thông tin đơn, người giao, ảnh, items[] (kèm trace_code, allocations[]) và menus[].",
            new[]
            {
                new ThamSoApiHnC("order_code", "Mã đơn - lấy ở data[].code của Danh sách đơn hàng.",
                    TrongDuongDan: true, BatBuoc: true),
            }),

        new ApiHnCDinhNghia("order-item", "Chi tiết sản phẩm trong đơn",
            "/api/supplier/orders/{order_code}/items/{product_code}", CanKy: true,
            "Một dòng hàng trong đơn, cấu trúc giống một phần tử items[] của Chi tiết đơn hàng.",
            new[]
            {
                new ThamSoApiHnC("order_code", "Mã đơn.", TrongDuongDan: true, BatBuoc: true),
                new ThamSoApiHnC("product_code", "Mã thực phẩm / món ăn - items[].code của Chi tiết đơn.",
                    TrongDuongDan: true, BatBuoc: true),
                new ThamSoApiHnC("trace_code",
                    "items[].trace_code - bắt buộc khi sản phẩm xuất hiện nhiều dòng trong đơn."),
            }),
    };

    /// <summary>
    /// Giá trị đặt vào đường dẫn chỉ nhận ký tự an toàn: đường dẫn gửi đi phải trùng khít chuỗi
    /// đem ký, và không được chèn "/", "?" hay ".." để trỏ sang endpoint khác.
    /// </summary>
    private static readonly Regex GiaTriDuongDanHopLe = new("^[A-Za-z0-9_.\\-]+$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonDep = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // giữ nguyên tiếng Việt thay vì \uXXXX
    };

    private readonly AppDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ITenantTokenManager _tokenManager;
    private readonly IHmacSigner _signer;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ISystemLogWriter _logWriter;
    private readonly ILogger<KiemTraApiHnCService> _logger;

    public KiemTraApiHnCService(AppDbContext db,
                                ISecretProtector protector,
                                ITenantTokenManager tokenManager,
                                IHmacSigner signer,
                                IHttpClientFactory httpFactory,
                                ISystemLogWriter logWriter,
                                ILogger<KiemTraApiHnCService> logger)
    {
        _db = db;
        _protector = protector;
        _tokenManager = tokenManager;
        _signer = signer;
        _httpFactory = httpFactory;
        _logWriter = logWriter;
        _logger = logger;
    }

    public IReadOnlyList<ApiHnCDinhNghia> DanhSachApi => Api;

    public async Task<KetQuaGoiApiHnC> GoiAsync(
        string maApi, IReadOnlyDictionary<string, string?> thamSo,
        string? userId = null, CancellationToken ct = default)
    {
        var tenantId = _db.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId))
            return KetQuaGoiApiHnC.ChuaGui("Không xác định được cơ sở đang đăng nhập.");

        var api = Api.FirstOrDefault(a => a.Ma == maApi);
        if (api is null) return KetQuaGoiApiHnC.ChuaGui("API không nằm trong danh sách được phép gọi.");

        // Dựng đường dẫn + query từ tham số đã khai báo; tham số lạ bị bỏ qua.
        var path = api.MauDuongDan;
        var query = new List<string>();
        foreach (var ts in api.ThamSo)
        {
            var giaTri = thamSo.TryGetValue(ts.Ten, out var v) ? v?.Trim() : null;
            if (string.IsNullOrEmpty(giaTri))
            {
                if (ts.BatBuoc) return KetQuaGoiApiHnC.ChuaGui($"Vui lòng nhập {ts.Ten}.");
                continue;
            }

            if (ts.TrongDuongDan)
            {
                if (!GiaTriDuongDanHopLe.IsMatch(giaTri) || giaTri.Contains(".."))
                    return KetQuaGoiApiHnC.ChuaGui(
                        $"{ts.Ten} chỉ được gồm chữ, số và các ký tự - _ . (đang là \"{giaTri}\").");
                path = path.Replace("{" + ts.Ten + "}", giaTri);
            }
            else
            {
                query.Add(Uri.EscapeDataString(ts.Ten) + "=" + Uri.EscapeDataString(giaTri));
            }
        }

        var cauHinh = await _db.TenantHnCCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (cauHinh is null || string.IsNullOrWhiteSpace(cauHinh.BaseUrl))
            return KetQuaGoiApiHnC.ChuaGui("Cơ sở chưa cấu hình kết nối HanoiCheck (màn Cài đặt kết nối).");

        string? hmacSecret = null;
        if (api.CanKy)
        {
            hmacSecret = _protector.TryUnprotect(cauHinh.HmacSecretEncrypted);
            if (string.IsNullOrEmpty(hmacSecret))
                return KetQuaGoiApiHnC.ChuaGui("Không giải mã được hmac_secret - hãy nhập lại ở màn Cài đặt kết nối.");
        }

        var token = await _tokenManager.LayAccessTokenAsync(tenantId, ct);
        if (token.TrangThai != TokenTrangThai.CoToken)
            return KetQuaGoiApiHnC.ChuaGui("Không lấy được access token: " + (token.ThongBao ?? "lỗi không rõ."));

        var url = cauHinh.BaseUrl.TrimEnd('/') + path + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (hmacSecret is not null)
        {
            // GET không có thân -> ký trên chuỗi rỗng; PATH ký KHÔNG kèm query string.
            var ky = _signer.Ky(hmacSecret, "GET", path, string.Empty);
            request.Headers.TryAddWithoutValidation("X-Timestamp", ky.Timestamp);
            request.Headers.TryAddWithoutValidation("X-Nonce", ky.Nonce);
            request.Headers.TryAddWithoutValidation("X-Signature", ky.Signature);
        }

        var dongHo = Stopwatch.StartNew();
        KetQuaGoiApiHnC ketQua;
        try
        {
            var http = _httpFactory.CreateClient(HanoiCheckSyncClient.HttpClientName);
            using var response = await http.SendAsync(request, ct);
            var noiDung = await response.Content.ReadAsStringAsync(ct);
            dongHo.Stop();

            var (hienThi, laJson) = LamDepJson(noiDung);
            ketQua = new KetQuaGoiApiHnC(true, (int)response.StatusCode, url, api.CanKy,
                dongHo.ElapsedMilliseconds, hienThi, laJson, null);

            await GhiNhatKyAsync(tenantId, userId, api, url, (int)response.StatusCode, noiDung, null, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            ketQua = new KetQuaGoiApiHnC(false, null, url, api.CanKy, dongHo.ElapsedMilliseconds, null, false,
                "Hết thời gian chờ phản hồi từ HanoiCheck.");
            await GhiNhatKyAsync(tenantId, userId, api, url, null, null, ketQua.Loi, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Lỗi mạng khi kiểm tra API {Api} cơ sở {TenantId}.", api.Ma, tenantId);
            ketQua = new KetQuaGoiApiHnC(false, null, url, api.CanKy, dongHo.ElapsedMilliseconds, null, false,
                "Lỗi mạng: " + ex.Message);
            await GhiNhatKyAsync(tenantId, userId, api, url, null, null, ketQua.Loi, ct);
        }

        return ketQua;
    }

    /// <summary>JSON thì trình bày thụt lề cho dễ đọc; không phải JSON thì trả nguyên văn.</summary>
    private static (string NoiDung, bool LaJson) LamDepJson(string noiDung)
    {
        if (string.IsNullOrWhiteSpace(noiDung)) return (noiDung, false);
        try
        {
            using var doc = JsonDocument.Parse(noiDung);
            return (JsonSerializer.Serialize(doc.RootElement, JsonDep), true);
        }
        catch (JsonException)
        {
            return (noiDung, false);
        }
    }

    private Task GhiNhatKyAsync(string tenantId, string? userId, ApiHnCDinhNghia api, string url,
                                int? httpStatus, string? noiDung, string? loi, CancellationToken ct)
    {
        var thanhCong = httpStatus is >= 200 and < 300;
        return _logWriter.GhiAsync(new SystemLogEntry
        {
            TenantId = tenantId,
            UserId = userId,
            Level = thanhCong ? "Information" : "Warning",
            Message = $"Kiểm tra API {api.Ten}: " + (httpStatus is { } s ? $"HTTP {s}" : loi),
            HttpMethod = "GET",
            RequestUrl = url,
            ResponsePayload = noiDung,
            HttpStatusCode = httpStatus
        }, ct);
    }
}
