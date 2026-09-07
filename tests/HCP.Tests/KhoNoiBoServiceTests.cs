using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.Kho;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng kho nội bộ: nhập nguyên liệu cộng tồn, tồn tính theo lô, ràng buộc, và cách ly
/// theo cơ sở (KhoGiaoDich có lọc tenant).
/// </summary>
public class KhoNoiBoServiceTests
{
    private const string CoSoA = "coso-a";
    private const string CoSoB = "coso-b";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb(string tenantId)
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private void Seed(string tenant, LoaiSanPham loai = LoaiSanPham.NguyenLieu)
    {
        using var db = MoDb(tenant);
        db.Products.Add(new Product
        {
            MaSanPham = "BOT_MI", TenSanPham = "Bột mì", MaLoaiSp = "", LoaiSanPham = loai, DonViTinh = "kg"
        });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
        db.SaveChanges();
    }

    private static NhapKhoRequest Req(string lo, decimal sl) => new()
    {
        MaSanPham = "BOT_MI", MaKho = "KHO01", MaLo = lo, SoLuong = sl
    };

    [Fact]
    public async Task Nhap_Nguyen_Lieu_Cong_Ton_Theo_Lo()
    {
        Seed(CoSoA);
        using (var db = MoDb(CoSoA))
        {
            var svc = new KhoNoiBoService(db);
            Assert.True((await svc.NhapNguyenLieuAsync(Req("LO1", 100))).ThanhCong);
            Assert.True((await svc.NhapNguyenLieuAsync(Req("LO1", 50))).ThanhCong);  // cùng lô -> cộng dồn
            Assert.True((await svc.NhapNguyenLieuAsync(Req("LO2", 30))).ThanhCong);  // lô khác -> tách dòng
        }

        using (var db = MoDb(CoSoA))
        {
            var ton = await new KhoNoiBoService(db).LayTonAsync();
            Assert.Equal(2, ton.Count);
            Assert.Equal(150, ton.Single(t => t.MaLo == "LO1").SoLuongTon);
            Assert.Equal(30, ton.Single(t => t.MaLo == "LO2").SoLuongTon);
        }
    }

    [Fact]
    public async Task Khong_Nhap_Neu_Khong_Phai_Nguyen_Lieu()
    {
        Seed(CoSoA, LoaiSanPham.ThanhPham);
        using var db = MoDb(CoSoA);
        var kq = await new KhoNoiBoService(db).NhapNguyenLieuAsync(Req("LO1", 10));
        Assert.False(kq.ThanhCong);
        Assert.Equal(0, await db.KhoGiaoDichs.CountAsync());
    }

    [Fact]
    public async Task So_Luong_Khong_Duong_Thi_Loi()
    {
        Seed(CoSoA);
        using var db = MoDb(CoSoA);
        Assert.False((await new KhoNoiBoService(db).NhapNguyenLieuAsync(Req("LO1", 0))).ThanhCong);
        Assert.False((await new KhoNoiBoService(db).NhapNguyenLieuAsync(Req("LO1", -5))).ThanhCong);
    }

    private static DieuChinhTonRequest DcReq(string lo, decimal thucTe, string? lyDo = null) => new()
    {
        MaSanPham = "BOT_MI", MaKho = "KHO01", MaLo = lo, SoLuongThucTe = thucTe, LyDo = lyDo
    };

    [Fact]
    public async Task Dieu_Chinh_Giam_Ton_Ve_So_Thuc_Te()
    {
        Seed(CoSoA);
        using (var db = MoDb(CoSoA)) await new KhoNoiBoService(db).NhapNguyenLieuAsync(Req("LO1", 10));

        using (var db = MoDb(CoSoA))
        {
            var kq = await new KhoNoiBoService(db).DieuChinhTonAsync(DcReq("LO1", 8, "hao hụt"));
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        using (var db = MoDb(CoSoA))
        {
            var ton = await new KhoNoiBoService(db).LayTonAsync();
            Assert.Equal(8, ton.Single(t => t.MaLo == "LO1").SoLuongTon);   // tồn đúng số đếm
            // Có đúng 1 dòng điều chỉnh (−2).
            var dc = await db.KhoGiaoDichs.Where(g => g.Loai == LoaiGiaoDichKho.DieuChinh).ToListAsync();
            Assert.Single(dc);
            Assert.Equal(-2, dc[0].SoLuong);
            Assert.Equal("hao hụt", dc[0].GhiChu);
        }
    }

    [Fact]
    public async Task Dieu_Chinh_Tang_Ton()
    {
        Seed(CoSoA);
        using (var db = MoDb(CoSoA)) await new KhoNoiBoService(db).NhapNguyenLieuAsync(Req("LO1", 5));
        using (var db = MoDb(CoSoA))
            Assert.True((await new KhoNoiBoService(db).DieuChinhTonAsync(DcReq("LO1", 7))).ThanhCong);
        using (var db = MoDb(CoSoA))
            Assert.Equal(7, (await new KhoNoiBoService(db).LayTonAsync()).Single().SoLuongTon);
    }

    [Fact]
    public async Task Dieu_Chinh_Khong_Chenh_Lech_Thi_Khong_Ghi()
    {
        Seed(CoSoA);
        using (var db = MoDb(CoSoA)) await new KhoNoiBoService(db).NhapNguyenLieuAsync(Req("LO1", 5));
        using (var db = MoDb(CoSoA))
        {
            var kq = await new KhoNoiBoService(db).DieuChinhTonAsync(DcReq("LO1", 5));
            Assert.True(kq.ThanhCong);
            Assert.Contains("không cần điều chỉnh", kq.ThongBao);
        }
        using (var db = MoDb(CoSoA))
            Assert.False(await db.KhoGiaoDichs.AnyAsync(g => g.Loai == LoaiGiaoDichKho.DieuChinh));
    }

    [Fact]
    public async Task Dieu_Chinh_So_Am_Thi_Loi()
    {
        Seed(CoSoA);
        using var db = MoDb(CoSoA);
        Assert.False((await new KhoNoiBoService(db).DieuChinhTonAsync(DcReq("LO1", -1))).ThanhCong);
    }

    [Fact]
    public async Task Ton_Kho_Bi_Loc_Theo_Co_So()
    {
        Seed(CoSoA);
        Seed(CoSoB);
        using (var db = MoDb(CoSoA)) await new KhoNoiBoService(db).NhapNguyenLieuAsync(Req("LO1", 100));
        using (var db = MoDb(CoSoB)) await new KhoNoiBoService(db).NhapNguyenLieuAsync(Req("LO1", 999));

        using var dbA = MoDb(CoSoA);
        var ton = await new KhoNoiBoService(dbA).LayTonAsync();
        Assert.Single(ton);
        Assert.Equal(100, ton[0].SoLuongTon); // không thấy 999 của cơ sở B
    }
}
