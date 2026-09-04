using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;

namespace HCP.Infrastructure.Services.NhatKyDongBo;

// KetQuaThaoTac nằm ở namespace HCP.Infrastructure.Services (cùng cây).

/// <summary>
/// Nhật ký đồng bộ của CƠ SỞ đang đăng nhập: xem trạng thái các bản ghi trong hàng đợi
/// (SyncOutbox) và gửi lại thủ công những bản ghi bị lỗi.
///
/// SyncOutbox KHÔNG có global query filter theo tenant (job nền cần truy vấn xuyên cơ sở),
/// nên service này PHẢI tự lọc theo cơ sở đang đăng nhập để không lộ / không sửa nhầm dữ liệu
/// cơ sở khác. Xem test cách ly trong HCP.Tests.
/// </summary>
public interface ISyncNhatKyService
{
    Task<IReadOnlyList<SyncOutboxItem>> LayDanhSachAsync(
        SyncOutboxStatus? loc = null, int gioiHan = 200, CancellationToken ct = default);

    /// <summary>Số bản ghi theo từng trạng thái (để hiển thị tổng quan).</summary>
    Task<IReadOnlyDictionary<SyncOutboxStatus, int>> DemTheoTrangThaiAsync(CancellationToken ct = default);

    /// <summary>
    /// Gửi lại một bản ghi đang lỗi (Failed / NeedsManualReview): đặt lại về Pending để job
    /// nền xử lý ngay. Chỉ tác động lên bản ghi thuộc chính cơ sở đang đăng nhập.
    /// </summary>
    Task<KetQuaThaoTac> GuiLaiAsync(long id, CancellationToken ct = default);
}
