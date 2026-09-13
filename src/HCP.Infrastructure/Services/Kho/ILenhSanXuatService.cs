using HCP.Domain.Entities.Business;

namespace HCP.Infrastructure.Services.Kho;

/// <summary>
/// Một ảnh lô thành phẩm đã được tầng web lưu xuống đĩa: tên gốc để hiển thị và đường dẫn phục vụ lại.
/// </summary>
public sealed record AnhLoSanXuat(string TenFile, string DuongDan);

/// <summary>Ảnh gửi kèm khi hoàn thành lệnh, gắn với MỘT dòng sản phẩm (mỗi dòng là một lô riêng).</summary>
public sealed record AnhTheoSanPham(int SanPhamId, IReadOnlyList<AnhLoSanXuat> Anh);

/// <summary>Nhu cầu nguyên liệu cho một lệnh sản xuất (để xem trước và kiểm tra đủ tồn).</summary>
public sealed record NguyenLieuCanDto(
    string MaNguyenLieu,
    string TenNguyenLieu,
    string? DonViTinh,
    decimal Can,
    decimal Ton,
    bool Du);

/// <summary>
/// Lệnh sản xuất nội bộ: mỗi lệnh gồm NHIỀU thành phẩm, mỗi thành phẩm là một lô riêng có
/// quy trình và các khâu (người thực hiện + cơ sở). Khi hoàn thành thì trừ tồn nguyên liệu
/// (FEFO - lô hết hạn trước xuất trước) và nhập thành phẩm vào kho.
/// </summary>
public interface ILenhSanXuatService
{
    Task<IReadOnlyList<LenhSanXuat>> LayTatCaAsync(CancellationToken ct = default);
    Task<LenhSanXuat?> LayTheoIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Nguyên liệu cần cho TOÀN BỘ các dòng sản phẩm tại một kho, kèm tồn hiện có.
    /// Nhu cầu của các dòng dùng chung một nguyên liệu được CỘNG DỒN rồi mới so với tồn.
    /// </summary>
    Task<IReadOnlyList<NguyenLieuCanDto>> TinhNguyenLieuCanAsync(
        IReadOnlyList<(string MaThanhPham, decimal SoLuong)> dong, string maKho, CancellationToken ct = default);

    Task<KetQuaThaoTac> TaoAsync(LenhSanXuat lenh, CancellationToken ct = default);

    /// <summary>
    /// Sửa lệnh (theo lenh.Id) - chỉ khi còn "Mới tạo", tức chưa động vào kho. Ghi đè toàn bộ
    /// danh sách sản phẩm và khâu theo dữ liệu gửi lên.
    /// </summary>
    Task<KetQuaThaoTac> CapNhatAsync(LenhSanXuat lenh, CancellationToken ct = default);

    /// <summary>
    /// Hoàn thành lệnh: trừ nguyên liệu (FEFO) + nhập thành phẩm cho mọi dòng. Chỉ chạy khi còn
    /// "Mới tạo". MỖI dòng sản phẩm bắt buộc kèm ít nhất 1 ảnh lô. Nếu có sửa lại người thực hiện
    /// hoặc cơ sở của khâu thì truyền qua <paramref name="khauSuaLai"/>.
    /// </summary>
    Task<KetQuaThaoTac> ThucHienAsync(
        int id,
        IReadOnlyList<AnhTheoSanPham> anhTheoSanPham,
        IReadOnlyList<LenhSanXuatKhau>? khauSuaLai = null,
        CancellationToken ct = default);

    /// <summary>Xoá lệnh (chỉ xoá được lệnh chưa thực hiện).</summary>
    Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Huỷ lệnh ĐÃ HOÀN THÀNH: ghi bút toán đảo trong sổ kho (trả nguyên liệu về đúng lô đã trừ,
    /// thu hồi thành phẩm của mọi dòng) rồi chuyển lệnh sang "Đã huỷ". Dữ liệu gốc được giữ nguyên
    /// để truy xuất. Chặn nếu thành phẩm của bất kỳ lô nào đã bị bán/dùng tiếp.
    /// </summary>
    Task<KetQuaThaoTac> HuyAsync(int id, string? lyDo, CancellationToken ct = default);
}
