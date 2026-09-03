using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using HCP.Domain.Entities.Business;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Persistence;

/// <summary>
/// DbContext chính: dữ liệu Identity + toàn bộ dữ liệu nghiệp vụ của các cơ sở.
///
/// Mọi entity được đánh dấu .IsMultiTenant() sẽ tự động:
///   - được gán TenantId khi ghi (SaveChanges gọi EnforceMultiTenant),
///   - bị lọc theo TenantId hiện tại khi đọc (global query filter).
/// Đây là hàng rào chống rò rỉ dữ liệu chéo giữa các cơ sở. Xem HCP.Tests/TenantIsolationTests.
///
/// CỐ Ý KHÔNG kế thừa MultiTenantIdentityDbContext: lớp đó lọc luôn cả bảng Identity
/// (thậm chí chèn TenantId vào khoá chính của AspNetUserLogins). Điều đó phá vỡ đăng nhập,
/// vì lúc xác thực chưa hề có tenant context - claim tenantId chỉ tồn tại SAU khi đăng nhập
/// thành công. Bảng Identity vì vậy để ở phạm vi toàn hệ thống; việc người dùng thuộc cơ sở nào
/// do cột ApplicationUser.TenantId và tầng ứng dụng kiểm soát.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>, IMultiTenantDbContext
{
    private readonly IMultiTenantContextAccessor _multiTenantContextAccessor;

    public AppDbContext(IMultiTenantContextAccessor multiTenantContextAccessor,
                        DbContextOptions<AppDbContext> options)
        : base(options)
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
    }

    public ITenantInfo? TenantInfo => _multiTenantContextAccessor.MultiTenantContext?.TenantInfo;

    /// <summary>Gán TenantId khác tenant đang đăng nhập -> NÉM LỖI, không âm thầm bỏ qua.
    /// Chốt chặn cuối cùng nếu tầng ứng dụng để lọt request giả mạo Id.</summary>
    public TenantMismatchMode TenantMismatchMode => TenantMismatchMode.Throw;

    /// <summary>Chưa gán TenantId khi tạo mới -> tự điền theo tenant đang đăng nhập.</summary>
    public TenantNotSetMode TenantNotSetMode => TenantNotSetMode.Overwrite;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.EnforceMultiTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
                                               CancellationToken cancellationToken = default)
    {
        this.EnforceMultiTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // --- Bảng hạ tầng (không lọc theo tenant: job nền và platform admin cần truy vấn xuyên tenant) ---
    public DbSet<TenantHnCCredential> TenantHnCCredentials => Set<TenantHnCCredential>();
    public DbSet<TenantOAuthToken> TenantOAuthTokens => Set<TenantOAuthToken>();
    public DbSet<SyncOutboxItem> SyncOutboxItems => Set<SyncOutboxItem>();
    public DbSet<SystemLogEntry> SystemLogs => Set<SystemLogEntry>();

    // --- Bảng nghiệp vụ (LỌC theo tenant) ---
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    // TODO (Giai đoạn 2+): Facility, ProductionStep, ProductionProcess, Staff, SubSupplier,
    // Product, Batch, Dish, Order... Mỗi entity nghiệp vụ PHẢI kế thừa TenantEntity
    // và gọi .IsMultiTenant() ở dưới.

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Người dùng thuộc về tenant nào - lọc để TenantAdmin chỉ thấy nhân sự cơ sở mình.
        // (PlatformSuperAdmin có TenantId null nên không nằm trong bất kỳ tenant nào.)
        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.TenantId).HasMaxLength(64);
            b.Property(u => u.HoTen).HasMaxLength(255);
            b.HasIndex(u => u.TenantId);
        });

        // Bảng Tenants do TenantStoreDbContext sở hữu (đó là sổ đăng ký cơ sở).
        // AppDbContext không map lại, nếu không EF sẽ sinh thêm một bảng "Tenant" trùng lặp
        // qua các navigation bên dưới. Ràng buộc giữa 2 context được đảm bảo ở tầng ứng dụng.
        builder.Ignore<Tenant>();

        builder.Entity<TenantHnCCredential>(b =>
        {
            b.ToTable("TenantHnCCredentials");
            b.HasKey(c => c.TenantId);
            b.Ignore(c => c.Tenant);
            b.Property(c => c.TenantId).HasMaxLength(64);
            b.Property(c => c.BaseUrl).HasMaxLength(500).IsRequired();
            b.Property(c => c.ClientId).HasMaxLength(255).IsRequired();
            b.Property(c => c.ClientSecretEncrypted).HasMaxLength(2000).IsRequired();
            b.Property(c => c.HmacSecretEncrypted).HasMaxLength(2000).IsRequired();
            b.Property(c => c.LoiXacThucGanNhat).HasMaxLength(2000);
        });

        builder.Entity<TenantOAuthToken>(b =>
        {
            b.ToTable("TenantOAuthTokens");
            b.HasKey(t => t.TenantId);
            b.Ignore(t => t.Tenant);
            b.Property(t => t.TenantId).HasMaxLength(64);
            b.Property(t => t.AccessToken).HasMaxLength(4000).IsRequired();
            b.Property(t => t.RefreshToken).HasMaxLength(4000).IsRequired();
        });

        builder.Entity<SyncOutboxItem>(b =>
        {
            b.ToTable("SyncOutbox");
            b.Property(o => o.TenantId).HasMaxLength(64).IsRequired();
            b.Property(o => o.EntityType).HasMaxLength(100).IsRequired();
            b.Property(o => o.EntityKey).HasMaxLength(255).IsRequired();
            b.Property(o => o.LastError).HasMaxLength(4000);

            // Job nền quét theo (Status, NextRetryAt) rồi gom theo TenantId.
            b.HasIndex(o => new { o.Status, o.NextRetryAtUtc });
            b.HasIndex(o => new { o.TenantId, o.EntityType, o.EntityKey });
        });

        builder.Entity<SystemLogEntry>(b =>
        {
            b.ToTable("SystemLogs");
            b.Property(l => l.TenantId).HasMaxLength(64);
            b.Property(l => l.Level).HasMaxLength(32).IsRequired();
            b.Property(l => l.Message).HasMaxLength(4000).IsRequired();
            b.Property(l => l.HttpMethod).HasMaxLength(10);
            b.Property(l => l.RequestUrl).HasMaxLength(1000);
            b.Property(l => l.UserId).HasMaxLength(450);

            // Truy vấn nhật ký theo cơ sở và theo thời gian (log giữ tối thiểu 2 năm).
            b.HasIndex(l => new { l.TenantId, l.TimestampUtc });
        });

        // --- Entity nghiệp vụ: BẮT BUỘC .IsMultiTenant() để cách ly dữ liệu giữa các cơ sở ---
        var warehouse = builder.Entity<Warehouse>();
        warehouse.ToTable("Warehouses");
        warehouse.Property(w => w.MaKho).HasMaxLength(255).IsRequired();
        warehouse.Property(w => w.TenKho).HasMaxLength(255).IsRequired();
        warehouse.Property(w => w.DiaChi).HasMaxLength(255).IsRequired();
        warehouse.Property(w => w.DienTich).HasPrecision(8, 2);

        // Mã kho chỉ cần duy nhất TRONG một cơ sở, không phải toàn hệ thống.
        // AdjustUniqueIndexes() tự thêm TenantId vào index để đảm bảo điều đó.
        warehouse.HasIndex(w => w.MaKho).IsUnique();
        warehouse.IsMultiTenant().AdjustUniqueIndexes();

        // Áp dụng cấu hình multi-tenant cho các entity đã đánh dấu .IsMultiTenant().
        builder.ConfigureMultiTenant();
    }
}
