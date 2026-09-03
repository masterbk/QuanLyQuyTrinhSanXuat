using Finbuckle.MultiTenant.EntityFrameworkCore.Stores.EFCoreStore;
using HCP.Domain.Entities.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HCP.Infrastructure.Persistence;

/// <summary>
/// DbContext tối giản CHỈ phục vụ Finbuckle tra cứu tenant ở đầu mỗi request.
/// Tách riêng khỏi <see cref="AppDbContext"/> để việc resolve tenant không kéo theo
/// toàn bộ model nghiệp vụ. Hai context trỏ về cùng một database.
/// </summary>
public class TenantStoreDbContext : EFCoreStoreDbContext<Tenant>
{
    public TenantStoreDbContext(DbContextOptions<TenantStoreDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("Tenants");

            // Credential và token do AppDbContext sở hữu - context này chỉ cần
            // thông tin định danh cơ sở để resolve tenant, giữ cho việc tra cứu nhẹ nhất.
            b.Ignore(t => t.HnCCredential);
            b.Ignore(t => t.OAuthToken);

            b.Property(t => t.EmailLienHe).HasMaxLength(256).IsRequired();
            b.Property(t => t.MaSoThue).HasMaxLength(20);
            b.Property(t => t.DiaChi).HasMaxLength(500);
            b.Property(t => t.NguoiDaiDien).HasMaxLength(255);
            b.Property(t => t.SoDienThoai).HasMaxLength(20);
            b.Property(t => t.GiayChungNhanAttpPath).HasMaxLength(1000);
            b.Property(t => t.LyDoTuChoi).HasMaxLength(1000);
            b.Property(t => t.NguoiDuyet).HasMaxLength(256);
        });
    }
}
