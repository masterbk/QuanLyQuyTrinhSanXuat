namespace HCP.Domain.Enums;

/// <summary>
/// Trạng thái một bản ghi trong hàng đợi đồng bộ sang HanoiCheck.
/// </summary>
public enum SyncOutboxStatus
{
    /// <summary>Vừa ghi vào outbox, chờ job nền xử lý.</summary>
    Pending = 0,

    /// <summary>Tenant chưa cấu hình đủ credential HanoiCheck - job bỏ qua, không tính là lỗi.</summary>
    AwaitingCredential = 1,

    /// <summary>Job đang gửi đi.</summary>
    Processing = 2,

    /// <summary>HanoiCheck đã nhận (HTTP 200 hoặc 202).</summary>
    Success = 3,

    /// <summary>Gửi lỗi, còn trong hạn retry.</summary>
    Failed = 4,

    /// <summary>Hết số lần retry - cần người dùng sửa dữ liệu rồi gửi lại thủ công.</summary>
    NeedsManualReview = 5
}
