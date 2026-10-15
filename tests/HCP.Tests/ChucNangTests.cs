using HCP.Domain.Constants;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCP.Tests;

/// <summary>
/// Phân quyền module chức năng theo cơ sở (quản trị nền tảng mở): chuẩn hoá + tự thêm module phụ thuộc,
/// tắt "Kết nối HanoiCheck" thì tắt công tắc đồng bộ + gỡ hàng đợi chưa gửi, cơ sở chưa được mở thì không tự bật được.
/// </summary>
public class ChucNangTests
{
    private const string CoSo = "coso-a";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb(string? chucNangCsv = null)
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo, chucNangCsv);
        return new AppDbContext(accessor, new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);
    }

    private TenantStoreDbContext MoStore() =>
        new(new DbContextOptionsBuilder<TenantStoreDbContext>().UseInMemoryDatabase(_dbName + "-store").Options);

    [Fact]
    public void Ghep_Tu_Them_Phu_Thuoc_Bo_Ma_La_Va_Sap_Theo_Thu_Tu()
    {
        // HanoiCheck cần Bán hàng, Bán hàng cần Kho & sản xuất.
        Assert.Equal("KhoSanXuat,BanHang,HanoiCheck", ChucNang.Ghep(["HanoiCheck", "XYZ"]));
        Assert.Equal("", ChucNang.Ghep([]));
        Assert.Equal(["SuatAn"], ChucNang.Tach(" SuatAn ,LaLung,SuatAn"));
        Assert.True(ChucNang.Co("LoSanXuat,KiemSoatAttp", ChucNang.KiemSoatAttp));
        Assert.False(ChucNang.Co("", ChucNang.LoSanXuat));
    }

    [Fact]
    public void Co_So_Moi_Mac_Dinh_Chi_Danh_Muc_Co_Ban()
    {
        var t = new Tenant();
        Assert.Equal("", t.ChucNangCsv);
        Assert.All(ChucNang.TatCa, ma => Assert.False(t.CoChucNang(ma)));
    }

    [Fact]
    public async Task Tat_Ket_Noi_HanoiCheck_Thi_Tat_Cong_Tac_Va_Go_Hang_Doi_Chua_Gui()
    {
        using (var store = MoStore())
        {
            store.TenantInfo.Add(new Tenant
            {
                Id = CoSo, Identifier = CoSo, Name = "Cơ sở A", EmailLienHe = "a@x.vn",
                TrangThai = TenantStatus.Active, ChucNangCsv = ChucNang.Ghep(ChucNang.TatCa)
            });
            store.SaveChanges();
        }
        using (var db = MoDb())
        {
            db.TenantHnCCredentials.Add(new TenantHnCCredential { TenantId = CoSo, BatDongBo = true });
            db.SyncOutboxItems.AddRange(
                new SyncOutboxItem { TenantId = CoSo, EntityType = "Product", EntityKey = "SP1", PayloadJson = "{}", Status = SyncOutboxStatus.Pending },
                new SyncOutboxItem { TenantId = CoSo, EntityType = "Product", EntityKey = "SP2", PayloadJson = "{}", Status = SyncOutboxStatus.Success });
            db.SaveChanges();
        }

        using (var store = MoStore())
        using (var db = MoDb())
        {
            var svc = new CoSoService(store, db, null!, NullLogger<CoSoService>.Instance);
            var kq = await svc.CapNhatChucNangAsync(CoSo, [ChucNang.SuatAn, ChucNang.KiemSoatAttp], "admin");
            Assert.True(kq.ThanhCong, kq.ThongBao);
            Assert.Contains("tắt đồng bộ HanoiCheck", kq.ThongBao);
        }

        using (var store = MoStore())
            Assert.Equal("SuatAn,KiemSoatAttp", store.TenantInfo.Single().ChucNangCsv);
        using (var db = MoDb())
        {
            Assert.False(db.TenantHnCCredentials.Single().BatDongBo);
            Assert.Equal("SP2", db.SyncOutboxItems.Single().EntityKey);   // giữ lịch sử đã gửi
        }
    }

    [Fact]
    public async Task Co_So_Chua_Mo_HanoiCheck_Khong_Tu_Bat_Dong_Bo_Duoc()
    {
        using (var db = MoDb(""))
        {
            db.TenantHnCCredentials.Add(new TenantHnCCredential { TenantId = CoSo, BatDongBo = false });
            db.SaveChanges();
        }

        using (var db = MoDb(""))
        {
            var kq = await new DongBoHnCTongService(db, new SyncOutboxWriter(db, new TestMultiTenantContextAccessor()))
                .BatTatAsync(true);
            Assert.False(kq.ThanhCong);
            Assert.Contains("Kết nối HanoiCheck", kq.ThongBao);
        }

        using (var db = MoDb(ChucNang.Ghep([ChucNang.HanoiCheck])))
        {
            var kq = await new DongBoHnCTongService(db, new SyncOutboxWriter(db, new TestMultiTenantContextAccessor()))
                .BatTatAsync(true);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }
    }
}
