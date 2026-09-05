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

    /// <summary>Tạm khoá một cơ sở đang hoạt động (Active -> Suspended). Cơ sở bị khoá không đăng nhập được.</summary>
    Task<KetQuaThaoTac> KhoaAsync(string tenantId, string nguoiThucHien, CancellationToken ct = default);

    /// <summary>Mở lại một cơ sở đang bị khoá (Suspended -> Active).</summary>
    Task<KetQuaThaoTac> MoKhoaAsync(string tenantId, string nguoiThucHien, CancellationToken ct = default);

    /// <summary>Trạng thái kết nối HnC theo cơ sở: tenantId -> đã kiểm tra kết nối thành công chưa.</summary>
    Task<IReadOnlyDictionary<string, bool>> LayTrangThaiKetNoiAsync(CancellationToken ct = default);
}
