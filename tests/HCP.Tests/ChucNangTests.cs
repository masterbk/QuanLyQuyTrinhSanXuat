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
/// Phân quyền chức năng (từng menu) theo cơ sở: chuẩn hoá + tự thêm chức năng phụ thuộc, quy đổi mã module cũ,
/// tắt "Cài đặt kết nối" thì tắt công tắc đồng bộ + gỡ hàng đợi chưa gửi, cơ sở chưa được mở thì không tự bật được.
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
        // Đơn hàng bán cần Khách hàng + Tồn kho; Tồn kho lại cần Kho + Thực phẩm.
        Assert.Equal("Kho,ThucPham,TonKho,KhachHang,DonHangBan", ChucNang.Ghep([ChucNang.DonHangBan, "XYZ"]));
        Assert.Equal("", ChucNang.Ghep([]));
        Assert.Equal([ChucNang.MonAn], ChucNang.Tach(" MonAn ,LaLung,MonAn"));
        Assert.True(ChucNang.Co("LoSanXuat,BieuMau", ChucNang.BieuMau));
        Assert.False(ChucNang.Co("", ChucNang.LoSanXuat));

        // Thực đơn kéo theo Món ăn, Món ăn kéo theo Thực phẩm + Quy trình, Quy trình kéo theo Khâu.
        Assert.Equal("KhauSanXuat,QuyTrinh,ThucPham,MonAn,ThucDon", ChucNang.Ghep([ChucNang.ThucDon]));
    }

    [Fact]
    public void Quy_Doi_Ma_Module_Cu_Sang_Ma_Theo_Menu()
    {
        // Cơ sở cũ mở đủ 6 module -> mở hết menu.
        var day = ChucNang.TuMaCu("LoSanXuat,SuatAn,KhoSanXuat,BanHang,HanoiCheck,KiemSoatAttp");
        Assert.All(ChucNang.TatCa, ma => Assert.True(ChucNang.Co(day, ma), ma));

        // Cơ sở cũ chỉ có Kiểm soát ATTP: vẫn được nhóm danh mục (trước đây luôn hiện), không có Bán hàng.
        var attp = ChucNang.TuMaCu("KiemSoatAttp");
        Assert.True(ChucNang.Co(attp, ChucNang.NhapPhieu));
        Assert.True(ChucNang.Co(attp, ChucNang.ThucPham));
        Assert.False(ChucNang.Co(attp, ChucNang.DonHangBan));
        Assert.False(ChucNang.Co(attp, ChucNang.LenhSanXuat));
    }

    [Fact]
    public void Tra_Kem_Ma_Module_Cu_Cho_App_Da_Cai()
    {
        // App phát trước 18/10/2026 lọc tab theo mã cũ - máy chủ phải trả kèm, nếu không app cũ mất sạch tab.
        var csv = ChucNang.Ghep([ChucNang.LenhSanXuat, ChucNang.DonHangBan, ChucNang.ThucDon, ChucNang.NhapPhieu]);
        var guiApp = ChucNang.KemMaCu(csv);

        Assert.Contains(ChucNang.MaCu.KhoSanXuat, guiApp);
        Assert.Contains(ChucNang.MaCu.BanHang, guiApp);
        Assert.Contains(ChucNang.MaCu.SuatAn, guiApp);
        Assert.Contains(ChucNang.MaCu.KiemSoatAttp, guiApp);
        Assert.Contains(ChucNang.LenhSanXuat, guiApp);          // mã mới vẫn còn cho app mới
        Assert.DoesNotContain(ChucNang.MaCu.LoSanXuat, guiApp);  // không bật Lô thì không suy ra mã cũ

        // Cơ sở chỉ có danh mục thuần thì app cũ không thấy tab nào.
        var chiDanhMuc = ChucNang.KemMaCu(ChucNang.Ghep([ChucNang.ThucPham]));
        Assert.DoesNotContain(ChucNang.MaCu.KhoSanXuat, chiDanhMuc);
        Assert.DoesNotContain(ChucNang.MaCu.BanHang, chiDanhMuc);
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
            var kq = await svc.CapNhatChucNangAsync(CoSo, [ChucNang.MonAn, ChucNang.BieuMau], "admin");
            Assert.True(kq.ThanhCong, kq.ThongBao);
            Assert.Contains("tắt đồng bộ HanoiCheck", kq.ThongBao);
        }

        using (var store = MoStore())
            Assert.Equal("KhauSanXuat,QuyTrinh,ThucPham,MonAn,BieuMau", store.TenantInfo.Single().ChucNangCsv);
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

        using (var db = MoDb(ChucNang.Ghep([ChucNang.KetNoiHnC])))
        {
            var kq = await new DongBoHnCTongService(db, new SyncOutboxWriter(db, new TestMultiTenantContextAccessor()))
                .BatTatAsync(true);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }
    }
}
