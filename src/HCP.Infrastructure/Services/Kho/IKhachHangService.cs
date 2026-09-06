using HCP.Domain.Entities.Business;

namespace HCP.Infrastructure.Services.Kho;

/// <summary>Danh mục khách hàng mua thành phẩm của cơ sở (nội bộ, không đồng bộ HnC).</summary>
public interface IKhachHangService
{
    Task<IReadOnlyList<KhachHang>> LayTatCaAsync(CancellationToken ct = default);
    Task<KhachHang?> LayTheoIdAsync(int id, CancellationToken ct = default);

    /// <summary>Thêm mới (Id == 0) hoặc cập nhật khách hàng.</summary>
    Task<KetQuaThaoTac> LuuAsync(KhachHang kh, CancellationToken ct = default);

    Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default);
}
