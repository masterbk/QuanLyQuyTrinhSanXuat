using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>Ảnh tổng quan của đơn hàng - phần tử của images (tối đa 3, sort_order 1–3 không trùng).</summary>
public class OrderImage : TenantEntity
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>path_file - đường dẫn ảnh.</summary>
    public string PathFile { get; set; } = string.Empty;

    /// <summary>sort_order - thứ tự 1–3.</summary>
    public int SortOrder { get; set; }
}
