using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.Kho;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng phiếu xuất bán: trừ tồn thành phẩm đúng FEFO (lô hết hạn trước xuất trước),
/// chặn khi thiếu tồn, gộp dòng trùng mã và không cho thực hiện hai lần.
/// </summary>
public class PhieuXuatBanServiceTests
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

    /// <summary>Thành phẩm BANH_MI (cái), kho KHO01, khách hàng KH01.</summary>
    private void SeedDanhMuc()
    {
        using var db = MoDb();
        db.Products.Add(new Product { MaSanPham = "BANH_MI", TenSanPham = "Bánh mì", MaLoaiSp = "x",
                                      LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái" });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
        db.KhachHangs.Add(new KhachHang { MaKhachHang = "KH01", TenKhachHang = "Cửa hàng A" });
        db.SaveChanges();
    }

    private void NhapThanhPham(string lo, decimal sl, DateOnly hsd)
    {
        using var db = MoDb();
        db.KhoGiaoDichs.Add(new KhoGiaoDich
        {
            MaSanPham = "BANH_MI", MaKho = "KHO01", MaLo = lo, SoLuong = sl, HanSuDung = hsd,
            Loai = LoaiGiaoDichKho.NhapThanhPham, ThoiGianUtc = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private decimal TonLo(string lo)
    {
        using var db = MoDb();
        return db.KhoGiaoDichs.Where(g => g.MaSanPham == "BANH_MI" && g.MaLo == lo)
            .Sum(g => (decimal?)g.SoLuong) ?? 0m;
    }

    private static PhieuXuatBan Phieu(params (string ma, decimal sl)[] dong) => new()
    {
        MaPhieu = "XB-001", MaKhachHang = "KH01", MaKho = "KHO01",
        NgayXuat = new DateOnly(2026, 9, 6),
        ChiTiet = dong.Select(d => new PhieuXuatBanChiTiet { MaThanhPham = d.ma, SoLuong = d.sl }).ToList()
    };

    [Fact]
    public async Task Thuc_Hien_Tru_Thanh_Pham_FEFO()
    {
        SeedDanhMuc();
        NhapThanhPham("LO_A", 6m, new DateOnly(2026, 1, 1));  // hết hạn sớm -> xuất trước
        NhapThanhPham("LO_B", 6m, new DateOnly(2026, 6, 1));

        int id;
        using (var db = MoDb())
        {
            var svc = new PhieuXuatBanService(db);
            var kq = await svc.TaoAsync(Phieu(("BANH_MI", 10m)));
            Assert.True(kq.ThanhCong, kq.ThongBao);
            id = (await db.PhieuXuatBans.SingleAsync()).Id;
        }

        using (var db = MoDb())
        {
            var kq = await new PhieuXuatBanService(db).ThucHienAsync(id);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        // FEFO: LO_A (6, hết hạn sớm) hết sạch, LO_B trừ 4 còn 2.
        Assert.Equal(0m, TonLo("LO_A"));
        Assert.Equal(2m, TonLo("LO_B"));

        using (var db = MoDb())
            Assert.Equal(TrangThaiXuatBan.HoanThanh, (await db.PhieuXuatBans.SingleAsync()).TrangThai);
    }

    [Fact]
    public async Task Gop_Dong_Trung_Ma_Khi_Tao()
    {
        SeedDanhMuc();
        NhapThanhPham("LO_A", 10m, new DateOnly(2026, 1, 1));

        using var db = MoDb();
        var kq = await new PhieuXuatBanService(db).TaoAsync(Phieu(("BANH_MI", 3m), ("BANH_MI", 2m)));
        Assert.True(kq.ThanhCong, kq.ThongBao);

        var phieu = await db.PhieuXuatBans.Include(p => p.ChiTiet).SingleAsync();
        Assert.Single(phieu.ChiTiet);
        Assert.Equal(5m, phieu.ChiTiet[0].SoLuong);
    }

    [Fact]
    public async Task Thieu_Ton_Thi_Khong_Tru_Gi()
    {
        SeedDanhMuc();
        NhapThanhPham("LO_A", 4m, new DateOnly(2026, 1, 1)); // chỉ 4, cần 10

        int id;
        using (var db = MoDb())
        {
            await new PhieuXuatBanService(db).TaoAsync(Phieu(("BANH_MI", 10m)));
            id = (await db.PhieuXuatBans.SingleAsync()).Id;
        }

        using (var db = MoDb())
        {
            var kq = await new PhieuXuatBanService(db).ThucHienAsync(id);
            Assert.False(kq.ThanhCong);
            Assert.Contains("Không đủ", kq.ThongBao);
        }

        Assert.Equal(4m, TonLo("LO_A"));
        using (var db = MoDb())
            Assert.Equal(TrangThaiXuatBan.MoiTao, (await db.PhieuXuatBans.SingleAsync()).TrangThai);
    }

    [Fact]
    public async Task Khong_Thuc_Hien_Hai_Lan()
    {
        SeedDanhMuc();
        NhapThanhPham("LO_A", 20m, new DateOnly(2026, 1, 1));

        int id;
        using (var db = MoDb())
        {
            await new PhieuXuatBanService(db).TaoAsync(Phieu(("BANH_MI", 10m)));
            id = (await db.PhieuXuatBans.SingleAsync()).Id;
        }
        using (var db = MoDb()) Assert.True((await new PhieuXuatBanService(db).ThucHienAsync(id)).ThanhCong);
        using (var db = MoDb()) Assert.False((await new PhieuXuatBanService(db).ThucHienAsync(id)).ThanhCong);

        // Chỉ trừ 1 lần -> còn 10.
        Assert.Equal(10m, TonLo("LO_A"));
    }

    [Fact]
    public async Task Khong_Ban_Nguyen_Lieu()
    {
        SeedDanhMuc();
        using var db = MoDb();
        db.Products.Add(new Product { MaSanPham = "BOT_MI", TenSanPham = "Bột mì",
                                      LoaiSanPham = LoaiSanPham.NguyenLieu, DonViTinh = "kg" });
        db.SaveChanges();

        var kq = await new PhieuXuatBanService(db).TaoAsync(Phieu(("BOT_MI", 1m)));
        Assert.False(kq.ThanhCong);
        Assert.Contains("không phải thành phẩm", kq.ThongBao);
    }
}
