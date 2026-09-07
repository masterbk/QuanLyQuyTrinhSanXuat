using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.Kho;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>Kiểm chứng định mức: lưu công thức, thay thế toàn bộ, và ràng buộc nguyên liệu hợp lệ.</summary>
public class DinhMucServiceTests
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

    private int SeedThanhPham()
    {
        using var db = MoDb();
        var tp = new Product { MaSanPham = "BANH_MI", TenSanPham = "Bánh mì", MaLoaiSp = "x",
                               LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái" };
        db.Products.Add(tp);
        db.Products.Add(new Product { MaSanPham = "BOT_MI", TenSanPham = "Bột mì", LoaiSanPham = LoaiSanPham.NguyenLieu });
        db.Products.Add(new Product { MaSanPham = "DUONG", TenSanPham = "Đường", LoaiSanPham = LoaiSanPham.NguyenLieu });
        db.SaveChanges();
        return tp.Id;
    }

    [Fact]
    public async Task Luu_Va_Thay_The_Dinh_Muc()
    {
        var id = SeedThanhPham();
        using (var db = MoDb())
        {
            var kq = await new DinhMucService(db).LuuAsync(id, new[]
            {
                new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0.1m },
                new DinhMucNguyenLieu { MaNguyenLieu = "DUONG", SoLuong = 0.02m }
            });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        // Lưu lại chỉ 1 nguyên liệu -> thay thế toàn bộ (còn 1 dòng).
        using (var db = MoDb())
        {
            await new DinhMucService(db).LuuAsync(id, new[]
            {
                new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0.15m }
            });
        }

        using (var db = MoDb())
        {
            var dm = await new DinhMucService(db).LayTheoThanhPhamAsync(id);
            Assert.Single(dm);
            Assert.Equal("BOT_MI", dm[0].MaNguyenLieu);
            Assert.Equal(0.15m, dm[0].SoLuong);
        }
    }

    [Fact]
    public async Task Chan_Nguyen_Lieu_Khong_Hop_Le()
    {
        var id = SeedThanhPham();
        using var db = MoDb();
        // "BANH_MI" là thành phẩm, không phải nguyên liệu -> phải bị chặn.
        var kq = await new DinhMucService(db).LuuAsync(id, new[]
        {
            new DinhMucNguyenLieu { MaNguyenLieu = "BANH_MI", SoLuong = 1 }
        });
        Assert.False(kq.ThanhCong);
        Assert.Contains("BANH_MI", kq.ThongBao);
    }

    [Fact]
    public async Task Chan_Dinh_Luong_Khong_Duong()
    {
        var id = SeedThanhPham();
        using var db = MoDb();
        var kq = await new DinhMucService(db).LuuAsync(id, new[]
        {
            new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0 }
        });
        Assert.False(kq.ThanhCong);
    }

    [Fact]
    public async Task Luu_Va_Doc_Lai_Hao_Hut_Phan_Tram()
    {
        var id = SeedThanhPham();
        using (var db = MoDb())
        {
            var kq = await new DinhMucService(db).LuuAsync(id, new[]
            {
                new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0.1m, HaoHutPhanTram = 10m }
            });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }
        using (var db = MoDb())
        {
            var dm = await new DinhMucService(db).LayTheoThanhPhamAsync(id);
            Assert.Equal(10m, dm.Single().HaoHutPhanTram);
        }
    }

    [Fact]
    public async Task Chan_Hao_Hut_Ngoai_Khoang()
    {
        var id = SeedThanhPham();
        using var db = MoDb();
        var svc = new DinhMucService(db);
        Assert.False((await svc.LuuAsync(id, new[]
            { new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0.1m, HaoHutPhanTram = 150m } })).ThanhCong);
        Assert.False((await svc.LuuAsync(id, new[]
            { new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0.1m, HaoHutPhanTram = -5m } })).ThanhCong);
    }
}
