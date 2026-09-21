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

    [Fact]
    public async Task Xoa_Duoc_Nhan_Su_Khong_Bi_Tham_Chieu_O_Dau()
    {
        int id;
        using (var db = MoDb())
        {
            var svc = new NhanSuService(db, new CapturingOutbox(), new PrefixProtector());
            var ns = new Staff { MaNhanSu = "NV-TRONG", HoTen = "Không liên quan", TrangThai = true };
            Assert.True((await svc.ThemAsync(ns)).ThanhCong);
            id = ns.Id;
        }
        using var db2 = MoDb();
        var kq = await new NhanSuService(db2, new CapturingOutbox(), new PrefixProtector()).XoaAsync(id);
        Assert.True(kq.ThanhCong, kq.ThongBao);
        Assert.Equal(0, await db2.Staff.CountAsync());
    }

    /// <summary>
    /// Mã nhân sự không phải khoá ngoại (CSDL không tự chặn) nên phải tự kiểm tra ở service - đơn hàng bán/
    /// từ trường đã gán người giao, đã tham gia lệnh, hoặc đang là người thực hiện một khâu/bước (lệnh sản
    /// xuất, lô sản xuất, món ăn) đều phải chặn xoá để không để lại mã treo trong dữ liệu nghiệp vụ.
    /// </summary>
    [Theory]
    [InlineData("NS-DHB")]
    [InlineData("NS-DHN")]
    [InlineData("NS-TG")]
    [InlineData("NS-KHAU")]
    [InlineData("NS-LO")]
    [InlineData("NS-MON")]
    public async Task Khong_Xoa_Duoc_Nhan_Su_Dang_Duoc_Tham_Chieu(string ma)
    {
        int id;
        using (var db = MoDb())
        {
            var svc = new NhanSuService(db, new CapturingOutbox(), new PrefixProtector());
            var ns = new Staff { MaNhanSu = ma, HoTen = "Người " + ma, TrangThai = true };
            Assert.True((await svc.ThemAsync(ns)).ThanhCong);
            id = ns.Id;
        }

        using (var db = MoDb())
        {
            db.DonHangBans.Add(new DonHangBan
            {
                MaDonHang = "DH-NS10", MaKhachHang = "KH", MaKho = "KHO", NgayDat = new DateOnly(2026, 9, 1),
                MaNguoiGiao = "NS-DHB"
            });
            db.DonHangNhans.Add(new DonHangNhan { MaDonHang = "HNC-NS10", MaNguoiGiao = "NS-DHN" });
            db.LenhSanXuats.Add(new LenhSanXuat
            {
                MaLenh = "LSX-NS10", MaKho = "KHO", NgaySanXuat = new DateOnly(2026, 9, 1),
                ThamGia = { new LenhSanXuatThamGia { MaNhanSu = "NS-TG", HoTen = "x", ThoiGianUtc = DateTime.UtcNow } },
                SanPham =
                {
                    new LenhSanXuatSanPham
                    {
                        MaThanhPham = "SP1", SoLuong = 1, MaLoThanhPham = "LO1", MaQuyTrinh = "QT1",
                        Khau = { new LenhSanXuatKhau { MaKhau = "K1", ThuTu = 1, MaCoSo = "CS1", NguoiThucHienCsv = "NS-KHAU" } }
                    }
                }
            });
            db.Batches.Add(new Batch
            {
                MaSanPham = "SP1", MaLo = "LO-NS10", TenLo = "Lô", NgayNhap = new DateOnly(2026, 9, 1),
                DanhSachKhau = { new BatchStep { MaKhau = "K1", ThuTu = 1, NguoiThucHienCsv = "NS-LO" } }
            });
            db.Dishes.Add(new Dish
            {
                MaMonAn = "MON-NS10", TenMonAn = "Món",
                DanhSachKhau = { new DishStep { MaKhau = "K1", ThuTu = 1, NguoiThucHienCsv = "NS-MON" } }
            });
            await db.SaveChangesAsync();
        }

        using var dbXoa = MoDb();
        var kq = await new NhanSuService(dbXoa, new CapturingOutbox(), new PrefixProtector()).XoaAsync(id);
        Assert.False(kq.ThanhCong);
        Assert.Contains("Không xoá được", kq.ThongBao);
        Assert.Equal(1, await dbXoa.Staff.CountAsync(s => s.MaNhanSu == ma));
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

        public Task<bool> DangBatAsync(CancellationToken ct = default) => Task.FromResult(true);

        public async Task<string?> GuiAsync(object banGhi, CancellationToken ct = default)
        {
            if (banGhi is Staff s) await ThemAsync("Staff", s.MaNhanSu, HnCPayloadMapper.NhanSu(s), ct);
            return null;
        }
    }
}
