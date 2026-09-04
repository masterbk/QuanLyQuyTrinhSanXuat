namespace HCP.Infrastructure.Sync;

/// <summary>
/// Xử lý hàng đợi đồng bộ: quét các bản ghi đến hạn, gửi sang HanoiCheck, cập nhật trạng thái
/// và lịch retry. Được job nền Hangfire gọi định kỳ.
/// </summary>
public interface ISyncOutboxProcessor
{
    /// <summary>Xử lý tối đa <paramref name="gioiHan"/> bản ghi đến hạn. Trả về số bản ghi đã xử lý.</summary>
    Task<int> XuLyCacBanGhiDenHanAsync(int gioiHan = 50, CancellationToken ct = default);
}
