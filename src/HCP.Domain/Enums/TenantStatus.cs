namespace HCP.Domain.Enums;

/// <summary>
/// Trạng thái của một cơ sở sản xuất (tenant) trên nền tảng.
/// </summary>
public enum TenantStatus
{
    /// <summary>Cơ sở đã tự đăng ký, đang chờ platform admin duyệt.</summary>
    PendingApproval = 0,

    /// <summary>Đã được duyệt, đang hoạt động bình thường.</summary>
    Active = 1,

    /// <summary>Bị tạm khoá (vd vi phạm, ngừng sử dụng dịch vụ).</summary>
    Suspended = 2,

    /// <summary>Hồ sơ đăng ký bị từ chối.</summary>
    Rejected = 3
}
