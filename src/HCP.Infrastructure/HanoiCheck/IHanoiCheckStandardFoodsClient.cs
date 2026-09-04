namespace HCP.Infrastructure.HanoiCheck;

/// <summary>
/// Lấy danh mục thực phẩm chuẩn: GET /supplier/standard-foods. Chỉ cần OAuth2 (KHÔNG ký HMAC,
/// đặc tả mục 2). Danh mục này do HnC ban hành, dùng chung mọi cơ sở.
/// </summary>
public interface IHanoiCheckStandardFoodsClient
{
    Task<StandardFoodsResult> LayDanhMucAsync(string baseUrl, string accessToken, CancellationToken ct = default);
}
