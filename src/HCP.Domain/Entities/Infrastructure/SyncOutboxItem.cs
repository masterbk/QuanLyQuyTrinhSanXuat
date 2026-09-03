using HCP.Domain.Enums;

namespace HCP.Domain.Entities.Infrastructure;

/// <summary>
/// Hàng đợi đồng bộ (outbox pattern).
///
/// Khi cơ sở lưu dữ liệu nghiệp vụ, ta KHÔNG gọi API HanoiCheck ngay trong request
/// (tránh treo UI và mất dữ liệu khi HnC lỗi) mà ghi một bản ghi vào đây.
/// Job nền Hangfire đọc theo TenantId, gom theo lô rồi gửi đi, có retry backoff.
///
/// Không kế thừa TenantEntity vì job nền chạy ngoài request context (không có tenant
/// context của Finbuckle) và platform admin cần truy vấn xuyên tenant để giám sát.
/// TenantId ở đây được gán tường minh khi ghi.
/// </summary>
public class SyncOutboxItem
{
    public long Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    /// <summary>Loại entity nghiệp vụ, vd "Warehouse", "Batch", "Order".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Khoá nghiệp vụ dùng để upsert phía HnC, vd ma_kho, ma_san_pham.</summary>
    public string EntityKey { get; set; } = string.Empty;

    /// <summary>Payload JSON đúng định dạng endpoint merge tương ứng.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public SyncOutboxStatus Status { get; set; } = SyncOutboxStatus.Pending;
    public int Attempts { get; set; }

    public int? LastHttpStatusCode { get; set; }

    /// <summary>Nội dung lỗi gần nhất (gồm cả map errors 422 để hiển thị lại cho người nhập).</summary>
    public string? LastError { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? NextRetryAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
