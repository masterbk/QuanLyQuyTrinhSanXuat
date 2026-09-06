using HCP.Domain.Entities.Business;

namespace HCP.Infrastructure.Services.Kho;

/// <summary>Nhu cầu nguyên liệu cho một lệnh sản xuất (để xem trước và kiểm tra đủ tồn).</summary>
public sealed record NguyenLieuCanDto(
    string MaNguyenLieu,
    string TenNguyenLieu,
    string? DonViTinh,
    decimal Can,
    decimal Ton,
    bool Du);

/// <summary>
/// Lệnh sản xuất nội bộ: tính nguyên liệu cần theo định mức, khi thực hiện thì trừ tồn nguyên
/// liệu (FEFO - lô hết hạn trước xuất trước) và nhập thành phẩm vào kho.
/// </summary>
public interface ILenhSanXuatService
{
    Task<IReadOnlyList<LenhSanXuat>> LayTatCaAsync(CancellationToken ct = default);
    Task<LenhSanXuat?> LayTheoIdAsync(int id, CancellationToken ct = default);

    /// <summary>Nguyên liệu cần cho (thành phẩm, số lượng) tại một kho, kèm tồn hiện có.</summary>
    Task<IReadOnlyList<NguyenLieuCanDto>> TinhNguyenLieuCanAsync(
        string maThanhPham, decimal soLuong, string maKho, CancellationToken ct = default);

    Task<KetQuaThaoTac> TaoAsync(LenhSanXuat lenh, CancellationToken ct = default);

    /// <summary>Thực hiện lệnh: trừ nguyên liệu (FEFO) + nhập thành phẩm. Chỉ chạy khi còn "Mới tạo".</summary>
    Task<KetQuaThaoTac> ThucHienAsync(int id, CancellationToken ct = default);

    /// <summary>Xoá lệnh (chỉ xoá được lệnh chưa thực hiện).</summary>
    Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default);
}
