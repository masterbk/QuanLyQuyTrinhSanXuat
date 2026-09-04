using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Logging;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng bộ xử lý hàng đợi đồng bộ: chuyển trạng thái đúng theo từng loại kết quả,
/// giãn cách retry tăng dần, và dừng đúng lúc khi hết số lần thử.
/// </summary>
public class SyncOutboxProcessorTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly DateTime _now = new(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.ClearTenant();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private long SeedItem(SyncOutboxStatus status = SyncOutboxStatus.Pending, int attempts = 0,
                         DateTime? nextRetry = null)
    {
        using var db = MoDb();
        var item = new SyncOutboxItem
        {
            TenantId = "coso-a",
            EntityType = "Warehouse",
            EntityKey = "KHO01",
            PayloadJson = "[{\"ma_kho\":\"KHO01\"}]",
            Status = status,
            Attempts = attempts,
            NextRetryAtUtc = nextRetry
        };
        db.SyncOutboxItems.Add(item);
        db.SaveChanges();
        return item.Id;
    }

    private (SyncOutboxProcessor, FakeSyncClient, FakeLogWriter) TaoProcessor(AppDbContext db, SyncSendResult ketQua)
    {
        var client = new FakeSyncClient { KetQua = ketQua };
        var log = new FakeLogWriter();
        var proc = new SyncOutboxProcessor(db, client, log,
            new FakeTimeProvider(_now), NullLogger<SyncOutboxProcessor>.Instance);
        return (proc, client, log);
    }

    private SyncOutboxItem DocLai(long id)
    {
        using var db = MoDb();
        return db.SyncOutboxItems.Single(o => o.Id == id);
    }

    [Fact]
    public async Task ThanhCong_Thi_Danh_Dau_Success_Va_Ghi_Log()
    {
        var id = SeedItem();
        using var db = MoDb();
        var (proc, _, log) = TaoProcessor(db, SyncSendResult.ThanhCong(200, "/api/supplier/warehouses/merge", "{\"success\":true}"));

        var soLuong = await proc.XuLyCacBanGhiDenHanAsync();

        Assert.Equal(1, soLuong);
        var item = DocLai(id);
        Assert.Equal(SyncOutboxStatus.Success, item.Status);
        Assert.Equal(_now, item.CompletedAtUtc);
        Assert.Null(item.NextRetryAtUtc);
        Assert.Single(log.Entries);
    }

    [Fact]
    public async Task ChuaCauHinh_Thi_AwaitingCredential_Khong_Tang_Attempts_Khong_Ghi_Log()
    {
        var id = SeedItem();
        using var db = MoDb();
        var (proc, _, log) = TaoProcessor(db, SyncSendResult.ChuaCauHinh("Chưa cấu hình."));

        await proc.XuLyCacBanGhiDenHanAsync();

        var item = DocLai(id);
        Assert.Equal(SyncOutboxStatus.AwaitingCredential, item.Status);
        Assert.Equal(0, item.Attempts);
        Assert.Equal(_now.AddMinutes(15), item.NextRetryAtUtc);
        Assert.Empty(log.Entries); // không ghi log cho cơ sở chưa cấu hình.
    }

    [Fact]
    public async Task Loi_422_Thi_NeedsManualReview_Ngay_Va_Luu_Body_Loi()
    {
        var id = SeedItem();
        using var db = MoDb();
        var (proc, _, log) = TaoProcessor(db,
            SyncSendResult.LoiDuLieu("Lỗi dữ liệu (422)", 422, "/api/supplier/warehouses/merge",
                "{\"errors\":{\"0.ma_kho\":[\"đã tồn tại\"]}}"));

        await proc.XuLyCacBanGhiDenHanAsync();

        var item = DocLai(id);
        Assert.Equal(SyncOutboxStatus.NeedsManualReview, item.Status);
        Assert.Null(item.NextRetryAtUtc); // không retry lỗi dữ liệu.
        Assert.Contains("ma_kho", item.LastError);
        Assert.Single(log.Entries);
    }

    [Fact]
    public async Task Loi_Tam_Thoi_Thi_Failed_Va_Hen_Retry_Gian_Cach()
    {
        var id = SeedItem();
        using var db = MoDb();
        var (proc, _, _) = TaoProcessor(db, SyncSendResult.LoiTamThoi("HTTP 503", 503));

        await proc.XuLyCacBanGhiDenHanAsync();

        var item = DocLai(id);
        Assert.Equal(SyncOutboxStatus.Failed, item.Status);
        Assert.Equal(1, item.Attempts);
        Assert.Equal(_now.Add(SyncOutboxProcessor.TinhGianCach(1)), item.NextRetryAtUtc);
    }

    [Fact]
    public async Task Het_So_Lan_Thu_Thi_Chuyen_NeedsManualReview()
    {
        // Đã thử SoLanThuToiDa-1 lần, lần này thất bại nữa là chạm trần.
        var id = SeedItem(status: SyncOutboxStatus.Failed, attempts: SyncOutboxProcessor.SoLanThuToiDa - 1);
        using var db = MoDb();
        var (proc, _, _) = TaoProcessor(db, SyncSendResult.LoiTamThoi("vẫn lỗi", 500));

        await proc.XuLyCacBanGhiDenHanAsync();

        var item = DocLai(id);
        Assert.Equal(SyncOutboxProcessor.SoLanThuToiDa, item.Attempts);
        Assert.Equal(SyncOutboxStatus.NeedsManualReview, item.Status);
        Assert.Null(item.NextRetryAtUtc);
    }

    [Fact]
    public async Task Ban_Ghi_Chua_Den_Han_Retry_Thi_Bo_Qua()
    {
        var id = SeedItem(status: SyncOutboxStatus.Failed, attempts: 1, nextRetry: _now.AddMinutes(30));
        using var db = MoDb();
        var (proc, client, _) = TaoProcessor(db, SyncSendResult.ThanhCong(200, "/x", "{}"));

        var soLuong = await proc.XuLyCacBanGhiDenHanAsync();

        Assert.Equal(0, soLuong);
        Assert.Empty(client.LanGoi); // không đụng tới bản ghi chưa đến hạn.
        Assert.Equal(SyncOutboxStatus.Failed, DocLai(id).Status);
    }

    [Fact]
    public async Task Ban_Ghi_Da_Success_Thi_Khong_Gui_Lai()
    {
        SeedItem(status: SyncOutboxStatus.Success);
        using var db = MoDb();
        var (proc, client, _) = TaoProcessor(db, SyncSendResult.ThanhCong(200, "/x", "{}"));

        var soLuong = await proc.XuLyCacBanGhiDenHanAsync();

        Assert.Equal(0, soLuong);
        Assert.Empty(client.LanGoi);
    }

    [Fact]
    public async Task Nhat_Ky_An_Cccd_Trong_Payload()
    {
        // Log giữ 2 năm nên không được lưu số CCCD ở dạng đọc được.
        using (var db = MoDb())
        {
            db.SyncOutboxItems.Add(new SyncOutboxItem
            {
                TenantId = "coso-a", EntityType = "Staff", EntityKey = "NV001",
                PayloadJson = "[{\"ma_nhan_su\":\"NV001\",\"cccd\":\"079090012345\",\"ho_ten\":\"A\"}]",
                Status = SyncOutboxStatus.Pending
            });
            db.SaveChanges();
        }

        using var db2 = MoDb();
        var (proc, _, log) = TaoProcessor(db2, SyncSendResult.ThanhCong(200, "/api/supplier/users/merge", "{}"));
        await proc.XuLyCacBanGhiDenHanAsync();

        var entry = Assert.Single(log.Entries);
        Assert.DoesNotContain("079090012345", entry.RequestPayload);
        Assert.Contains("\"cccd\":\"***\"", entry.RequestPayload);
        Assert.Contains("NV001", entry.RequestPayload); // các trường khác vẫn còn
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 9)]
    [InlineData(3, 27)]
    [InlineData(4, 60)]  // 81 bị chặn trần 60
    [InlineData(5, 60)]
    public void TinhGianCach_Tang_Dan_Va_Chan_Tran(int soLanThu, int phutKyVong)
    {
        Assert.Equal(TimeSpan.FromMinutes(phutKyVong), SyncOutboxProcessor.TinhGianCach(soLanThu));
    }

    // --- Test doubles ---

    private sealed class FakeTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class FakeSyncClient : IHanoiCheckSyncClient
    {
        public List<string> LanGoi { get; } = new();
        public SyncSendResult KetQua { get; set; } = SyncSendResult.ThanhCong(200, "/x", "{}");

        public Task<SyncSendResult> GuiMergeAsync(string tenantId, string entityType, string payloadJson, CancellationToken ct = default)
        {
            LanGoi.Add($"{tenantId}/{entityType}");
            return Task.FromResult(KetQua);
        }
    }

    private sealed class FakeLogWriter : ISystemLogWriter
    {
        public List<SystemLogEntry> Entries { get; } = new();
        public Task GhiAsync(SystemLogEntry entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
