namespace HCP.Domain.Entities.Infrastructure;

/// <summary>
/// Token thiết bị (FCM) để gửi thông báo đẩy cho app mobile - đơn hàng mới/đổi trạng thái.
///
/// Khoá theo Token (duy nhất): đăng ký lại cùng token thì cập nhật UserId, để xử lý đúng ca đổi
/// tài khoản trên cùng máy (máy A đăng xuất người 1, đăng nhập người 2 -> token gắn lại người 2).
///
/// KHÔNG kế thừa TenantEntity: bảng hạ tầng, giống <see cref="MobileRefreshToken"/>. Cơ sở của
/// người dùng suy ra qua UserId khi cần lọc theo tenant lúc gửi thông báo.
/// </summary>
public class PushDeviceToken
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>Registration token do Firebase Cloud Messaging cấp cho thiết bị.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Mô tả thiết bị do app gửi lên (tuỳ chọn), để đối chiếu khi cần.</summary>
    public string? ThietBi { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
