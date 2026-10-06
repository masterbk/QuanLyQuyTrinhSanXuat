using BieuMauEntity = HCP.Domain.Entities.Business.BieuMau;

namespace HCP.Infrastructure.Services.BieuMau;

/// <summary>
/// Quản lý ĐỊNH NGHĨA biểu mẫu kiểm soát (mẫu + trường + hạng mục). Việc nhân viên nhập phiếu ghi nhận
/// theo mẫu sẽ do dịch vụ riêng ở giai đoạn sau.
/// </summary>
public interface IBieuMauService
{
    Task<IReadOnlyList<BieuMauEntity>> LayTatCaAsync(CancellationToken ct = default);
    Task<BieuMauEntity?> LayTheoIdAsync(int id, CancellationToken ct = default);

    /// <summary>Tạo mới (Id==0) hoặc cập nhật mẫu, thay toàn bộ danh sách trường và hạng mục.</summary>
    Task<KetQuaThaoTac> LuuAsync(BieuMauEntity mau, CancellationToken ct = default);

    /// <summary>Xoá mẫu (chặn nếu đã có phiếu ghi nhận theo mẫu này).</summary>
    Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Nạp bộ biểu mẫu mẫu (nhóm A - theo ngày; nhóm B - checklist) cho cơ sở đang đăng nhập.
    /// Bỏ qua mẫu đã có cùng mã hiệu (idempotent) - gọi lại nhiều lần an toàn.
    /// </summary>
    Task<KetQuaThaoTac> NapMauMacDinhAsync(CancellationToken ct = default);
}
