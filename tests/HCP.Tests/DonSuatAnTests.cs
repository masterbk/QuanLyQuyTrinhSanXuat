using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BanHang;
using HCP.Infrastructure.Services.MaTuSinh;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Đơn suất ăn / thực đơn (đặc tả HnC v2.6 mục 11, update_mode dish_ingredient_fulfillment): dòng hàng là món ăn,
/// số lượng là số suất; xuất kho trừ NGUYÊN LIỆU theo công thức (định lượng × số suất) và khai nguồn hàng theo
/// từng nguyên liệu ở chi_tiet[].nguyen_lieu[].
/// </summary>
public class DonSuatAnTests
{
    private const string CoSo = "coso-bep";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private static DonHangBanService Svc(AppDbContext db, FakeHanoiCheckOrderCommandClient fake) =>
        new(db, new MaTuSinhService(db), fake, new FakePushNotificationService());

    private static void Nhap(AppDbContext db, string sp, string lo, decimal sl, DateOnly hsd) =>
        db.KhoGiaoDichs.Add(new KhoGiaoDich
        {
            MaSanPham = sp, MaKho = "KHO01", MaLo = lo, SoLuong = sl, HanSuDung = hsd,
            Loai = LoaiGiaoDichKho.NhapThanhPham, ThoiGianUtc = DateTime.UtcNow
        });

    /// <summary>
    /// Thịt: lô L-T1 2kg (hết hạn trước) + L-T2 5kg; rau: L-R1 1kg. Món MA-1 = thịt 0,08 + rau 0,02 mỗi suất.
    /// Đơn HnC "DH-HNC-1": 30 suất MA-1 (trace TR-1).
    /// </summary>
    private int Seed(decimal soSuat = 30)
    {
        using var db = MoDb();
        db.Products.Add(new Product { MaSanPham = "TP-THIT", TenSanPham = "Thịt heo", MaLoaiSp = "969", DonViTinh = "kg" });
        db.Products.Add(new Product { MaSanPham = "TP-RAU", TenSanPham = "Rau cải", MaLoaiSp = "1000", DonViTinh = "kg" });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho bếp", DiaChi = "HN" });
        db.KhachHangs.Add(new KhachHang { MaKhachHang = "KH01", TenKhachHang = "MN Hoa Sen", Loai = LoaiKhachHang.TruongHoc });
        db.Staff.Add(new Staff { MaNhanSu = "NS01", HoTen = "Người giao" });
        Nhap(db, "TP-THIT", "L-T1", 2, new DateOnly(2026, 10, 20));
        Nhap(db, "TP-THIT", "L-T2", 5, new DateOnly(2026, 10, 25));
        Nhap(db, "TP-RAU", "L-R1", 1, new DateOnly(2026, 10, 18));
        db.Dishes.Add(new Dish
        {
            MaMonAn = "MA-1", TenMonAn = "Thịt xào rau", NhomTuoiId = 1,
            DanhSachNguyenLieu =
            {
                new DishIngredient { MaNguyenLieu = "TP-THIT", DinhLuong = 0.08m },
                new DishIngredient { MaNguyenLieu = "TP-RAU", DinhLuong = 0.02m }
            }
        });
        var don = new DonHangBan
        {
            MaKhachHang = "KH01", MaKho = "KHO01", MaDonHang = "DH-001", NgayDat = new DateOnly(2026, 10, 15),
            Nguon = NguonDonHang.HanoiCheck, MaDonHnC = "DH-HNC-1", TrangThai = TrangThaiDonHangBan.DaXacNhan,
            Dong = { new DonHangBanDong { MaThanhPham = "MA-1", SoLuong = soSuat, LaMonAn = true, MaTruyVetHnC = "TR-1" } }
        };
        db.DonHangBans.Add(don);
        db.SaveChanges();
        return don.Id;
    }

    private decimal Ton(string sp, string lo)
    {
        using var db = MoDb();
        return db.KhoGiaoDichs.Where(g => g.MaSanPham == sp && g.MaLo == lo).Sum(g => g.SoLuong);
    }

    [Fact]
    public async Task Goi_Y_Xuat_Kho_Tach_Theo_Nguyen_Lieu_Va_FEFO()
    {
        var id = Seed();
        using var db = MoDb();
        var goiY = await Svc(db, new FakeHanoiCheckOrderCommandClient()).GoiYXuatKhoAsync(id);

        Assert.Equal(2, goiY.Count);
        var thit = goiY.Single(g => g.MaNguyenLieu == "TP-THIT");
        Assert.Equal(2.4m, thit.SoLuong);                    // 0,08 × 30 suất
        Assert.Equal("Thịt xào rau", thit.TenMonAn);
        Assert.Equal(30m, thit.SoSuat);
        Assert.Equal(2m, thit.Lo.Single(l => l.MaLo == "L-T1").GoiY);   // lô hết hạn trước lấy hết
        Assert.Equal(0.4m, thit.Lo.Single(l => l.MaLo == "L-T2").GoiY);
        Assert.Equal(0.6m, goiY.Single(g => g.MaNguyenLieu == "TP-RAU").Lo.Single().GoiY);
    }

    [Fact]
    public async Task Xuat_Kho_Day_Nguyen_Lieu_Sang_HnC_Va_Tru_Ton_Nguyen_Lieu()
    {
        var id = Seed();
        ProcessOrderRequest? daGui = null;
        var fake = new FakeHanoiCheckOrderCommandClient
        {
            XuLyDon = (_, req) => { daGui = req; return OrderCommandResult.Ok(); },
            DoiTrangThai = (_, _) => OrderCommandResult.Ok()
        };

        using (var db = MoDb())
        {
            var kq = await Svc(db, fake).XuatKhoAsync(id, null, "NS01");
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        // Một dòng định danh bằng trace_code, khai theo nguyên liệu - KHÔNG có phan_bo trực tiếp.
        var dong = Assert.Single(daGui!.ChiTiet);
        Assert.Equal("TR-1", dong.TraceCode);
        Assert.Null(dong.MaMonAn);
        Assert.NotNull(dong.NguyenLieu);
        Assert.Empty(dong.PhanBo);
        var thit = dong.NguyenLieu!.Single(n => n.MaThucPham == "TP-THIT");
        Assert.Equal(new[] { ("L-T1", "KHO01", 2m), ("L-T2", "KHO01", 0.4m) },
                     thit.PhanBo.Select(p => (p.MaLo, p.MaKho, p.SoLuong)).ToArray());
        Assert.Equal(0.6m, dong.NguyenLieu!.Single(n => n.MaThucPham == "TP-RAU").PhanBo.Sum(p => p.SoLuong));

        // Trừ tồn NGUYÊN LIỆU (không phải mã món), lưu lô xuất kèm mã nguyên liệu.
        Assert.Equal(0m, Ton("TP-THIT", "L-T1"));
        Assert.Equal(4.6m, Ton("TP-THIT", "L-T2"));
        Assert.Equal(0.4m, Ton("TP-RAU", "L-R1"));
        using var db2 = MoDb();
        var don = await db2.DonHangBans.Include(d => d.Dong).ThenInclude(l => l.XuatLo).SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.DangGiao, don.TrangThai);
        Assert.Equal(3, don.Dong.Single().XuatLo.Count);
        Assert.All(don.Dong.Single().XuatLo, x => Assert.NotNull(x.MaNguyenLieu));
    }

    [Fact]
    public async Task Khong_Du_Nguyen_Lieu_Thi_Chan_Va_Khong_Goi_HnC()
    {
        var id = Seed(soSuat: 60);    // cần 4,8kg thịt (đủ) và 1,2kg rau - kho chỉ còn 1kg rau
        var daGoi = false;
        var fake = new FakeHanoiCheckOrderCommandClient { XuLyDon = (_, _) => { daGoi = true; return OrderCommandResult.Ok(); } };

        using var db = MoDb();
        var kq = await Svc(db, fake).XuatKhoAsync(id, null, "NS01");
        Assert.False(kq.ThanhCong);
        Assert.Contains("Rau cải", kq.ThongBao);
        Assert.False(daGoi);
        Assert.Equal(1m, Ton("TP-RAU", "L-R1"));
    }

    [Fact]
    public async Task Phan_Bo_Tay_Phai_Du_Tung_Nguyen_Lieu()
    {
        var id = Seed();
        using var db = MoDb();
        var kq = await Svc(db, new FakeHanoiCheckOrderCommandClient()).XuatKhoAsync(id, new[]
        {
            new PhanBoLoRequest(0, "L-T2", 2.4m, "TP-THIT")   // DongId sai -> sửa bên dưới
        }, "NS01");
        Assert.False(kq.ThanhCong);

        var dongId = db.DonHangBanDongs.Single().Id;
        var thieuRau = await Svc(db, new FakeHanoiCheckOrderCommandClient()).XuatKhoAsync(id, new[]
        {
            new PhanBoLoRequest(dongId, "L-T2", 2.4m, "TP-THIT")
        }, "NS01");
        Assert.False(thieuRau.ThanhCong);
        Assert.Contains("Rau cải", thieuRau.ThongBao);
    }

    [Fact]
    public async Task Huy_Don_Da_Xuat_Tra_Ve_Dung_Lo_Nguyen_Lieu()
    {
        var id = Seed();
        using (var db = MoDb())
            Assert.True((await Svc(db, new FakeHanoiCheckOrderCommandClient()).XuatKhoAsync(id, null, "NS01")).ThanhCong);
        using (var db = MoDb())
            Assert.True((await Svc(db, new FakeHanoiCheckOrderCommandClient()).HuyAsync(id, "Trường báo nghỉ")).ThanhCong);

        Assert.Equal(2m, Ton("TP-THIT", "L-T1"));
        Assert.Equal(5m, Ton("TP-THIT", "L-T2"));
        Assert.Equal(1m, Ton("TP-RAU", "L-R1"));
    }

    [Fact]
    public async Task Mon_Chua_Co_Cong_Thuc_Thi_Khong_Xuat_Duoc()
    {
        var id = Seed();
        using (var db = MoDb())
        {
            db.DishIngredients.RemoveRange(db.DishIngredients);
            db.SaveChanges();
        }
        using var db2 = MoDb();
        var kq = await Svc(db2, new FakeHanoiCheckOrderCommandClient()).XuatKhoAsync(id, null, "NS01");
        Assert.False(kq.ThanhCong);
        Assert.Contains("chưa có công thức", kq.ThongBao);
    }

    [Fact]
    public async Task Dong_Mon_An_Tu_HnC_Thanh_Dong_Suat_An()
    {
        Seed();
        using (var db = MoDb())
        {
            db.DonHangNhans.Add(new DonHangNhan
            {
                TenantId = CoSo, MaDonHang = "DH-HNC-2", TenTruong = "MN Hoa Sen", TrangThai = "CHO_XAC_NHAN",
                NgayTaoTrenHnC = new DateTime(2026, 10, 15, 8, 0, 0), DaLayChiTiet = true, LanDongBoUtc = DateTime.UtcNow,
                Dong =
                {
                    new DonHangNhanDong { TenantId = CoSo, MaSanPham = "MA-1", TenSanPham = "Thịt xào rau", SoLuong = 120, MaTruyVet = "TR-9" }
                }
            });
            db.SaveChanges();
        }
        using (var db = MoDb())
            Assert.Equal(1, await new DonHangHnCService(db, new MaTuSinhService(db), new FakePushNotificationService())
                .DongBoVaoDonHangBanAsync());

        using var db2 = MoDb();
        var dong = db2.DonHangBans.Include(d => d.Dong).Single(d => d.MaDonHnC == "DH-HNC-2").Dong.Single();
        Assert.True(dong.LaMonAn);
        Assert.Equal(("MA-1", 120m, "TR-9"), (dong.MaThanhPham, dong.SoLuong, dong.MaTruyVetHnC));
    }

    [Fact]
    public async Task Sua_Don_Giu_Trace_Code_Va_Loai_Dong()
    {
        var id = Seed();
        using var db = MoDb();
        var svc = Svc(db, new FakeHanoiCheckOrderCommandClient());
        var kq = await svc.CapNhatAsync(new DonHangBan
        {
            Id = id, MaKhachHang = "KH01", MaKho = "KHO01",
            Dong = { new DonHangBanDong { MaThanhPham = "MA-1", SoLuong = 30, DonGia = 25000 } }   // form không mang cờ/trace
        });
        Assert.True(kq.ThanhCong, kq.ThongBao);

        using var db2 = MoDb();
        var dong = db2.DonHangBanDongs.Single();
        Assert.True(dong.LaMonAn);
        Assert.Equal("TR-1", dong.MaTruyVetHnC);
    }
}
