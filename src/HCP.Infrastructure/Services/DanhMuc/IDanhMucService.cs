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

    Task<KetQuaThaoTac> ThemAsync(T entity, CancellationToken ct = default);

    Task<KetQuaThaoTac> CapNhatAsync(T entity, CancellationToken ct = default);

    Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default);
}
