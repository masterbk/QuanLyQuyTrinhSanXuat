using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services;
using HCP.Infrastructure.Services.BanHang;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Services.ThongBao;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Nhân viên giao hàng trên app: nhận đơn ĐÃ XUẤT KHO, xác nhận đã giao kèm ảnh bắt buộc, tìm đơn từ mã QR.
/// Đơn từ trường (HanoiCheck) phải gửi ảnh giao rồi mới đẩy trạng thái "Đã giao".
/// </summary>
public class DonHangGiaoHangTests
{
    private const string CoSo = "coso-a";
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly FakeHanoiCheckOrderCommandClient _hnc = new();

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private DonHangBanService Svc(AppDbContext db) => new(db, new MaTuSinhService(db), _hnc, new FakePushNotificationService());

    private static readonly IReadOnlyList<AnhDauVao> AnhGiao = new[]
    {
        new AnhDauVao("giao-1.jpg", "https://app.vn/uploads/giao-1.jpg")
    };

    private void Seed()
    {
        using var db = MoDb();
        db.Products.Add(new Product { MaSanPham = "BANH_MI", TenSanPham = "Bánh mì", MaLoaiSp = "x",
                                      LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái" });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
        db.KhachHangs.Add(new KhachHang { MaKhachHang = "KH01", TenKhachHang = "Trường A" });
        db.Staff.Add(new Staff { MaNhanSu = "NS01", HoTen = "Người giao 1" });
        db.Staff.Add(new Staff { MaNhanSu = "NS02", HoTen = "Người giao 2" });
        db.Staff.Add(new Staff { MaNhanSu = "NS_NGHI", HoTen = "Đã nghỉ", TrangThai = false });
        db.KhoGiaoDichs.Add(new KhoGiaoDich
        {
            MaSanPham = "BANH_MI", MaKho = "KHO01", MaLo = "LOTP_A", SoLuong = 20,
            HanSuDung = new DateOnly(2026, 9, 30), Loai = LoaiGiaoDichKho.NhapThanhPham, ThoiGianUtc = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    /// <summary>Đơn nội bộ (hoặc HanoiCheck nếu truyền mã) đã xuất kho, chưa có người giao.</summary>
    private async Task<int> DonDangGiaoAsync(string? maDonHnC = null)
    {
        var don = new DonHangBan
        {
            MaKhachHang = "KH01", MaKho = "KHO01", NgayDat = new DateOnly(2026, 9, 18),
            DiaChiGiao = "12 Láng Hạ",
            Nguon = maDonHnC is null ? NguonDonHang.NoiBo : NguonDonHang.HanoiCheck,
            MaDonHnC = maDonHnC,
            Dong = { new DonHangBanDong { MaThanhPham = "BANH_MI", SoLuong = 2, DonGia = 5000, MaTruyVetHnC = maDonHnC is null ? null : "MTX-1" } }
        };
        using (var db = MoDb()) Assert.True((await Svc(db).TaoAsync(don)).ThanhCong);
        using (var db = MoDb()) Assert.True((await Svc(db).XuatKhoAsync(don.Id, null, null)).ThanhCong);
        return don.Id;
    }

    [Fact]
    public async Task Nhan_Don_Chi_Voi_Don_Da_Xuat_Kho_Va_Chua_Ai_Nhan()
    {
        Seed();
        var id = await DonDangGiaoAsync();

        using (var db = MoDb())
        {
            var svc = Svc(db);
            Assert.Contains("hồ sơ nhân sự", (await svc.NhanDonAsync(id, "NS_LA")).ThongBao);
            Assert.Contains("nghỉ", (await svc.NhanDonAsync(id, "NS_NGHI")).ThongBao);
            Assert.Contains("Bạn đã nhận", (await svc.NhanDonAsync(id, "NS01")).ThongBao);
            Assert.Contains("Bạn đang giao", (await svc.NhanDonAsync(id, "NS01")).ThongBao);   // bấm lại
            Assert.Contains("Người giao 1 nhận rồi", (await svc.NhanDonAsync(id, "NS02")).ThongBao);
        }
        using (var db = MoDb()) Assert.Equal("NS01", (await db.DonHangBans.SingleAsync()).MaNguoiGiao);

        // Đơn chưa xuất kho thì chưa nhận được.
        var chuaXuat = new DonHangBan
        {
            MaKhachHang = "KH01", MaKho = "KHO01", NgayDat = new DateOnly(2026, 9, 18),
            Dong = { new DonHangBanDong { MaThanhPham = "BANH_MI", SoLuong = 1, DonGia = 1000 } }
        };
        using (var db = MoDb()) Assert.True((await Svc(db).TaoAsync(chuaXuat)).ThanhCong);
        using (var db = MoDb())
            Assert.Contains("chưa xuất kho", (await Svc(db).NhanDonAsync(chuaXuat.Id, "NS01")).ThongBao);
    }

    [Fact]
    public async Task Xac_Nhan_Da_Giao_Bat_Buoc_Anh_Va_Giu_Anh_Tong_Quan()
    {
        Seed();
        var id = await DonDangGiaoAsync();
        using (var db = MoDb())
        {
            // Ảnh tổng quan lúc xuất kho (xuất kho lại để gắn ảnh) - không bị ảnh giao ghi đè.
            var don = await db.DonHangBans.Include(d => d.AnhTongQuan).SingleAsync();
            don.AnhTongQuan.Add(new DonHangBanAnh { TenAnh = "tq.jpg", DuongDan = "https://app.vn/uploads/tq.jpg" });
            await db.SaveChangesAsync();
        }

        using (var db = MoDb())
        {
            var svc = Svc(db);
            Assert.Contains("ít nhất 1 ảnh", (await svc.HoanTatGiaoAsync(id, Array.Empty<AnhDauVao>())).ThongBao);
            var kq = await svc.HoanTatGiaoAsync(id, AnhGiao);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        using (var db = MoDb())
        {
            var don = await db.DonHangBans.Include(d => d.AnhTongQuan).SingleAsync();
            Assert.Equal(TrangThaiDonHangBan.DaGiao, don.TrangThai);
            Assert.Equal(new[] { LoaiAnhDonHang.TongQuan, LoaiAnhDonHang.Giao },
                         don.AnhTongQuan.OrderBy(a => a.Loai).Select(a => a.Loai));
            Assert.Contains("đã giao", (await Svc(db).HoanTatGiaoAsync(id, AnhGiao)).ThongBao);   // không giao hai lần
        }
    }

    [Fact]
    public async Task Don_Tu_Truong_Gui_Anh_Giao_Roi_Moi_Doi_Trang_Thai_Sang_HanoiCheck()
    {
        Seed();
        var id = await DonDangGiaoAsync("DH-HNC-1");

        var anhDaGui = new List<string>();
        var trangThaiDaGui = new List<string>();
        var soDongDaGui = new List<int>();
        _hnc.XuLyDon = (_, noiDung) =>
        {
            anhDaGui.AddRange(noiDung.DanhSachAnh ?? new List<string>());
            soDongDaGui.Add(noiDung.ChiTiet.Count);
            return OrderCommandResult.Ok();
        };
        _hnc.DoiTrangThai = (_, tt) => { trangThaiDaGui.Add(tt); return OrderCommandResult.Ok(); };

        using (var db = MoDb()) Assert.True((await Svc(db).NhanDonAsync(id, "NS01")).ThanhCong);
        using (var db = MoDb()) Assert.True((await Svc(db).HoanTatGiaoAsync(id, AnhGiao)).ThanhCong);

        Assert.Contains("https://app.vn/uploads/giao-1.jpg", anhDaGui);
        // Nhận đơn và xác nhận giao KHÔNG khai lại nguồn hàng - client bỏ hẳn khoá chi_tiet để HnC không trả 422.
        Assert.Equal(new[] { 0, 0 }, soDongDaGui);
        // Chỉ tính từ lúc gắn hàm giả: "DANG_GIAO" đã đẩy ở bước xuất kho phía trên.
        Assert.Equal(new[] { "DA_GIAO" }, trangThaiDaGui);

        // HanoiCheck từ chối -> giữ nguyên "Đang giao" để bấm lại.
        var id2 = await DonDangGiaoAsync("DH-HNC-2");
        _hnc.DoiTrangThai = (_, tt) => tt == "DA_GIAO" ? OrderCommandResult.Loi("422 thiếu ảnh") : OrderCommandResult.Ok();
        using (var db = MoDb())
            Assert.Contains("Không đẩy được trạng thái", (await Svc(db).HoanTatGiaoAsync(id2, AnhGiao)).ThongBao);
        using (var db = MoDb())
            Assert.Equal(TrangThaiDonHangBan.DangGiao, (await db.DonHangBans.SingleAsync(d => d.Id == id2)).TrangThai);
    }

    [Fact]
    public async Task Tim_Don_Tu_Noi_Dung_Ma_QR()
    {
        Seed();
        var id = await DonDangGiaoAsync("DH-HNC-9");
        string maTraCuu, maDon;
        using (var db = MoDb())
        {
            var don = await db.DonHangBans.SingleAsync();
            don.MaTraCuu = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
            (maTraCuu, maDon) = (don.MaTraCuu, don.MaDonHang);
        }

        using var db2 = MoDb();
        var svc = Svc(db2);
        Assert.Equal(id, (await svc.TimTheoQrAsync($"https://quanly.vn/tra-cuu/don-hang/{maTraCuu}"))!.Id);
        Assert.Equal(id, (await svc.TimTheoQrAsync("https://tracuu.hanoicheck.com.vn/NCC-1/truy-xuat/DH-HNC-9"))!.Id);
        Assert.Equal(id, (await svc.TimTheoQrAsync(maDon))!.Id);
        Assert.Null(await svc.TimTheoQrAsync("https://quanly.vn/tra-cuu/lo/abc"));   // QR của lô sản xuất
        Assert.Null(await svc.TimTheoQrAsync("LSX:LSX-20260918-001"));               // QR của lệnh sản xuất
        Assert.Null(await svc.TimTheoQrAsync(""));
    }
}
