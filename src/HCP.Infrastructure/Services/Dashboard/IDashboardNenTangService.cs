using HCP.Domain.Enums;

namespace HCP.Infrastructure.Services.Dashboard;

/// <summary>Một cơ sở đang có nhiều bản ghi đồng bộ lỗi.</summary>
public sealed record CoSoLoi(string TenantId, string TenCoSo, int SoLoi);

/// <summary>Số liệu giám sát toàn hệ thống cho quản trị nền tảng.</summary>
public sealed record DashboardNenTang(
    int SoCoSoActive,
    int SoCoSoChoDuyet,
    int SoCoSoSuspended,
    IReadOnlyDictionary<SyncOutboxStatus, int> OutboxToanHeThong,
    int TongLoi,
    IReadOnlyList<CoSoLoi> TopCoSoLoi,
    int SoLoiGanDay);

/// <summary>
/// Tổng hợp tình trạng đồng bộ XUYÊN CƠ SỞ cho quản trị nền tảng. Chỉ đọc các bảng hạ tầng
/// (SyncOutbox, SystemLogs) vốn không lọc theo tenant, và sổ đăng ký cơ sở - không chạm dữ
/// liệu nghiệp vụ của cơ sở.
/// </summary>
public interface IDashboardNenTangService
{
    Task<DashboardNenTang> LayAsync(CancellationToken ct = default);
}
