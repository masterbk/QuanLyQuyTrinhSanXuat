using HCP.Domain.Entities.Business;

namespace HCP.Infrastructure.Services.Kho;

/// <summary>Một dòng kiểm tra tồn thành phẩm khi xuất bán (xem trước, kiểm tra đủ hàng).</summary>
public sealed record TonThanhPhamDto(
    string MaThanhPham,
    string TenThanhPham,
    string? DonViTinh,
    decimal Can,
    decimal Ton,
    bool Du);

/// <summary>
/// Phiếu xuất bán thành phẩm cho khách hàng: khi thực hiện sẽ trừ tồn thành phẩm theo lô
/// (FEFO - lô hết hạn trước xuất trước) khỏi kho. Nội bộ, không đồng bộ HanoiCheck.
/// </summary>
public interface IPhieuXuatBanService
{
    Task<IReadOnlyList<PhieuXuatBan>> LayTatCaAsync(CancellationToken ct = default);
    Task<PhieuXuatBan?> LayTheoIdAsync(int id, CancellationToken ct = default);

    /// <summary>Kiểm tra tồn cho danh sách dòng thành phẩm tại một kho (gộp trùng mã).</summary>
    Task<IReadOnlyList<TonThanhPhamDto>> KiemTraTonAsync(
        IEnumerable<PhieuXuatBanChiTiet> chiTiet, string maKho, CancellationToken ct = default);

    Task<KetQuaThaoTac> TaoAsync(PhieuXuatBan phieu, CancellationToken ct = default);

    /// <summary>Thực hiện phiếu: trừ tồn thành phẩm (FEFO). Chỉ chạy khi còn "Mới tạo".</summary>
    Task<KetQuaThaoTac> ThucHienAsync(int id, CancellationToken ct = default);

    /// <summary>Xoá phiếu (chỉ xoá được phiếu chưa thực hiện).</summary>
    Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default);
}
