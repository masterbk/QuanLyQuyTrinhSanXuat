namespace HCP.Infrastructure.Services.DanhMuc;

/// <summary>
/// Thao tác CRUD chung cho các danh mục lõi của một cơ sở.
///
/// KHÔNG nhận tenantId làm tham số: AppDbContext đã tự lọc và tự gán TenantId theo
/// người đang đăng nhập. Truyền tenantId thủ công ở đây sẽ mở đường cho lỗi
/// truy cập chéo cơ sở.
/// </summary>
public interface IDanhMucService<T> where T : class
{
    Task<IReadOnlyList<T>> LayTatCaAsync(CancellationToken ct = default);

    Task<T?> LayTheoIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Đọc bản mới nhất để ĐỔ VÀO FORM SỬA. Khác <see cref="LayTheoIdAsync"/> ở chỗ KHÔNG theo dõi thay đổi:
    /// trong Blazor Server, DbContext sống suốt phiên làm việc của người dùng, nên nếu bản ghi đã được đọc
    /// trước đó thì truy vấn có theo dõi sẽ trả lại ĐÚNG BẢN CŨ đang giữ trong bộ nhớ, không lấy giá trị mới
    /// dưới CSDL (đã tái hiện thật: mở form sửa lần hai trong cùng phiên vẫn ra dữ liệu cũ).
    /// Dùng cho hiển thị; muốn lưu thì vẫn đi qua CapNhatAsync.
    /// </summary>
    Task<T?> LayDeSuaAsync(int id, CancellationToken ct = default);

    Task<KetQuaThaoTac> ThemAsync(T entity, CancellationToken ct = default);

    Task<KetQuaThaoTac> CapNhatAsync(T entity, CancellationToken ct = default);

    Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default);
}
