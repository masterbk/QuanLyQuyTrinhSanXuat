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

// --- Chi tiết đơn: GET /orders/{code} ---
// LƯU Ý: đặc tả chỉ mô tả bằng lời, KHÔNG có JSON mẫu đầy đủ. Tên trường allocations/số lượng
// là PHỎNG ĐOÁN theo snake_case; parse tolerant (nhận cả so_luong lẫn quantity). Cần đối chiếu
// sandbox thật rồi chỉnh lại đúng một chỗ này nếu sai.
public sealed class OrderDetailResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("data")] public OrderDetail? Data { get; set; }
}

public sealed class OrderDetail
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("order_date")] public string? OrderDate { get; set; }
    [JsonPropertyName("school")] public SchoolInfo? School { get; set; }
    [JsonPropertyName("items")] public List<OrderDetailItem>? Items { get; set; }
    [JsonPropertyName("menus")] public List<OrderMenu>? Menus { get; set; }
    // Bắt các trường header chưa chắc tên (người giao, địa chỉ giao...) để dò sau.
    [JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }
}

public sealed class OrderDetailItem
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("trace_code")] public string? TraceCode { get; set; }
    [JsonPropertyName("so_luong")] public decimal? SoLuong { get; set; }
    [JsonPropertyName("quantity")] public decimal? Quantity { get; set; }
    [JsonPropertyName("menu_code")] public string? MenuCode { get; set; }
    [JsonPropertyName("allocations")] public List<OrderAllocation>? Allocations { get; set; }
    public decimal? SoLuongCuoi => SoLuong ?? Quantity;
}

public sealed class OrderAllocation
{
    [JsonPropertyName("supplier_food_code")] public string? SupplierFoodCode { get; set; }
    [JsonPropertyName("ma_lo")] public string? MaLo { get; set; }
    [JsonPropertyName("ma_kho")] public string? MaKho { get; set; }
    [JsonPropertyName("so_luong")] public decimal? SoLuong { get; set; }
    [JsonPropertyName("quantity")] public decimal? Quantity { get; set; }
    public decimal? SoLuongCuoi => SoLuong ?? Quantity;
}

public sealed class OrderMenu
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("trace_code")] public string? TraceCode { get; set; }
}

public sealed record OrderDetailResult(bool ThanhCong, bool ChuaCauHinh, string? ThongBao, OrderDetail? Detail)
{
    public static OrderDetailResult Ok(OrderDetail d) => new(true, false, null, d);
    public static OrderDetailResult ChuaCauHinhKq(string lyDo) => new(false, true, lyDo, null);
    public static OrderDetailResult Loi(string thongBao) => new(false, false, thongBao, null);
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

    /// <summary>Lấy chi tiết một đơn theo mã (items + allocations + menus).</summary>
    Task<OrderDetailResult> LayChiTietAsync(string tenantId, string maDon, CancellationToken ct = default);
}
