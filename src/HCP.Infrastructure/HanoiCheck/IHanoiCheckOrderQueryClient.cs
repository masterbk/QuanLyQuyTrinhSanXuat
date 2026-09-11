using System.Text.Json;
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

/// <summary>
/// Thông tin chung của một đơn - khớp JSON THẬT của GET /api/supplier/orders (đối chiếu
/// response do người dùng cung cấp 11/09/2026, lưu tại Docs/donhang.json). Chi tiết đơn
/// (GET /orders/{code}) được coi là cùng bộ trường này cộng thêm items[] và menus[].
/// </summary>
public abstract class OrderHeader
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("status_label")] public string? StatusLabel { get; set; }
    [JsonPropertyName("order_date")] public string? OrderDate { get; set; }
    [JsonPropertyName("school")] public SchoolInfo? School { get; set; }
    /// <summary>"food" hoặc "dish".</summary>
    [JsonPropertyName("product_type")] public string? ProductType { get; set; }
    [JsonPropertyName("product_type_label")] public string? ProductTypeLabel { get; set; }
    [JsonPropertyName("products")] public List<ProductInfo>? Products { get; set; }
    [JsonPropertyName("warehouses")] public List<WarehouseInfo>? Warehouses { get; set; }
    /// <summary>Người giao - object, null khi chưa phân công.</summary>
    [JsonPropertyName("transporter")] public TransporterInfo? Transporter { get; set; }
    /// <summary>Điểm trường giao hàng - JSON thật trả chuỗi ("4") nhưng nhận cả số cho chắc.</summary>
    [JsonPropertyName("school_point"), JsonConverter(typeof(ChuoiHoacSoConverter))]
    public string? SchoolPoint { get; set; }
    [JsonPropertyName("delivery_address")] public string? DeliveryAddress { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("traceability_url")] public string? TraceabilityUrl { get; set; }
    /// <summary>Giờ Việt Nam, dạng "yyyy-MM-dd HH:mm:ss".</summary>
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
}

public sealed class OrderListItem : OrderHeader
{
}

public sealed class WarehouseInfo
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class TransporterInfo
{
    /// <summary>ma_nhan_su đã gửi ở mục Đồng bộ nhân sự.</summary>
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("phone"), JsonConverter(typeof(ChuoiHoacSoConverter))]
    public string? Phone { get; set; }
    [JsonPropertyName("transport_mean")] public string? TransportMean { get; set; }
    [JsonPropertyName("license_plate")] public string? LicensePlate { get; set; }
}

/// <summary>Đọc một trường có thể là chuỗi hoặc số thành chuỗi (HnC không nhất quán kiểu).</summary>
public sealed class ChuoiHoacSoConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var n) ? n.ToString()
                                    : reader.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => SkipAndNull(ref reader)
        };

    private static string? SkipAndNull(ref Utf8JsonReader reader)
    {
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue(); else writer.WriteStringValue(value);
    }
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
// Khớp JSON THẬT (Docs/ChiTietDonHang.json, Docs/ChiTietSanPhamTrongDon.json - 11/09/2026).
// GET /orders/{code}/items/{productCode} trả đúng cấu trúc một phần tử items[] cộng order_code.
public sealed class OrderDetailResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("data")] public OrderDetail? Data { get; set; }
}

public sealed class OrderDetail : OrderHeader
{
    [JsonPropertyName("items")] public List<OrderDetailItem>? Items { get; set; }
    [JsonPropertyName("menus")] public List<OrderMenu>? Menus { get; set; }
}

[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed class OrderDetailItem
{
    /// <summary>Mã thực phẩm hoặc mã món ăn.</summary>
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("menu_code")] public string? MenuCode { get; set; }
    [JsonPropertyName("order_menu_trace_code")] public string? OrderMenuTraceCode { get; set; }
    [JsonPropertyName("trace_code")] public string? TraceCode { get; set; }
    [JsonPropertyName("file_url")] public string? FileUrl { get; set; }
    /// <summary>Đơn thực phẩm: food có giá trị, dish = null; đơn món ăn thì ngược lại.</summary>
    [JsonPropertyName("food")] public ProductInfo? Food { get; set; }
    [JsonPropertyName("dish")] public ProductInfo? Dish { get; set; }
    /// <summary>Số lượng trường đặt.</summary>
    [JsonPropertyName("requested_amount")] public decimal? RequestedAmount { get; set; }
    [JsonPropertyName("unit")] public string? Unit { get; set; }
    [JsonPropertyName("allocations")] public List<OrderAllocation>? Allocations { get; set; }

    public string? Name => Food?.Name ?? Dish?.Name;
}

/// <summary>Phân bổ cung ứng của dòng hàng = phiếu xuất kho của NCC trên HanoiCheck.</summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed class OrderAllocation
{
    [JsonPropertyName("stock_out_code")] public string? StockOutCode { get; set; }
    [JsonPropertyName("supplier_food_code")] public string? SupplierFoodCode { get; set; }
    [JsonPropertyName("food")] public ProductInfo? Food { get; set; }
    [JsonPropertyName("batch")] public BatchInfo? Batch { get; set; }
    [JsonPropertyName("warehouse")] public WarehouseInfo? Warehouse { get; set; }
    [JsonPropertyName("amount")] public decimal? Amount { get; set; }
}

public sealed class BatchInfo
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
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
