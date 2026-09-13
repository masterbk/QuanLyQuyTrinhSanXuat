namespace HCP.Infrastructure.HanoiCheck;

/// <summary>
/// Ánh xạ loại entity nghiệp vụ sang đường dẫn endpoint merge tương ứng của HanoiCheck.
/// EntityType lưu trong SyncOutbox phải khớp một khoá ở đây thì job mới biết gửi đi đâu.
/// </summary>
public static class SyncEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>
    {
        // 5 endpoint đồng bộ ngay (HTTP 200)
        ["Warehouse"] = "/api/supplier/warehouses/merge",
        ["ProductionStep"] = "/api/supplier/steps/merge",
        ["ProductionProcess"] = "/api/supplier/processes/merge",
        ["Staff"] = "/api/supplier/users/merge",
        ["SubSupplier"] = "/api/supplier/sub-suppliers/merge",

        // 5 endpoint bất đồng bộ (HTTP 202) - làm ở các giai đoạn sau, khai sẵn đường dẫn
        ["Facility"] = "/api/supplier/facilities/merge",
        ["Product"] = "/api/supplier/foods/merge",
        ["Batch"] = "/api/supplier/batches/merge",
        ["Dish"] = "/api/supplier/dishes/merge",
    };

    /// <summary>Trả về đường dẫn merge, hoặc null nếu EntityType không hợp lệ.</summary>
    public static string? DuongDan(string entityType) =>
        Map.TryGetValue(entityType, out var path) ? path : null;
}
