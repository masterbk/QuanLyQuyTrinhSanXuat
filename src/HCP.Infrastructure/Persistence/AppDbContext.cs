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
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<ProductionStep> ProductionSteps => Set<ProductionStep>();
    public DbSet<ProductionProcess> ProductionProcesses => Set<ProductionProcess>();
    public DbSet<ProcessStepLine> ProcessStepLines => Set<ProcessStepLine>();
    public DbSet<SubSupplier> SubSuppliers => Set<SubSupplier>();
    public DbSet<SubSupplierFoodGroup> SubSupplierFoodGroups => Set<SubSupplierFoodGroup>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<BatchWarehouse> BatchWarehouses => Set<BatchWarehouse>();
    public DbSet<BatchStep> BatchSteps => Set<BatchStep>();
    public DbSet<BatchFile> BatchFiles => Set<BatchFile>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<DishIngredient> DishIngredients => Set<DishIngredient>();
    public DbSet<DishStep> DishSteps => Set<DishStep>();
    public DbSet<DishFile> DishFiles => Set<DishFile>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderImage> OrderImages => Set<OrderImage>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderExport> OrderExports => Set<OrderExport>();

    /// <summary>Danh mục do HanoiCheck ban hành - dùng chung mọi cơ sở, KHÔNG lọc theo tenant.</summary>
    public DbSet<StandardFoodCategory> StandardFoodCategories => Set<StandardFoodCategory>();

    // TODO (Giai đoạn 3+): Staff, Product, Batch, Dish, Order...
    // Mỗi entity nghiệp vụ PHẢI kế thừa TenantEntity và gọi .IsMultiTenant() ở dưới.

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

        var facility = builder.Entity<Facility>();
        facility.ToTable("Facilities");
        facility.Property(f => f.MaCoSo).HasMaxLength(255).IsRequired();
        facility.Property(f => f.TenCoSo).HasMaxLength(255).IsRequired();
        facility.Property(f => f.DiaChi).HasMaxLength(255);
        facility.HasIndex(f => f.MaCoSo).IsUnique();
        facility.IsMultiTenant().AdjustUniqueIndexes();

        var step = builder.Entity<ProductionStep>();
        step.ToTable("ProductionSteps");
        step.Property(s => s.MaKhau).HasMaxLength(255).IsRequired();
        step.Property(s => s.TenKhau).HasMaxLength(255).IsRequired();
        step.Property(s => s.GhiChu).HasMaxLength(1000);
        step.HasIndex(s => s.MaKhau).IsUnique();
        step.IsMultiTenant().AdjustUniqueIndexes();

        var process = builder.Entity<ProductionProcess>();
        process.ToTable("ProductionProcesses");
        process.Property(p => p.MaQuyTrinh).HasMaxLength(255).IsRequired();
        process.Property(p => p.TenQuyTrinh).HasMaxLength(255).IsRequired();
        process.HasMany(p => p.DanhSachKhau)
               .WithOne(l => l.ProductionProcess!)
               .HasForeignKey(l => l.ProductionProcessId)
               .OnDelete(DeleteBehavior.Cascade);
        process.HasIndex(p => p.MaQuyTrinh).IsUnique();
        process.IsMultiTenant().AdjustUniqueIndexes();

        // Bảng con CŨNG phải lọc theo tenant. Nếu chỉ dựa vào quy trình cha, một truy vấn
        // trực tiếp vào bảng này (vd đếm quy trình đang dùng một mã khâu) sẽ quét dữ liệu
        // của mọi cơ sở - mã khâu là mã chuẩn nên trùng nhau giữa các cơ sở là chuyện thường.
        var stepLine = builder.Entity<ProcessStepLine>();
        stepLine.ToTable("ProcessStepLines");
        stepLine.Property(l => l.MaKhau).HasMaxLength(255).IsRequired();
        stepLine.HasIndex(l => new { l.ProductionProcessId, l.ThuTu });
        stepLine.IsMultiTenant();

        var subSupplier = builder.Entity<SubSupplier>();
        subSupplier.ToTable("SubSuppliers");
        subSupplier.Property(s => s.MaNccDauVao).HasMaxLength(255).IsRequired();
        subSupplier.Property(s => s.Ten).HasMaxLength(255).IsRequired();
        subSupplier.Property(s => s.MaSoThue).HasMaxLength(20);
        subSupplier.Property(s => s.DiaChi).HasMaxLength(500);
        subSupplier.Property(s => s.DienThoai).HasMaxLength(20);
        subSupplier.Property(s => s.AttpSoGiay).HasMaxLength(255);
        subSupplier.Property(s => s.HopDongSo).HasMaxLength(255);
        subSupplier.Ignore(s => s.CoGiayChungNhanAttp);
        subSupplier.Ignore(s => s.CoHopDong);
        subSupplier.HasMany(s => s.NhomThucPham)
                   .WithOne(g => g.SubSupplier!)
                   .HasForeignKey(g => g.SubSupplierId)
                   .OnDelete(DeleteBehavior.Cascade);
        subSupplier.HasIndex(s => s.MaNccDauVao).IsUnique();
        subSupplier.IsMultiTenant().AdjustUniqueIndexes();

        var foodGroup = builder.Entity<SubSupplierFoodGroup>();
        foodGroup.ToTable("SubSupplierFoodGroups");
        foodGroup.Property(g => g.MaNhom).HasMaxLength(100).IsRequired();
        foodGroup.HasIndex(g => new { g.SubSupplierId, g.MaNhom }).IsUnique();
        foodGroup.IsMultiTenant().AdjustUniqueIndexes();

        var staff = builder.Entity<Staff>();
        staff.ToTable("Staff");
        staff.Property(s => s.MaNhanSu).HasMaxLength(255).IsRequired();
        staff.Property(s => s.HoTen).HasMaxLength(255).IsRequired();
        staff.Property(s => s.ViTri).HasMaxLength(255);
        staff.Property(s => s.DiaChi).HasMaxLength(500);
        staff.Property(s => s.DienThoai).HasMaxLength(20);
        // CccdEncrypted giữ bản mã hoá; Cccd (plaintext) chỉ ở bộ nhớ, không map DB.
        staff.Property(s => s.CccdEncrypted).HasMaxLength(2000);
        staff.Ignore(s => s.Cccd);
        staff.Ignore(s => s.CoGiayKhamSucKhoe);
        staff.Ignore(s => s.CoChungNhanAttp);
        staff.Property(s => s.PhuongTien).HasMaxLength(255);
        staff.Property(s => s.BienSo).HasMaxLength(50);
        staff.Property(s => s.KskSoGiay).HasMaxLength(255);
        staff.Property(s => s.KskNoiKham).HasMaxLength(255);
        staff.Property(s => s.AttpSoChungNhan).HasMaxLength(255);
        staff.Property(s => s.AttpCoQuanCap).HasMaxLength(255);
        staff.HasIndex(s => s.MaNhanSu).IsUnique();
        staff.IsMultiTenant().AdjustUniqueIndexes();

        var product = builder.Entity<Product>();
        product.ToTable("Products");
        product.Property(p => p.MaSanPham).HasMaxLength(255).IsRequired();
        product.Property(p => p.TenSanPham).HasMaxLength(255).IsRequired();
        product.Property(p => p.MaLoaiSp).HasMaxLength(100).IsRequired();
        product.Property(p => p.MaThucPhamChuan).HasMaxLength(100);
        product.Property(p => p.Gtin).HasMaxLength(50);
        product.Property(p => p.QuocGia).HasMaxLength(100);
        product.Property(p => p.MoTa).HasMaxLength(2000);
        product.Property(p => p.MaQuyTrinh).HasMaxLength(255);
        product.HasIndex(p => p.MaSanPham).IsUnique();
        product.IsMultiTenant().AdjustUniqueIndexes();

        // --- Lô sản xuất và các bảng con ---
        var batch = builder.Entity<Batch>();
        batch.ToTable("Batches");
        batch.Property(b => b.MaSanPham).HasMaxLength(255).IsRequired();
        batch.Property(b => b.MaLo).HasMaxLength(255).IsRequired();
        batch.Property(b => b.TenLo).HasMaxLength(255).IsRequired();
        batch.Property(b => b.DiaChiThuMua).HasMaxLength(2000);
        batch.Property(b => b.MaCoSo).HasMaxLength(255);
        batch.Property(b => b.MaNccDauVao).HasMaxLength(255);
        batch.Property(b => b.GhiChu).HasMaxLength(2000);
        batch.HasMany(b => b.DanhSachKho).WithOne(w => w.Batch!)
             .HasForeignKey(w => w.BatchId).OnDelete(DeleteBehavior.Cascade);
        batch.HasMany(b => b.DanhSachKhau).WithOne(s => s.Batch!)
             .HasForeignKey(s => s.BatchId).OnDelete(DeleteBehavior.Cascade);
        batch.HasMany(b => b.DanhSachFile).WithOne(f => f.Batch!)
             .HasForeignKey(f => f.BatchId).OnDelete(DeleteBehavior.Cascade);
        batch.HasIndex(b => b.MaLo).IsUnique();
        batch.IsMultiTenant().AdjustUniqueIndexes();

        var batchKho = builder.Entity<BatchWarehouse>();
        batchKho.ToTable("BatchWarehouses");
        batchKho.Property(w => w.MaKho).HasMaxLength(255).IsRequired();
        batchKho.HasIndex(w => new { w.BatchId, w.MaKho }).IsUnique();
        batchKho.IsMultiTenant().AdjustUniqueIndexes();

        var batchStep = builder.Entity<BatchStep>();
        batchStep.ToTable("BatchSteps");
        batchStep.Property(s => s.MaBuocSx).HasMaxLength(255).IsRequired();
        batchStep.Property(s => s.MaKhau).HasMaxLength(255).IsRequired();
        batchStep.Property(s => s.MaLoNhap).HasMaxLength(255);
        batchStep.Property(s => s.MaLoNguyenLieu).HasMaxLength(255);
        batchStep.Property(s => s.MaLoSanXuat).HasMaxLength(255);
        batchStep.Property(s => s.NguoiThucHienCsv).HasMaxLength(1000);
        batchStep.Property(s => s.DiaChi).HasMaxLength(500);
        batchStep.Property(s => s.TrangThai).HasMaxLength(100);
        batchStep.Property(s => s.MaQrTruyVet).HasMaxLength(255);
        batchStep.Property(s => s.GhiChu).HasMaxLength(1000);
        batchStep.Property(s => s.MaCoSo).HasMaxLength(255);
        batchStep.Property(s => s.MaNccDauVao).HasMaxLength(255);
        batchStep.Ignore(s => s.NguoiThucHien);
        batchStep.HasIndex(s => new { s.BatchId, s.MaBuocSx }).IsUnique();
        batchStep.IsMultiTenant().AdjustUniqueIndexes();

        var batchFile = builder.Entity<BatchFile>();
        batchFile.ToTable("BatchFiles");
        batchFile.Property(f => f.MaFile).HasMaxLength(255).IsRequired();
        batchFile.Property(f => f.TenFile).HasMaxLength(255).IsRequired();
        batchFile.Property(f => f.DuongDan).HasMaxLength(1000).IsRequired();
        batchFile.Property(f => f.Loai).HasMaxLength(100).IsRequired();
        batchFile.Property(f => f.MaKhau).HasMaxLength(255);
        batchFile.Property(f => f.MaBuocSx).HasMaxLength(255);
        batchFile.HasIndex(f => new { f.BatchId, f.MaFile }).IsUnique();
        batchFile.IsMultiTenant().AdjustUniqueIndexes();

        // --- Món ăn và các bảng con ---
        var dish = builder.Entity<Dish>();
        dish.ToTable("Dishes");
        dish.Property(d => d.MaMonAn).HasMaxLength(255).IsRequired();
        dish.Property(d => d.TenMonAn).HasMaxLength(255).IsRequired();
        dish.Property(d => d.MoTa).HasMaxLength(2000);
        dish.Property(d => d.MaCoSo).HasMaxLength(255);
        dish.Property(d => d.MaQuyTrinh).HasMaxLength(255);
        dish.HasMany(d => d.DanhSachNguyenLieu).WithOne(i => i.Dish!)
            .HasForeignKey(i => i.DishId).OnDelete(DeleteBehavior.Cascade);
        dish.HasMany(d => d.DanhSachKhau).WithOne(s => s.Dish!)
            .HasForeignKey(s => s.DishId).OnDelete(DeleteBehavior.Cascade);
        dish.HasMany(d => d.DanhSachFile).WithOne(f => f.Dish!)
            .HasForeignKey(f => f.DishId).OnDelete(DeleteBehavior.Cascade);
        dish.HasIndex(d => d.MaMonAn).IsUnique();
        dish.IsMultiTenant().AdjustUniqueIndexes();

        var dishNL = builder.Entity<DishIngredient>();
        dishNL.ToTable("DishIngredients");
        dishNL.Property(i => i.MaNguyenLieu).HasMaxLength(255).IsRequired();
        dishNL.Property(i => i.DinhLuong).HasPrecision(18, 4);
        dishNL.HasIndex(i => new { i.DishId, i.MaNguyenLieu }).IsUnique();
        dishNL.IsMultiTenant().AdjustUniqueIndexes();

        var dishStep = builder.Entity<DishStep>();
        dishStep.ToTable("DishSteps");
        dishStep.Property(s => s.MaBuocSx).HasMaxLength(255);
        dishStep.Property(s => s.MaKhau).HasMaxLength(255).IsRequired();
        dishStep.Property(s => s.NguoiThucHienCsv).HasMaxLength(1000);
        dishStep.Property(s => s.DiaChi).HasMaxLength(500);
        dishStep.Property(s => s.TrangThai).HasMaxLength(100);
        dishStep.Property(s => s.MaQrTruyVet).HasMaxLength(255);
        dishStep.Property(s => s.GhiChu).HasMaxLength(1000);
        dishStep.Property(s => s.MaCoSo).HasMaxLength(255);
        dishStep.Property(s => s.MaNccDauVao).HasMaxLength(255);
        dishStep.Ignore(s => s.NguoiThucHien);
        dishStep.HasIndex(s => new { s.DishId, s.ThuTu });
        dishStep.IsMultiTenant().AdjustUniqueIndexes();

        var dishFile = builder.Entity<DishFile>();
        dishFile.ToTable("DishFiles");
        dishFile.Property(f => f.MaFile).HasMaxLength(255).IsRequired();
        dishFile.Property(f => f.TenFile).HasMaxLength(255).IsRequired();
        dishFile.Property(f => f.DuongDan).HasMaxLength(1000).IsRequired();
        dishFile.Property(f => f.Loai).HasMaxLength(100).IsRequired();
        dishFile.Property(f => f.MaKhau).HasMaxLength(255);
        dishFile.Property(f => f.MaBuocSx).HasMaxLength(255);
        dishFile.HasIndex(f => new { f.DishId, f.MaFile }).IsUnique();
        dishFile.IsMultiTenant().AdjustUniqueIndexes();

        // --- Đơn hàng và các bảng con ---
        var order = builder.Entity<Order>();
        order.ToTable("Orders");
        order.Property(o => o.MaDonHang).HasMaxLength(255).IsRequired();
        order.Property(o => o.LoaiDonHang).HasMaxLength(20).IsRequired();
        order.Property(o => o.MaTruong).HasMaxLength(255).IsRequired();
        order.Property(o => o.DiaChiNhan).HasMaxLength(500);
        order.Property(o => o.DiemGiao).HasMaxLength(255);
        order.Property(o => o.MaNguoiGiao).HasMaxLength(255);
        order.Property(o => o.TrangThai).HasMaxLength(30).IsRequired();
        order.Property(o => o.GhiChu).HasMaxLength(2000);
        order.HasMany(o => o.Images).WithOne(i => i.Order!)
             .HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        order.HasMany(o => o.ChiTiet).WithOne(l => l.Order!)
             .HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        order.HasMany(o => o.XuatKho).WithOne(x => x.Order!)
             .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        order.HasIndex(o => o.MaDonHang).IsUnique();
        order.IsMultiTenant().AdjustUniqueIndexes();

        var orderImg = builder.Entity<OrderImage>();
        orderImg.ToTable("OrderImages");
        orderImg.Property(i => i.PathFile).HasMaxLength(1000).IsRequired();
        orderImg.HasIndex(i => new { i.OrderId, i.SortOrder }).IsUnique();
        orderImg.IsMultiTenant().AdjustUniqueIndexes();

        var orderLine = builder.Entity<OrderLine>();
        orderLine.ToTable("OrderLines");
        orderLine.Property(l => l.MaLoaiSp).HasMaxLength(100);
        orderLine.Property(l => l.MaMonAn).HasMaxLength(255);
        orderLine.Property(l => l.SoLuong).HasPrecision(18, 3);
        orderLine.Property(l => l.PathFile).HasMaxLength(1000);
        orderLine.HasIndex(l => l.OrderId);
        orderLine.IsMultiTenant().AdjustUniqueIndexes();

        var orderExport = builder.Entity<OrderExport>();
        orderExport.ToTable("OrderExports");
        orderExport.Property(x => x.MaXuatKho).HasMaxLength(255);
        orderExport.Property(x => x.MaLoaiSp).HasMaxLength(100);
        orderExport.Property(x => x.MaSanPham).HasMaxLength(255).IsRequired();
        orderExport.Property(x => x.MaKho).HasMaxLength(255).IsRequired();
        orderExport.Property(x => x.MaLo).HasMaxLength(255).IsRequired();
        orderExport.Property(x => x.SoLuong).HasPrecision(18, 3);
        orderExport.HasIndex(x => x.OrderId);
        orderExport.IsMultiTenant().AdjustUniqueIndexes();

        // Danh mục chuẩn của HanoiCheck: dùng chung, KHÔNG gọi IsMultiTenant().
        var standardFood = builder.Entity<StandardFoodCategory>();
        standardFood.ToTable("StandardFoodCategories");
        standardFood.Property(c => c.Code).HasMaxLength(100).IsRequired();
        standardFood.Property(c => c.Name).HasMaxLength(255).IsRequired();
        standardFood.Property(c => c.MeasureName).HasMaxLength(100);
        standardFood.HasIndex(c => c.Code).IsUnique();

        // Áp dụng cấu hình multi-tenant cho các entity đã đánh dấu .IsMultiTenant().
        builder.ConfigureMultiTenant();
    }
}
