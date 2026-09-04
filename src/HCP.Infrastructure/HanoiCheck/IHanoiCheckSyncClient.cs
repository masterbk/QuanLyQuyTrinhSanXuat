namespace HCP.Infrastructure.HanoiCheck;

/// <summary>
/// Gửi một bản ghi merge sang HanoiCheck cho MỘT cơ sở: tự lấy token, ký HMAC bằng đúng
/// hmac_secret của cơ sở đó, POST và diễn giải phản hồi. Không tự cập nhật outbox - việc đó
/// do <c>SyncOutboxProcessor</c> làm, để tách rõ phần gọi mạng và phần quản lý hàng đợi.
/// </summary>
public interface IHanoiCheckSyncClient
{
    Task<SyncSendResult> GuiMergeAsync(
        string tenantId, string entityType, string payloadJson, CancellationToken ct = default);
}
