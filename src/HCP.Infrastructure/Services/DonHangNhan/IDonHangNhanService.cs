using HCP.Infrastructure.Sync;
using DonHangNhanEntity = HCP.Domain.Entities.Business.DonHangNhan;

namespace HCP.Infrastructure.Services.DonHangNhan;

/// <summary>
/// Đọc đơn hàng nhận về từ HanoiCheck cho cơ sở đang đăng nhập, và kích hoạt đồng bộ theo yêu cầu.
/// Vì DonHangNhan không dùng global filter, service này lọc TƯỜNG MINH theo tenant hiện hành.
/// </summary>
public interface IDonHangNhanService
{
    Task<IReadOnlyList<DonHangNhanEntity>> LayTatCaAsync(CancellationToken ct = default);

    /// <summary>Đồng bộ ngay đơn hàng cho cơ sở đang đăng nhập.</summary>
    Task<KetQuaDongBoDon> DongBoNgayAsync(CancellationToken ct = default);
}
