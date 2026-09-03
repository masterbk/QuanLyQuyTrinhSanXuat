using HCP.Domain.Entities.Infrastructure;

namespace HCP.Infrastructure.Services;

/// <summary>
/// Quản lý vòng đời một cơ sở sản xuất trên nền tảng:
/// tự đăng ký -> quản trị nền tảng duyệt -> hoạt động.
/// </summary>
public interface ICoSoService
{
    Task<KetQuaThaoTac> DangKyAsync(DangKyCoSoRequest request, CancellationToken ct = default);

    /// <summary>Danh sách hồ sơ theo trạng thái, dùng cho màn duyệt của quản trị nền tảng.</summary>
    Task<IReadOnlyList<Tenant>> LayDanhSachAsync(Domain.Enums.TenantStatus? trangThai = null,
                                                 CancellationToken ct = default);

    Task<Tenant?> LayTheoIdAsync(string tenantId, CancellationToken ct = default);

    Task<KetQuaThaoTac> DuyetAsync(string tenantId, string nguoiDuyet, CancellationToken ct = default);

    Task<KetQuaThaoTac> TuChoiAsync(string tenantId, string nguoiDuyet, string lyDo,
                                    CancellationToken ct = default);
}
