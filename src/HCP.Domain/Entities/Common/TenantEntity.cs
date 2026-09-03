namespace HCP.Domain.Entities.Common;

/// <summary>
/// Lớp cơ sở cho MỌI entity nghiệp vụ thuộc về một cơ sở sản xuất (tenant).
/// Cột TenantId được Finbuckle tự động gán khi ghi và tự động lọc khi đọc
/// (xem AppDbContext.OnModelCreating - .IsMultiTenant()).
///
/// QUAN TRỌNG: mọi entity nghiệp vụ mới PHẢI kế thừa lớp này VÀ được đăng ký
/// .IsMultiTenant() trong AppDbContext, nếu không sẽ rò rỉ dữ liệu chéo giữa các cơ sở.
/// </summary>
public abstract class TenantEntity : AuditableEntity
{
    /// <summary>
    /// Để null khi tạo mới - Finbuckle tự điền theo tenant đang đăng nhập
    /// (TenantNotSetMode.Overwrite). Gán sẵn một giá trị khác tenant hiện hành
    /// sẽ bị chặn ở SaveChanges (TenantMismatchMode.Throw).
    /// </summary>
    public string TenantId { get; set; } = null!;
}
