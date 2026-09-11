using HCP.Domain.Entities.Business;

namespace HCP.Infrastructure.Services.Kho;

/// <summary>
/// Một ảnh lô thành phẩm đã được tầng web lưu xuống đĩa: tên gốc để hiển thị và đường dẫn phục vụ lại.
/// </summary>
public sealed record AnhLoSanXuat(string TenFile, string DuongDan);

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

    /// <summary>
    /// Sửa lệnh (theo lenh.Id) - chỉ khi còn "Mới tạo", tức chưa động vào kho. Kiểm tra dữ liệu
    /// giống lúc tạo; mã lệnh được đổi nhưng không được trùng lệnh khác.
    /// </summary>
    Task<KetQuaThaoTac> CapNhatAsync(LenhSanXuat lenh, CancellationToken ct = default);

    /// <summary>
    /// Hoàn thành lệnh: trừ nguyên liệu (FEFO) + nhập thành phẩm. Chỉ chạy khi còn "Mới tạo".
    /// Bắt buộc kèm ít nhất 1 ảnh lô thành phẩm; ảnh được lưu theo lệnh và, nếu lệnh có sinh Lô
    /// sản xuất đồng bộ, được đưa luôn vào danh sách file của lô (loại HINH_ANH).
    /// </summary>
    Task<KetQuaThaoTac> ThucHienAsync(
        int id, IReadOnlyList<AnhLoSanXuat> anhLo, CancellationToken ct = default);

    /// <summary>Xoá lệnh (chỉ xoá được lệnh chưa thực hiện).</summary>
    Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Huỷ lệnh ĐÃ THỰC HIỆN: ghi bút toán đảo trong sổ kho (trả nguyên liệu về đúng lô đã trừ,
    /// thu hồi thành phẩm đã nhập) rồi chuyển lệnh sang "Đã huỷ". Dữ liệu gốc được giữ nguyên để
    /// truy xuất. Chặn nếu thành phẩm của lô đã bị bán/dùng tiếp (tồn không đủ để thu hồi).
    /// </summary>
    Task<KetQuaThaoTac> HuyAsync(int id, string? lyDo, CancellationToken ct = default);
}
