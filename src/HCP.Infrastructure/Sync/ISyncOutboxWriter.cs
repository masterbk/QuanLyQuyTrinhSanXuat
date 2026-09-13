namespace HCP.Infrastructure.Sync;

/// <summary>
/// Ghi bản ghi vào hàng đợi đồng bộ cho cơ sở ĐANG ĐĂNG NHẬP.
///
/// Các service danh mục gọi <see cref="GuiAsync"/> NGAY SAU khi lưu dữ liệu, trong cùng luồng request.
/// Hàng đợi chỉ nhận bản ghi khi cơ sở BẬT đồng bộ HanoiCheck (công tắc tổng) và bản ghi đó được tick
/// "Đồng bộ HanoiCheck"; job nền tự đẩy đi, không cần bấm "gửi".
/// </summary>
public interface ISyncOutboxWriter
{
    /// <summary>
    /// Ghi thẳng một payload vào hàng đợi (mức thấp - bỏ qua khi công tắc tổng tắt).
    /// </summary>
    /// <param name="entityType">Khoá trong <see cref="HanoiCheck.SyncEndpoints"/>, vd "Warehouse".</param>
    /// <param name="entityKey">Khoá nghiệp vụ để upsert phía HnC, vd mã kho.</param>
    /// <param name="payload">Đối tượng sẽ được tuần tự hoá thành body JSON gửi HnC
    /// (thường là một MẢNG bản ghi theo đúng định dạng endpoint merge).</param>
    Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default);

    /// <summary>Công tắc tổng HanoiCheck của cơ sở hiện hành có đang bật không.</summary>
    Task<bool> DangBatAsync(CancellationToken ct = default);

    /// <summary>
    /// Gửi một bản ghi danh mục (kho, cơ sở, khâu, quy trình, NCC, nhân sự, thành phẩm, lô, món ăn) theo cờ đồng bộ
    /// của nó: không tick thì gỡ các lần gửi đang chờ của bản ghi; có tick thì TỰ BẬT + gửi trước các bản ghi nó phụ
    /// thuộc (vd thành phẩm → quy trình → khâu) để HanoiCheck không báo 422.
    /// Trả ghi chú cho người dùng khi có bật thêm bản ghi phụ thuộc, ngược lại null.
    /// </summary>
    Task<string?> GuiAsync(object banGhi, CancellationToken ct = default);
}
