using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.HanoiCheck;

namespace HCP.Infrastructure.Services;

/// <summary>
/// Quản lý cấu hình kết nối HanoiCheck của MỘT cơ sở.
/// Mọi thao tác đều nhận tenantId tường minh để không bao giờ dùng nhầm credential cơ sở khác.
/// </summary>
public interface IKetNoiHnCService
{
    Task<TenantHnCCredential?> LayCauHinhAsync(string tenantId, CancellationToken ct = default);

    Task<KetQuaThaoTac> LuuCauHinhAsync(string tenantId, CauHinhKetNoiRequest request,
                                        CancellationToken ct = default);

    /// <summary>Gọi thử endpoint token bằng credential đã lưu của cơ sở.</summary>
    Task<ConnectionTestResult> KiemTraKetNoiAsync(string tenantId, CancellationToken ct = default);
}
