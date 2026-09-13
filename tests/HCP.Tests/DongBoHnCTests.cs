using HCP.Domain.Entities.Business;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng công tắc đồng bộ HanoiCheck: công tắc tổng theo cơ sở, tick từng bản ghi, tự bật bản ghi phụ thuộc,
/// tắt thì gỡ bản ghi chưa gửi, đẩy toàn bộ danh mục, và ràng buộc riêng HanoiCheck chỉ áp khi thật sự gửi.
/// </summary>
public class DongBoHnCTests
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

    private static SyncOutboxWriter Writer(AppDbContext db)
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);
        return new SyncOutboxWriter(db, accessor);
    }

    private void BatTong(bool bat = true)
    {
        using var db = MoDb();
        db.TenantHnCCredentials.Add(new TenantHnCCredential { TenantId = CoSo, BatDongBo = bat });
        db.SaveChanges();
    }

    private List<SyncOutboxItem> HangDoi()
    {
        using var db = MoDb();
        return db.SyncOutboxItems.OrderBy(o => o.Id).ToList();
    }

    private void SeedLo()
    {
        using var db = MoDb();
        db.Products.Add(new Product { MaSanPham = "SP01", TenSanPham = "Bánh", MaLoaiSp = "x", LoaiSanPham = LoaiSanPham.ThanhPham });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
        db.SaveChanges();
    }

    private static Batch LoKhongAnh(bool dongBo = true) => new()
    {
        MaSanPham = "SP01", TenLo = "Lô", NgayNhap = new DateOnly(2026, 9, 13), DongBoHnC = dongBo,
        DanhSachKho = { new BatchWarehouse { MaKho = "KHO01" } }
    };

    [Fact]
    public async Task Tat_Tong_Thi_Khong_Xep_Hang_Va_Khong_Bat_Rang_Buoc_Rieng_HanoiCheck()
    {
        SeedLo();   // chưa có cấu hình kết nối = công tắc tổng tắt

        using (var db = MoDb())
        {
            var coSo = await new CoSoSanXuatService(db, Writer(db), new MaTuSinhService(db)).ThemAsync(new Facility { TenCoSo = "Xưởng" });
            Assert.True(coSo.ThanhCong, coSo.ThongBao);
            var lo = await new LoSanXuatService(db, Writer(db), new MaTuSinhService(db)).ThemAsync(LoKhongAnh());
            Assert.True(lo.ThanhCong, lo.ThongBao);          // không bắt album ảnh khi không gửi HanoiCheck
        }
        Assert.Empty(HangDoi());

        // Bật lên: cùng lô đó bị chặn vì HanoiCheck bắt buộc ảnh...
        BatTong();
        using (var db = MoDb())
        {
            var svc = new LoSanXuatService(db, Writer(db), new MaTuSinhService(db));
            Assert.Contains("ảnh chung", (await svc.ThemAsync(LoKhongAnh())).ThongBao);
            // ...trừ khi lô được bỏ tick đồng bộ.
            Assert.True((await svc.ThemAsync(LoKhongAnh(dongBo: false))).ThanhCong);
        }
        Assert.Empty(HangDoi());
    }

    [Fact]
    public async Task Tick_Tung_Ban_Ghi_Bo_Tick_Thi_Go_Lan_Gui_Dang_Cho()
    {
        BatTong();
        var a = new Facility { TenCoSo = "Xưởng A" };
        using (var db = MoDb())
        {
            var svc = new CoSoSanXuatService(db, Writer(db), new MaTuSinhService(db));
            Assert.True((await svc.ThemAsync(a)).ThanhCong);
            Assert.True((await svc.ThemAsync(new Facility { TenCoSo = "Xưởng nội bộ", DongBoHnC = false })).ThanhCong);
        }
        Assert.Equal(a.MaCoSo, Assert.Single(HangDoi()).EntityKey);

        // Bỏ tick khi sửa -> lần gửi đang chờ bị gỡ, cờ được lưu.
        using (var db = MoDb())
        {
            var kq = await new CoSoSanXuatService(db, Writer(db), new MaTuSinhService(db))
                .CapNhatAsync(new Facility { Id = a.Id, TenCoSo = "Xưởng A2", DongBoHnC = false });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }
        Assert.Empty(HangDoi());
        using (var db = MoDb()) Assert.False((await db.Facilities.SingleAsync(f => f.Id == a.Id)).DongBoHnC);
    }

    [Fact]
    public async Task Gui_Thanh_Pham_Tu_Bat_Quy_Trinh_Va_Khau_Phu_Thuoc()
    {
        BatTong();
        using (var db = MoDb())
        {
            db.ProductionSteps.Add(new ProductionStep { MaKhau = "KHAU-0001", TenKhau = "Nướng", DongBoHnC = false });
            var qt = new ProductionProcess { MaQuyTrinh = "QT01", TenQuyTrinh = "Làm bánh", DongBoHnC = false };
            qt.DanhSachKhau.Add(new ProcessStepLine { MaKhau = "KHAU-0001", ThuTu = 1 });
            db.ProductionProcesses.Add(qt);
            db.Products.Add(new Product { MaSanPham = "SP01", TenSanPham = "Bánh", MaLoaiSp = "x",
                                          LoaiSanPham = LoaiSanPham.ThanhPham, MaQuyTrinh = "QT01" });
            db.Products.Add(new Product { MaSanPham = "BOT", TenSanPham = "Bột", LoaiSanPham = LoaiSanPham.NguyenLieu });
            await db.SaveChangesAsync();
        }

        using (var db = MoDb())
        {
            var ghiChu = await Writer(db).GuiAsync(await db.Products.SingleAsync(p => p.MaSanPham == "SP01"));
            Assert.Contains("quy trình QT01", ghiChu);
            Assert.Contains("khâu KHAU-0001", ghiChu);

            // Nguyên liệu không bao giờ gửi HanoiCheck.
            Assert.Null(await Writer(db).GuiAsync(await db.Products.SingleAsync(p => p.MaSanPham == "BOT")));
        }

        // Gửi đúng thứ tự phụ thuộc: khâu → quy trình → thành phẩm.
        Assert.Equal(new[] { "ProductionStep", "ProductionProcess", "Product" }, HangDoi().Select(o => o.EntityType));
        using var db2 = MoDb();
        Assert.True((await db2.ProductionSteps.SingleAsync()).DongBoHnC);
        Assert.True((await db2.ProductionProcesses.SingleAsync()).DongBoHnC);
    }

    [Fact]
    public async Task Tat_Tong_Go_Ban_Ghi_Chua_Gui_Giu_Ban_Da_Gui_Bat_Can_Cau_Hinh()
    {
        using (var db = MoDb())
            Assert.Contains("cấu hình", (await new DongBoHnCTongService(db, Writer(db)).BatTatAsync(true)).ThongBao);

        BatTong();
        using (var db = MoDb())
        {
            foreach (var (khoa, tt) in new[] { ("A", SyncOutboxStatus.Pending), ("B", SyncOutboxStatus.Success), ("C", SyncOutboxStatus.Failed) })
            {
                db.SyncOutboxItems.Add(new SyncOutboxItem
                {
                    TenantId = CoSo, EntityType = "Warehouse", EntityKey = khoa, PayloadJson = "[]",
                    Status = tt, CreatedAtUtc = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        using (var db = MoDb())
        {
            var kq = await new DongBoHnCTongService(db, Writer(db)).BatTatAsync(false);
            Assert.True(kq.ThanhCong, kq.ThongBao);
            Assert.Contains("gỡ 2", kq.ThongBao);
        }
        Assert.Equal("B", Assert.Single(HangDoi()).EntityKey);

        using (var db = MoDb())
        {
            Assert.False((await db.TenantHnCCredentials.SingleAsync()).BatDongBo);
            await Writer(db).ThemAsync("Warehouse", "D", new object[0]);        // đang tắt -> không ghi
        }
        Assert.Single(HangDoi());
    }

    [Fact]
    public async Task Day_Toan_Bo_Chi_Gui_Ban_Ghi_Duoc_Tick_Theo_Thu_Tu()
    {
        BatTong();
        using (var db = MoDb())
        {
            db.Products.Add(new Product { MaSanPham = "SP01", TenSanPham = "Bánh", MaLoaiSp = "x", LoaiSanPham = LoaiSanPham.ThanhPham });
            db.Products.Add(new Product { MaSanPham = "BOT", TenSanPham = "Bột", LoaiSanPham = LoaiSanPham.NguyenLieu });
            db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho 1", DiaChi = "HN" });
            db.Warehouses.Add(new Warehouse { MaKho = "KHO02", TenKho = "Kho nội bộ", DiaChi = "HN", DongBoHnC = false });
            db.Facilities.Add(new Facility { MaCoSo = "CS-0001", TenCoSo = "Xưởng" });
            await db.SaveChangesAsync();
        }

        using (var db = MoDb())
        {
            var kq = await new DongBoHnCTongService(db, Writer(db)).DayToanBoAsync();
            Assert.True(kq.ThanhCong, kq.ThongBao);
            Assert.Contains("3 bản ghi", kq.ThongBao);
        }
        Assert.Equal(new[] { ("Warehouse", "KHO01"), ("Facility", "CS-0001"), ("Product", "SP01") },
                     HangDoi().Select(o => (o.EntityType, o.EntityKey)));

        using (var db = MoDb())
        {
            await new DongBoHnCTongService(db, Writer(db)).BatTatAsync(false);
            Assert.False((await new DongBoHnCTongService(db, Writer(db)).DayToanBoAsync()).ThanhCong);
        }
    }

    [Fact]
    public async Task Thanh_Pham_Dong_Bo_Bat_Buoc_Quy_Trinh_Chi_Khi_Gui_HanoiCheck()
    {
        Product Banh(bool dongBo) => new()
        {
            MaSanPham = dongBo ? "SP-DB" : "SP-NB", TenSanPham = "Bánh", MaLoaiSp = "6798",
            LoaiSanPham = LoaiSanPham.ThanhPham, DongBoHnC = dongBo
        };

        // Công tắc tổng tắt: không cần quy trình.
        using (var db = MoDb())
            Assert.True((await new ThucPhamService(db, Writer(db)).ThemAsync(Banh(true))).ThanhCong);

        BatTong();
        using (var db = MoDb())
        {
            var svc = new ThucPhamService(db, Writer(db));
            var sp = Banh(true);
            sp.MaSanPham = "SP-DB2";
            Assert.Contains("quy trình sản xuất", (await svc.ThemAsync(sp)).ThongBao);
            Assert.True((await svc.ThemAsync(Banh(false))).ThanhCong);     // bỏ tick đồng bộ thì không bắt
        }
    }
}
