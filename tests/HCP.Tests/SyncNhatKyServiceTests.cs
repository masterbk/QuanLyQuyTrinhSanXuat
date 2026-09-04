using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.NhatKyDongBo;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng nhật ký đồng bộ chỉ cho cơ sở thao tác trên dữ liệu CỦA CHÍNH MÌNH.
/// SyncOutbox không có global query filter nên đây là hàng rào cách ly duy nhất - phải chắc.
/// </summary>
public class SyncNhatKyServiceTests
{
    private const string CoSoA = "coso-a";
    private const string CoSoB = "coso-b";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDbKhongTenant()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.ClearTenant();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private SyncNhatKyService TaoService(string tenantId)
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new SyncNhatKyService(new AppDbContext(accessor, options), accessor);
    }

    private long SeedItem(string tenantId, SyncOutboxStatus status)
    {
        using var db = MoDbKhongTenant();
        var item = new SyncOutboxItem
        {
            TenantId = tenantId,
            EntityType = "Warehouse",
            EntityKey = "KHO01",
            PayloadJson = "[{}]",
            Status = status,
            Attempts = 3,
            NextRetryAtUtc = DateTime.UtcNow.AddMinutes(10),
            LastError = "lỗi cũ"
        };
        db.SyncOutboxItems.Add(item);
        db.SaveChanges();
        return item.Id;
    }

    [Fact]
    public async Task Chi_Thay_Ban_Ghi_Cua_Chinh_Co_So()
    {
        SeedItem(CoSoA, SyncOutboxStatus.Success);
        SeedItem(CoSoA, SyncOutboxStatus.Failed);
        SeedItem(CoSoB, SyncOutboxStatus.Failed);

        var ds = await TaoService(CoSoA).LayDanhSachAsync();

        Assert.Equal(2, ds.Count);
        Assert.All(ds, o => Assert.Equal(CoSoA, o.TenantId));
    }

    [Fact]
    public async Task Dem_Theo_Trang_Thai_Chi_Tinh_Co_So_Minh()
    {
        SeedItem(CoSoA, SyncOutboxStatus.Success);
        SeedItem(CoSoA, SyncOutboxStatus.Success);
        SeedItem(CoSoA, SyncOutboxStatus.Failed);
        SeedItem(CoSoB, SyncOutboxStatus.Failed);

        var thongKe = await TaoService(CoSoA).DemTheoTrangThaiAsync();

        Assert.Equal(2, thongKe[SyncOutboxStatus.Success]);
        Assert.Equal(1, thongKe[SyncOutboxStatus.Failed]);
    }

    [Fact]
    public async Task Gui_Lai_Ban_Ghi_Loi_Thi_Dat_Lai_Pending()
    {
        var id = SeedItem(CoSoA, SyncOutboxStatus.NeedsManualReview);

        var kq = await TaoService(CoSoA).GuiLaiAsync(id);
        Assert.True(kq.ThanhCong, kq.ThongBao);

        using var db = MoDbKhongTenant();
        var item = db.SyncOutboxItems.Single(o => o.Id == id);
        Assert.Equal(SyncOutboxStatus.Pending, item.Status);
        Assert.Equal(0, item.Attempts);
        Assert.Null(item.NextRetryAtUtc);
        Assert.Null(item.LastError);
    }

    [Fact]
    public async Task Khong_Gui_Lai_Ban_Ghi_Da_Thanh_Cong()
    {
        var id = SeedItem(CoSoA, SyncOutboxStatus.Success);

        var kq = await TaoService(CoSoA).GuiLaiAsync(id);

        Assert.False(kq.ThanhCong);
        Assert.Equal(SyncOutboxStatus.Success, MoDbKhongTenant().SyncOutboxItems.Single(o => o.Id == id).Status);
    }

    [Fact]
    public async Task Khong_Gui_Lai_Duoc_Ban_Ghi_Cua_Co_So_Khac()
    {
        // Cơ sở B tạo bản ghi lỗi; cơ sở A cố gửi lại bằng đúng id -> phải bị chặn.
        var idCuaB = SeedItem(CoSoB, SyncOutboxStatus.Failed);

        var kq = await TaoService(CoSoA).GuiLaiAsync(idCuaB);

        Assert.False(kq.ThanhCong);
        // Bản ghi của B vẫn nguyên trạng Failed, không bị A đụng vào.
        Assert.Equal(SyncOutboxStatus.Failed, MoDbKhongTenant().SyncOutboxItems.Single(o => o.Id == idCuaB).Status);
    }
}
