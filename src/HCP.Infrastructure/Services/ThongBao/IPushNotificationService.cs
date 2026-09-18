namespace HCP.Infrastructure.Services.ThongBao;

/// <summary>
/// Gửi thông báo đẩy (FCM) cho nhân viên của một cơ sở, lọc theo vai trò. Luôn best-effort:
/// KHÔNG BAO GIỜ ném lỗi ra ngoài - một lần gửi thông báo hỏng không được chặn nghiệp vụ đơn
/// hàng đang xử lý. Chưa cấu hình Firebase (chưa có credentials) thì bỏ qua âm thầm.
/// </summary>
public interface IPushNotificationService
{
    /// <param name="vaiTro">Tên vai trò (khớp AppRoles) - nhân viên có ÍT NHẤT MỘT trong các vai trò này
    /// và thuộc đúng cơ sở <paramref name="tenantId"/> sẽ nhận thông báo trên mọi thiết bị đã đăng ký.</param>
    /// <param name="duLieu">Dữ liệu kèm thông báo (VD donHangId) - app dùng để mở đúng màn khi bấm vào.</param>
    Task GuiTheoQuyenAsync(string tenantId, IReadOnlyList<string> vaiTro, string tieuDe, string noiDung,
                          IReadOnlyDictionary<string, string>? duLieu = null, CancellationToken ct = default);
}
