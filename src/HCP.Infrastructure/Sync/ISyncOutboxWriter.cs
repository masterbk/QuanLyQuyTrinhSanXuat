namespace HCP.Infrastructure.Sync;

/// <summary>
/// Ghi bản ghi vào hàng đợi đồng bộ cho cơ sở ĐANG ĐĂNG NHẬP.
///
/// Các service danh mục gọi hàm này NGAY SAU khi lưu dữ liệu, trong cùng luồng request.
/// Nhờ đó việc đồng bộ là tự động (đúng yêu cầu): người dùng lưu là hàng đợi có bản ghi,
/// job nền tự đẩy đi, không cần bấm "gửi".
/// </summary>
public interface ISyncOutboxWriter
{
    /// <param name="entityType">Khoá trong <see cref="HanoiCheck.SyncEndpoints"/>, vd "Warehouse".</param>
    /// <param name="entityKey">Khoá nghiệp vụ để upsert phía HnC, vd mã kho.</param>
    /// <param name="payload">Đối tượng sẽ được tuần tự hoá thành body JSON gửi HnC
    /// (thường là một MẢNG bản ghi theo đúng định dạng endpoint merge).</param>
    Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default);
}
