using HCP.Domain.Entities.Common;

namespace HCP.Domain.Entities.Business;

/// <summary>
/// Thông tin in ở header mọi biểu mẫu kiểm soát của một cơ sở (logo, tên công ty, địa chỉ) - mỗi cơ sở
/// tối đa một dòng. Chưa có dòng thì PDF lấy tên/địa chỉ đã đăng ký của cơ sở.
/// </summary>
public class CaiDatInBieuMau : TenantEntity
{
    public int Id { get; set; }

    public string? TenCongTy { get; set; }

    public string? DiaChi { get; set; }

    /// <summary>Đường dẫn ảnh logo đã tải lên (/uploads/... hoặc URL tuyệt đối).</summary>
    public string? LogoDuongDan { get; set; }
}
