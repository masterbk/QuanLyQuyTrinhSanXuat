using System.Text.Json.Serialization;

namespace HCP.Infrastructure.HanoiCheck;

/// <summary>Bộ lọc khi tra cứu danh sách đơn hàng (GET /api/supplier/orders).</summary>
public sealed class OrderQueryFilter
{
    public string? Status { get; set; }
    public DateOnly? OrderDateFrom { get; set; }
    public DateOnly? OrderDateTo { get; set; }
    public string? Keyword { get; set; }
}

// --- DTO phản hồi từ HnC (snake_case) ---
public sealed class OrderListResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("data")] public List<OrderListItem>? Data { get; set; }
    [JsonPropertyName("pagination")] public PaginationInfo? Pagination { get; set; }
}

public sealed class OrderListItem
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("school")] public SchoolInfo? School { get; set; }
    [JsonPropertyName("products")] public List<ProductInfo>? Products { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("order_date")] public string? OrderDate { get; set; }
    [JsonPropertyName("transporter_code")] public string? TransporterCode { get; set; }
}

public sealed class SchoolInfo
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class ProductInfo
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class PaginationInfo
{
    [JsonPropertyName("total")] public int Total { get; set; }
}

/// <summary>Kết quả tra cứu danh sách đơn (gộp toàn bộ các trang).</summary>
public sealed record OrderQueryResult(
    bool ThanhCong,
    bool ChuaCauHinh,
    string? ThongBao,
    IReadOnlyList<OrderListItem> Items)
{
    public static OrderQueryResult Ok(IReadOnlyList<OrderListItem> items) => new(true, false, null, items);
    public static OrderQueryResult ChuaCauHinhKq(string lyDo) => new(false, true, lyDo, Array.Empty<OrderListItem>());
    public static OrderQueryResult Loi(string thongBao) => new(false, false, thongBao, Array.Empty<OrderListItem>());
}

/// <summary>
/// Tra cứu đơn hàng từ HanoiCheck (chiều KÉO): GET /api/supplier/orders, có ký HMAC.
/// </summary>
public interface IHanoiCheckOrderQueryClient
{
    /// <summary>Lấy TẤT CẢ đơn khớp bộ lọc (tự lặp phân trang).</summary>
    Task<OrderQueryResult> LayDanhSachAsync(string tenantId, OrderQueryFilter filter, CancellationToken ct = default);
}
