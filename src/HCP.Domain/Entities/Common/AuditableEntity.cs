namespace HCP.Domain.Entities.Common;

/// <summary>
/// Thông tin kiểm toán cơ bản. Tài liệu HnC yêu cầu truy vết được ai thay đổi dữ liệu lúc nào.
/// </summary>
public abstract class AuditableEntity
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
