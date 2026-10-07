using HCP.Domain.Entities.Business;
using BieuMauEntity = HCP.Domain.Entities.Business.BieuMau;

namespace HCP.Infrastructure.Services.BieuMau;

/// <summary>Nhân viên nhập phiếu ghi nhận theo một biểu mẫu (nhiệt độ, vệ sinh, checklist…).</summary>
public interface IPhieuGhiNhanService
{
    /// <summary>Danh sách biểu mẫu đang kích hoạt mà một trong các vai trò được phép điền.</summary>
    Task<IReadOnlyList<BieuMauEntity>> LayBieuMauChoNhapAsync(IEnumerable<string> vaiTro, CancellationToken ct = default);

    /// <summary>Lấy một biểu mẫu (kèm trường + hạng mục) để dựng form nhập.</summary>
    Task<BieuMauEntity?> LayBieuMauAsync(int bieuMauId, CancellationToken ct = default);

    /// <summary>Danh sách phiếu đã ghi, lọc theo ngày / người lập / biểu mẫu (mọi tham số là tuỳ chọn).</summary>
    Task<IReadOnlyList<PhieuGhiNhan>> LayPhieuAsync(DateOnly? ngay, string? nguoiLap, int? bieuMauId,
                                                    CancellationToken ct = default);

    /// <summary>Phiếu của một biểu mẫu trong một tháng (sắp theo ngày) - cho báo cáo tháng.</summary>
    Task<IReadOnlyList<PhieuGhiNhan>> LayPhieuThangAsync(int bieuMauId, int nam, int thang,
                                                        CancellationToken ct = default);

    Task<PhieuGhiNhan?> LayPhieuTheoIdAsync(int id, CancellationToken ct = default);

    /// <summary>Phiếu NHÁP (chưa hoàn thành) của một biểu mẫu cho một ngày/người lập - để mở nhập tiếp. Null nếu chưa có.</summary>
    Task<PhieuGhiNhan?> LayPhieuNhapAsync(int bieuMauId, DateOnly ngay, string? nguoiLap, CancellationToken ct = default);

    /// <summary>
    /// Phiếu của một biểu mẫu trong một NGÀY (mọi trạng thái, mới nhất) - dùng cho biểu mẫu "1 phiếu/ngày":
    /// còn nháp thì mở sửa, đã hoàn thành thì xem. Null nếu ngày đó chưa có phiếu.
    /// </summary>
    Task<PhieuGhiNhan?> LayPhieuTheoNgayAsync(int bieuMauId, DateOnly ngay, CancellationToken ct = default);

    /// <summary>
    /// Tạo một phiếu ghi nhận. <paramref name="hoanThanh"/>=false lưu NHÁP (cho phép thiếu trường bắt buộc, nhập
    /// tiếp sau); =true chốt (kiểm đủ trường bắt buộc).
    /// </summary>
    Task<KetQuaThaoTac> TaoPhieuAsync(PhieuGhiNhan phieu, bool hoanThanh = true, CancellationToken ct = default);

    /// <summary>Cập nhật một phiếu còn NHÁP (nhập tiếp). <paramref name="hoanThanh"/>=true thì chốt sau khi cập nhật.</summary>
    Task<KetQuaThaoTac> CapNhatPhieuAsync(PhieuGhiNhan phieu, bool hoanThanh = true, CancellationToken ct = default);
}
