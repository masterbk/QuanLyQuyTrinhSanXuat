namespace HCP.Infrastructure.HanoiCheck;

/// <summary>
/// Kết quả gọi endpoint token/refresh_token của HanoiCheck khi cần token thật để đồng bộ
/// (khác <see cref="ConnectionTestResult"/> vốn chỉ phục vụ nút "Kiểm tra kết nối" trên UI).
/// </summary>
public sealed record TokenFetchResult(
    bool ThanhCong,
    HanoiCheckTokenResponse? Token,
    int? HttpStatusCode,
    string? ThongBao)
{
    public static TokenFetchResult Ok(HanoiCheckTokenResponse token) => new(true, token, 200, null);

    public static TokenFetchResult Loi(string thongBao, int? httpStatusCode = null) =>
        new(false, null, httpStatusCode, thongBao);
}
