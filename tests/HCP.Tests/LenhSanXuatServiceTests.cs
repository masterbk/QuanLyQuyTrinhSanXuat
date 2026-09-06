using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.Kho;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng lệnh sản xuất: trừ nguyên liệu theo định mức đúng FEFO (lô hết hạn trước xuất
/// trước), cộng thành phẩm, chặn khi thiếu tồn và không cho thực hiện hai lần.
/// </summary>
public class LenhSanXuatServiceTests
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

    /// <summary>Thành phẩm BANH_MI (định mức 0.1kg bột/cái), nguyên liệu BOT_MI, kho KHO01.</summary>
    private void SeedDanhMuc()
    {
        using var db = MoDb();
        var banh = new Product { MaSanPham = "BANH_MI", TenSanPham = "Bánh mì", MaLoaiSp = "x",
                                 LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái" };
        banh.DanhSachDinhMuc.Add(new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0.1m });
        db.Products.Add(banh);
        db.Products.Add(new Product { MaSanPham = "BOT_MI", TenSanPham = "Bột mì",
                                      LoaiSanPham = LoaiSanPham.NguyenLieu, DonViTinh = "kg" });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
        db.SaveChanges();
    }

    private void NhapBot(string lo, decimal sl, DateOnly hsd)
    {
        using var db = MoDb();
        db.KhoGiaoDichs.Add(new KhoGiaoDich
        {
            MaSanPham = "BOT_MI", MaKho = "KHO01", MaLo = lo, SoLuong = sl, HanSuDung = hsd,
            Loai = LoaiGiaoDichKho.NhapNguyenLieu, ThoiGianUtc = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private decimal TonLo(string maSp, string lo)
    {
        using var db = MoDb();
        return db.KhoGiaoDichs.Where(g => g.MaSanPham == maSp && g.MaLo == lo).Sum(g => (decimal?)g.SoLuong) ?? 0m;
    }

    private static LenhSanXuat Lenh(decimal sl) => new()
    {
        MaLenh = "LSX-001", MaThanhPham = "BANH_MI", SoLuong = sl, MaKho = "KHO01",
        MaLoThanhPham = "LOTP1", NgaySanXuat = new DateOnly(2026, 9, 6)
    };

    [Fact]
    public async Task Thuc_Hien_Tru_Nguyen_Lieu_FEFO_Va_Cong_Thanh_Pham()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 0.6m, new DateOnly(2026, 1, 1));  // hết hạn sớm -> xuất trước
        NhapBot("LO_B", 0.6m, new DateOnly(2026, 6, 1));

        int id;
        using (var db = MoDb())
        {
            var svc = new LenhSanXuatService(db);
            var kq = await svc.TaoAsync(Lenh(10));   // cần 10 * 0.1 = 1.0 kg bột
            Assert.True(kq.ThanhCong, kq.ThongBao);
            id = (await db.LenhSanXuats.SingleAsync()).Id;
        }

        using (var db = MoDb())
        {
            var kq = await new LenhSanXuatService(db).ThucHienAsync(id);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        // FEFO: LO_A (0.6, hết hạn sớm) hết sạch, LO_B trừ 0.4 còn 0.2.
        Assert.Equal(0m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(0.2m, TonLo("BOT_MI", "LO_B"));
        // Thành phẩm nhập kho 10 cái.
        Assert.Equal(10m, TonLo("BANH_MI", "LOTP1"));

        using (var db = MoDb())
        {
            var lenh = await db.LenhSanXuats.Include(l => l.TieuHao).SingleAsync();
            Assert.Equal(TrangThaiLenhSX.HoanThanh, lenh.TrangThai);
            Assert.Equal(2, lenh.TieuHao.Count); // tiêu hao 2 lô
            Assert.Equal(0.6m, lenh.TieuHao.Single(t => t.MaLo == "LO_A").SoLuong);
            Assert.Equal(0.4m, lenh.TieuHao.Single(t => t.MaLo == "LO_B").SoLuong);
        }
    }

    [Fact]
    public async Task Thieu_Nguyen_Lieu_Thi_Khong_Tru_Gi()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 0.5m, new DateOnly(2026, 1, 1)); // chỉ 0.5kg, cần 1.0kg

        int id;
        using (var db = MoDb())
        {
            id = (await Tao(db, 10)).Id;
        }

        using (var db = MoDb())
        {
            var kq = await new LenhSanXuatService(db).ThucHienAsync(id);
            Assert.False(kq.ThanhCong);
            Assert.Contains("Không đủ", kq.ThongBao);
        }

        // Không phát sinh giao dịch nào ngoài dòng nhập ban đầu.
        Assert.Equal(0.5m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(0m, TonLo("BANH_MI", "LOTP1"));
        using (var db = MoDb())
            Assert.Equal(TrangThaiLenhSX.MoiTao, (await db.LenhSanXuats.SingleAsync()).TrangThai);
    }

    [Fact]
    public async Task Khong_Thuc_Hien_Hai_Lan()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 5m, new DateOnly(2026, 1, 1));

        int id;
        using (var db = MoDb()) id = (await Tao(db, 10)).Id;
        using (var db = MoDb()) Assert.True((await new LenhSanXuatService(db).ThucHienAsync(id)).ThanhCong);
        using (var db = MoDb()) Assert.False((await new LenhSanXuatService(db).ThucHienAsync(id)).ThanhCong);

        // Chỉ trừ 1 lần (10*0.1=1.0) -> còn 4.0.
        Assert.Equal(4.0m, TonLo("BOT_MI", "LO_A"));
    }

    private static async Task<LenhSanXuat> Tao(AppDbContext db, decimal sl)
    {
        await new LenhSanXuatService(db).TaoAsync(Lenh(sl));
        return await db.LenhSanXuats.OrderByDescending(l => l.Id).FirstAsync();
    }
}
