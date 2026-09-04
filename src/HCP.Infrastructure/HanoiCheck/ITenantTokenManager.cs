namespace HCP.Infrastructure.HanoiCheck;

/// <summary>Phân loại kết quả khi engine đồng bộ xin access token của một cơ sở.</summary>
public enum TokenTrangThai
{
    /// <summary>Có access token hợp lệ để gửi request.</summary>
    CoToken,

    /// <summary>Cơ sở chưa nhập đủ credential HanoiCheck - BỎ QUA, không tính là lỗi.</summary>
    ChuaCauHinh,

    /// <summary>Có credential nhưng lấy token thất bại (sai secret, HnC lỗi...) - cần retry.</summary>
    Loi
}

public sealed record TokenKetQua(
    TokenTrangThai TrangThai,
    string? AccessToken,
    string? ThongBao,
    int? HttpStatusCode)
{
    public static TokenKetQua Co(string accessToken) => new(TokenTrangThai.CoToken, accessToken, null, null);
    public static TokenKetQua ChuaCauHinh(string lyDo) => new(TokenTrangThai.ChuaCauHinh, null, lyDo, null);
    public static TokenKetQua Loi(string thongBao, int? httpStatusCode = null) =>
        new(TokenTrangThai.Loi, null, thongBao, httpStatusCode);
}

/// <summary>
/// Quản lý access token của TỪNG cơ sở khi đồng bộ sang HanoiCheck.
///
/// Mỗi cơ sở có bộ credential riêng nên phải có token riêng - tuyệt đối không dùng token
/// của cơ sở này cho request của cơ sở khác. Manager tự lấy token khi chưa có / đã hết hạn,
/// ưu tiên dùng refresh_token còn hạn để đỡ gọi lại client_credentials, và lưu token vào
/// TenantOAuthToken để tái sử dụng giữa các lần chạy job.
/// </summary>
public interface ITenantTokenManager
{
    Task<TokenKetQua> LayAccessTokenAsync(string tenantId, CancellationToken ct = default);
}
