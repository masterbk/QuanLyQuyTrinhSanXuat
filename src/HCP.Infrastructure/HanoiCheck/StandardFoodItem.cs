using System.Text.Json.Serialization;

namespace HCP.Infrastructure.HanoiCheck;

/// <summary>Một dòng danh mục thực phẩm chuẩn trả về từ GET /supplier/standard-foods.</summary>
public sealed class StandardFoodItem
{
    /// <summary>id (số) - định danh thật của danh mục. HnC dùng id này làm ma_loai_sp khi merge
    /// thực phẩm (trường code thường trả null).</summary>
    [JsonPropertyName("id")] public int? Id { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("measure_name")] public string? MeasureName { get; set; }

    /// <summary>v2.7.1: lô nhập kho của thực phẩm thuộc danh mục này có bắt buộc ngay_san_xuat không.
    /// HnC quản lý theo từng danh mục và CÓ THỂ ĐỔI, nên phải đọc lại danh mục chứ đừng nhớ cố định.
    /// Máy chủ bản cũ không trả trường này -> null -> hiểu là BẮT BUỘC (an toàn, giống v2.7).</summary>
    [JsonPropertyName("required_manufacturing_date")] public bool? RequiredManufacturingDate { get; set; }

    /// <summary>v2.7.1: tương tự cho han_su_dung.</summary>
    [JsonPropertyName("required_expiry_date")] public bool? RequiredExpiryDate { get; set; }
}

/// <summary>Phong bì phản hồi chung của HnC (mục 1.4) cho endpoint standard-foods.</summary>
public sealed class StandardFoodsResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("data")] public List<StandardFoodItem>? Data { get; set; }
}

/// <summary>Một dòng danh mục { id, name } - dùng cho GET /supplier/measures, student-groups, meal-types, schools.</summary>
public sealed class MeasureItem
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class MeasuresResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("data")] public List<MeasureItem>? Data { get; set; }
}

public sealed record MeasuresResult(bool ThanhCong, IReadOnlyList<MeasureItem> Items, string? ThongBao)
{
    public static MeasuresResult Ok(IReadOnlyList<MeasureItem> items) => new(true, items, null);
    public static MeasuresResult Loi(string thongBao) => new(false, Array.Empty<MeasureItem>(), thongBao);
}

/// <summary>Kết quả lấy danh mục thực phẩm chuẩn.</summary>
public sealed record StandardFoodsResult(bool ThanhCong, IReadOnlyList<StandardFoodItem> Items, string? ThongBao)
{
    public static StandardFoodsResult Ok(IReadOnlyList<StandardFoodItem> items) => new(true, items, null);
    public static StandardFoodsResult Loi(string thongBao) => new(false, Array.Empty<StandardFoodItem>(), thongBao);
}
