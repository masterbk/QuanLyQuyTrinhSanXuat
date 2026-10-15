namespace HCP.Infrastructure.HanoiCheck;

/// <summary>
/// Lấy danh mục thực phẩm chuẩn: GET /supplier/standard-foods. Chỉ cần OAuth2 (KHÔNG ký HMAC,
/// đặc tả mục 2). Danh mục này do HnC ban hành, dùng chung mọi cơ sở.
/// </summary>
public interface IHanoiCheckStandardFoodsClient
{
    Task<StandardFoodsResult> LayDanhMucAsync(string baseUrl, string accessToken, CancellationToken ct = default);

    /// <summary>GET /supplier/measures - danh mục đơn vị tính (đặc tả v2.6, Phần V mục 4). Không cần ký.</summary>
    Task<MeasuresResult> LayDonViTinhAsync(string baseUrl, string accessToken, CancellationToken ct = default);

    /// <summary>GET /supplier/student-groups - danh mục nhóm tuổi (đặc tả v2.6, Phần V mục 2). Không cần ký.</summary>
    Task<MeasuresResult> LayNhomTuoiAsync(string baseUrl, string accessToken, CancellationToken ct = default);

    /// <summary>GET /supplier/meal-types - danh mục bữa ăn, đã sắp theo thứ tự trong ngày (Phần V mục 3). Không cần ký.</summary>
    Task<MeasuresResult> LayBuaAnAsync(string baseUrl, string accessToken, CancellationToken ct = default);
}
