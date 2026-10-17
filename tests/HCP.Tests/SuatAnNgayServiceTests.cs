using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.SuatAn;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Bản tin suất ăn theo ngày (màn "Suất ăn" trên app): thực đơn lấy theo THỨ trong tuần, còn số suất
/// gộp từ đơn hàng bán có dòng món ăn theo NGÀY HẸN GIAO.
/// </summary>
public class SuatAnNgayServiceTests
{
    private const string CoSoA = "coso-a";
    private const string CoSoB = "coso-b";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb(string tenant = CoSoA)
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(tenant);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    /// <summary>Thứ 5 ngày 15/10/2026 - DayOfWeek = 4, đúng quy ước thu_ap_dung của HanoiCheck.</summary>
    private static readonly DateOnly ThuNam = new(2026, 10, 15);

    private static void Seed(AppDbContext db)
    {
        db.NhomTuoiHnCs.Add(new NhomTuoiHnC { Id = 3, Ten = "Mẫu giáo" });
        db.BuaAnHnCs.Add(new BuaAnHnC { Id = 1, Ten = "Bữa sáng", ThuTu = 1 });
        db.BuaAnHnCs.Add(new BuaAnHnC { Id = 2, Ten = "Bữa trưa", ThuTu = 2 });
        db.TruongHocHnCs.Add(new TruongHocHnC { TruongId = 77, Ten = "Mầm non Quang Trung" });

        db.Dishes.Add(new Dish { MaMonAn = "MON-CANH", TenMonAn = "Canh bí", NhomTuoiId = 3 });
        db.Dishes.Add(new Dish { MaMonAn = "MON-THIT", TenMonAn = "Thịt kho", NhomTuoiId = 3 });

        db.KhachHangs.Add(new KhachHang { MaKhachHang = "KH-MN", TenKhachHang = "Mầm non Quang Trung" });
        db.KhachHangs.Add(new KhachHang { MaKhachHang = "KH-TH", TenKhachHang = "Tiểu học Việt Úc" });
        db.SaveChanges();
    }

    private static Menu ThucDon(int thu, bool trangThai = true, string ma = "TD-01") => new()
    {
        MaThucDon = ma, TenThucDon = "Thực đơn " + ma, NhomTuoiId = 3, ThuApDung = thu,
        TrangThai = trangThai, DanhSachTruongCsv = "77",
        DanhSachMon =
        {
            new MenuItem { BuoiAnId = 2, MaMonAn = "MON-THIT" },
            new MenuItem { BuoiAnId = 1, MaMonAn = "MON-CANH" }
        }
    };

    private static DonHangBan Don(string ma, string maKhach, DateOnly ngayGiao, string maMon, decimal soSuat,
                                  TrangThaiDonHangBan trangThai = TrangThaiDonHangBan.DaXacNhan) => new()
    {
        MaDonHang = ma, MaKhachHang = maKhach, MaKho = "KHO01",
        NgayDat = ngayGiao, NgayGiao = ngayGiao, TrangThai = trangThai,
        Dong = { new DonHangBanDong { MaThanhPham = maMon, SoLuong = soSuat, LaMonAn = true } }
    };

    [Fact]
    public async Task Thuc_Don_Lay_Dung_Thu_Trong_Tuan_Va_Xep_Theo_Bua_An()
    {
        using (var db = MoDb())
        {
            Seed(db);
            db.Menus.Add(ThucDon(4, ma: "TD-T5"));              // thứ 5 - đúng ngày đang xem
            db.Menus.Add(ThucDon(5, ma: "TD-T6"));              // thứ 6 - không được lấy
            db.Menus.Add(ThucDon(4, trangThai: false, ma: "TD-NGUNG")); // ngừng áp dụng - không lấy
            db.SaveChanges();
        }

        using var db2 = MoDb();
        var kq = await new SuatAnNgayService(db2).LayTheoNgayAsync(ThuNam);

        Assert.Equal(4, kq.Thu);
        Assert.Equal("Thứ 5", kq.TenThu);
        var td = Assert.Single(kq.ThucDon);
        Assert.Equal("TD-T5", td.MaThucDon);
        Assert.Equal("Mẫu giáo", td.TenNhomTuoi);
        Assert.Equal(new[] { "Mầm non Quang Trung" }, td.TenTruong);

        // Bữa sáng xếp trước bữa trưa dù khai ngược thứ tự, và hiện TÊN món chứ không phải mã.
        Assert.Equal(new[] { "Bữa sáng", "Bữa trưa" }, td.DanhSachMon.Select(m => m.TenBuoiAn));
        Assert.Equal(new[] { "Canh bí", "Thịt kho" }, td.DanhSachMon.Select(m => m.TenMonAn));
    }

    [Fact]
    public async Task So_Suat_Gop_Theo_Truong_Va_Theo_Mon_Bo_Qua_Don_Huy()
    {
        using (var db = MoDb())
        {
            Seed(db);
            db.DonHangBans.Add(Don("DH-01", "KH-MN", ThuNam, "MON-THIT", 120));
            db.DonHangBans.Add(Don("DH-02", "KH-MN", ThuNam, "MON-CANH", 120));
            db.DonHangBans.Add(Don("DH-03", "KH-TH", ThuNam, "MON-THIT", 80));
            db.DonHangBans.Add(Don("DH-04", "KH-TH", ThuNam, "MON-THIT", 50,
                                   TrangThaiDonHangBan.DaHuy));                 // huỷ: không tính
            db.DonHangBans.Add(Don("DH-05", "KH-MN", ThuNam.AddDays(1), "MON-THIT", 999)); // ngày khác
            db.SaveChanges();
        }

        using var db2 = MoDb();
        var kq = await new SuatAnNgayService(db2).LayTheoNgayAsync(ThuNam);

        Assert.Equal(320m, kq.TongSuat);                      // 120 + 120 + 80
        Assert.Equal(2, kq.TheoKhachHang.Count);

        var mamNon = kq.TheoKhachHang.First();                // nhiều suất nhất xếp trước
        Assert.Equal("Mầm non Quang Trung", mamNon.TenKhachHang);
        Assert.Equal(240m, mamNon.TongSuat);
        Assert.Equal(new[] { "Canh bí", "Thịt kho" }, mamNon.DanhSachMon.Select(m => m.TenMonAn));

        var thit = kq.TongTheoMon.First();
        Assert.Equal("Thịt kho", thit.TenMonAn);
        Assert.Equal(200m, thit.SoSuat);                      // 120 + 80
    }

    [Fact]
    public async Task Khong_Lay_Du_Lieu_Cua_Co_So_Khac()
    {
        using (var db = MoDb(CoSoB))
        {
            db.Menus.Add(ThucDon(4, ma: "TD-B"));
            db.DonHangBans.Add(Don("DH-B", "KH-B", ThuNam, "MON-B", 500));
            db.SaveChanges();
        }

        using var db2 = MoDb(CoSoA);
        var kq = await new SuatAnNgayService(db2).LayTheoNgayAsync(ThuNam);

        Assert.Empty(kq.ThucDon);
        Assert.Empty(kq.TheoKhachHang);
        Assert.Equal(0m, kq.TongSuat);
    }

    [Fact]
    public async Task Ngay_Khong_Co_Gi_Thi_Tra_Rong_Chu_Khong_Loi()
    {
        using (var db = MoDb()) Seed(db);

        using var db2 = MoDb();
        var kq = await new SuatAnNgayService(db2).LayTheoNgayAsync(ThuNam.AddDays(2));  // Thứ 7

        Assert.Empty(kq.ThucDon);
        Assert.Empty(kq.TheoKhachHang);
        Assert.Equal("Thứ 7", kq.TenThu);
    }
}
