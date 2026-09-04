using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Một kho chứa lô (phần tử của danh_sach_kho). Bảng con vẫn kế thừa TenantEntity + lọc
/// theo tenant để tránh rò rỉ dữ liệu chéo khi truy vấn trực tiếp bảng con.
/// </summary>
public class BatchWarehouse : TenantEntity
{
    public int Id { get; set; }

    public int BatchId { get; set; }
    public Batch? Batch { get; set; }

    /// <summary>ma_kho.</summary>
    public string MaKho { get; set; } = string.Empty;
}
