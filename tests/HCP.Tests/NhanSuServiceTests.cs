using System.Text.Json;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng nghiệp vụ Nhân sự, trọng tâm là xử lý CCCD (dữ liệu cá nhân nhạy cảm):
/// mã hoá khi lưu, giải mã khi đọc, và vẫn gửi plaintext sang HnC (HnC cần trường này).
/// </summary>
public class NhanSuServiceTests
{
    private const string CoSo = "coso-a";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    [Fact]
    public async Task Luu_Thi_Cccd_Duoc_Ma_Hoa_Con_Doc_Thi_Giai_Ma()
    {
        var outbox = new CapturingOutbox();
        using (var db = MoDb())
        {
            var svc = new NhanSuService(db, outbox, new PrefixProtector());
            var kq = await svc.ThemAsync(new Staff
            {
                MaNhanSu = "NV001", HoTen = "Nguyễn Văn A", Cccd = "079090012345", TrangThai = true
            });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        // Cột lưu DB là bản ĐÃ MÃ HOÁ, không phải số CCCD gốc.
        using (var db = MoDb())
        {
            var row = await db.Staff.SingleAsync();
            Assert.Equal("enc:079090012345", row.CccdEncrypted);
            Assert.NotEqual("079090012345", row.CccdEncrypted);
        }

        // Đọc qua service thì Cccd được giải mã trả về plaintext.
        using (var db = MoDb())
        {
            var svc = new NhanSuService(db, outbox, new PrefixProtector());
            var ns = await svc.LayTheoIdAsync((await db.Staff.SingleAsync()).Id);
            Assert.Equal("079090012345", ns!.Cccd);
        }
    }

    [Fact]
    public async Task Payload_Gui_HnC_Chua_Cccd_Plaintext()
    {
        var outbox = new CapturingOutbox();
        using var db = MoDb();
        var svc = new NhanSuService(db, outbox, new PrefixProtector());

        await svc.ThemAsync(new Staff
        {
            MaNhanSu = "NV001", HoTen = "Nguyễn Văn A", Cccd = "079090012345", TrangThai = true
        });

        // HnC cần số CCCD thật để đối chiếu -> payload đẩy đi phải là plaintext, không phải bản mã hoá.
        Assert.Contains("\"cccd\":\"079090012345\"", outbox.PayloadCuoi);
        Assert.DoesNotContain("enc:", outbox.PayloadCuoi);
        Assert.Equal("Staff", outbox.EntityTypeCuoi);
    }

    [Fact]
    public async Task Giay_Kham_Suc_Khoe_Khai_Thieu_Thi_Bao_Loi()
    {
        using var db = MoDb();
        var svc = new NhanSuService(db, new CapturingOutbox(), new PrefixProtector());

        // Có số giấy nhưng thiếu ngày khám/hết hạn -> phải chặn trước khi lưu.
        var kq = await svc.ThemAsync(new Staff
        {
            MaNhanSu = "NV002", HoTen = "Trần B", TrangThai = true, KskSoGiay = "KSK-001"
        });

        Assert.False(kq.ThanhCong);
        Assert.Equal(0, await db.Staff.CountAsync());
    }

    // --- Test doubles ---

    /// <summary>Bộ bảo vệ giả có thể đảo ngược: đủ để phân biệt "đã mã hoá" với plaintext.</summary>
    private sealed class PrefixProtector : ISecretProtector
    {
        public string Protect(string plainText) => "enc:" + plainText;
        public string? TryUnprotect(string protectedText) =>
            protectedText.StartsWith("enc:") ? protectedText[4..] : null;
    }

    private sealed class CapturingOutbox : ISyncOutboxWriter
    {
        public string? EntityTypeCuoi { get; private set; }
        public string PayloadCuoi { get; private set; } = "";

        public Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
        {
            EntityTypeCuoi = entityType;
            PayloadCuoi = JsonSerializer.Serialize(payload, HnCPayloadMapper.Json);
            return Task.CompletedTask;
        }
    }
}
