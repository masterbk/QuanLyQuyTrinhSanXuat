using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BanHang;
using HCP.Infrastructure.Services.MaTuSinh;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Đẩy ngược xử lý đơn hàng HanoiCheck (Bước 4): Xác nhận đẩy "Đang chuẩn bị", Xuất kho đẩy process rồi
/// "Đang giao" TRƯỚC khi trừ tồn/đổi trạng thái nội bộ - lỗi thật ở 1 trong 2 bước thì không đổi gì cả.
/// </summary>
public class DonHangBanServiceHnCTests
{
    private const string CoSo = "coso-hnc";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    /// <summary>BANH_MI tồn 1 lô LO1 = 10 ở KHO01. Đơn nguồn HanoiCheck "NCC-001", 2 dòng cùng mã thành phẩm
    /// nhưng trace_code khác nhau (mô phỏng 1 sản phẩm xuất hiện 2 lần trong đơn HnC).</summary>
    private int Seed()
    {
        using var db = MoDb();
        db.Products.Add(new Product { MaSanPham = "BANH_MI", TenSanPham = "Bánh mì", MaLoaiSp = "x",
                                      LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái" });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
        db.KhachHangs.Add(new KhachHang { MaKhachHang = "KH01", TenKhachHang = "Trường A" });
        db.Staff.Add(new Staff { MaNhanSu = "NS01", HoTen = "Người giao" });
        db.KhoGiaoDichs.Add(new KhoGiaoDich
        {
            MaSanPham = "BANH_MI", MaKho = "KHO01", MaLo = "LO1", SoLuong = 10,
            Loai = LoaiGiaoDichKho.NhapThanhPham, ThoiGianUtc = DateTime.UtcNow
        });
        var don = new DonHangBan
        {
            MaKhachHang = "KH01", MaKho = "KHO01", MaDonHang = "DH-001", NgayDat = new DateOnly(2026, 9, 16),
            Nguon = NguonDonHang.HanoiCheck, MaDonHnC = "NCC-001", TrangThai = TrangThaiDonHangBan.ChoXacNhan,
            Dong = new List<DonHangBanDong>
            {
                new() { MaThanhPham = "BANH_MI", SoLuong = 4, DonGia = 0, MaTruyVetHnC = "TRACE-1" },
                new() { MaThanhPham = "BANH_MI", SoLuong = 3, DonGia = 0, MaTruyVetHnC = "TRACE-2" }
            }
        };
        db.DonHangBans.Add(don);
        db.SaveChanges();
        return don.Id;
    }

    private static DonHangBanService Svc(AppDbContext db, FakeHanoiCheckOrderCommandClient fake) =>
        new(db, new MaTuSinhService(db), fake);

    [Fact]
    public async Task Xac_Nhan_Day_Dang_Chuan_Bi_Sang_HanoiCheck()
    {
        var id = Seed();
        var fake = new FakeHanoiCheckOrderCommandClient();
        var goiDung = new List<(string MaDon, string TrangThai)>();
        fake.DoiTrangThai = (maDon, tt) => { goiDung.Add((maDon, tt)); return OrderCommandResult.Ok(); };

        using var db = MoDb();
        var kq = await Svc(db, fake).XacNhanAsync(id);
        Assert.True(kq.ThanhCong, kq.ThongBao);
        Assert.Equal(("NCC-001", "DANG_CHUAN_BI"), Assert.Single(goiDung));

        var don = await db.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.DaXacNhan, don.TrangThai);
    }

    [Fact]
    public async Task Xac_Nhan_Loi_HanoiCheck_Thi_Khong_Doi_Trang_Thai_Noi_Bo()
    {
        var id = Seed();
        var fake = new FakeHanoiCheckOrderCommandClient { DoiTrangThai = (_, _) => OrderCommandResult.Loi("HnC từ chối") };

        using var db = MoDb();
        var kq = await Svc(db, fake).XacNhanAsync(id);
        Assert.False(kq.ThanhCong);
        Assert.Contains("HnC từ chối", kq.ThongBao);

        var don = await db.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.ChoXacNhan, don.TrangThai);
    }

    [Fact]
    public async Task Xac_Nhan_Cong_Tac_Tat_Van_Xac_Nhan_Noi_Bo()
    {
        var id = Seed();
        var fake = new FakeHanoiCheckOrderCommandClient(); // mặc định ChuaCauHinh

        using var db = MoDb();
        var kq = await Svc(db, fake).XacNhanAsync(id);
        Assert.True(kq.ThanhCong, kq.ThongBao);
        var don = await db.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.DaXacNhan, don.TrangThai);
    }

    [Fact]
    public async Task Xuat_Kho_Day_Process_Roi_Status_Dung_Thu_Tu_Kem_Trace_Code_Tung_Dong()
    {
        var id = Seed();
        var fake = new FakeHanoiCheckOrderCommandClient();
        var thuTuGoi = new List<string>();
        ProcessOrderRequest? noiDungDaGui = null;
        fake.XuLyDon = (maDon, req) => { thuTuGoi.Add("process:" + maDon); noiDungDaGui = req; return OrderCommandResult.Ok(); };
        fake.DoiTrangThai = (maDon, tt) => { thuTuGoi.Add($"status:{maDon}:{tt}"); return OrderCommandResult.Ok(); };

        using var db = MoDb();
        var kq = await Svc(db, fake).XuatKhoAsync(id, null, "NS01", "Giao trước 7h", null);
        Assert.True(kq.ThanhCong, kq.ThongBao);

        Assert.Equal(new[] { "process:NCC-001", "status:NCC-001:DANG_GIAO" }, thuTuGoi);
        Assert.NotNull(noiDungDaGui);
        Assert.Equal("NS01", noiDungDaGui!.MaNguoiGiao);
        Assert.Equal("Giao trước 7h", noiDungDaGui.GhiChu);
        Assert.Equal(2, noiDungDaGui.ChiTiet.Count);
        Assert.Equal(new[] { "TRACE-1", "TRACE-2" }, noiDungDaGui.ChiTiet.Select(c => c.TraceCode).OrderBy(x => x));
        var dong1 = noiDungDaGui.ChiTiet.Single(c => c.TraceCode == "TRACE-1");
        Assert.Equal(new[] { ("LO1", "KHO01", 4m) }, dong1.PhanBo.Select(p => (p.MaLo, p.MaKho, p.SoLuong)));

        var don = await db.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.DangGiao, don.TrangThai);
        Assert.Equal(3m, await db.KhoGiaoDichs.Where(g => g.MaLo == "LO1").SumAsync(g => g.SoLuong)); // 10 - 7
    }

    [Fact]
    public async Task Don_Cu_Chua_Co_Trace_Code_Thi_Gui_Ma_Thuc_Pham_Thay_The()
    {
        var id = Seed();
        using (var db = MoDb())
        {
            // Đơn kéo về trước khi hệ thống lưu mã truy vết (hoặc chưa lấy được chi tiết đơn).
            var don = await db.DonHangBans.Include(d => d.Dong).SingleAsync(d => d.Id == id);
            foreach (var l in don.Dong) l.MaTruyVetHnC = null;
            await db.SaveChangesAsync();
        }

        var fake = new FakeHanoiCheckOrderCommandClient();
        ProcessOrderRequest? daGui = null;
        fake.XuLyDon = (_, req) => { daGui = req; return OrderCommandResult.Ok(); };
        fake.DoiTrangThai = (_, _) => OrderCommandResult.Ok();

        using var db2 = MoDb();
        var kq = await Svc(db2, fake).XuatKhoAsync(id, null, "NS01");
        Assert.True(kq.ThanhCong, kq.ThongBao);

        // Gộp 2 dòng cùng thực phẩm thành MỘT dòng (HanoiCheck đòi mã thực phẩm chỉ xuất hiện một lần).
        var dong = Assert.Single(daGui!.ChiTiet);
        Assert.Null(dong.TraceCode);
        Assert.Equal("BANH_MI", dong.MaThucPham);
        Assert.Equal(7m, dong.PhanBo.Sum(p => p.SoLuong));
    }

    [Fact]
    public async Task Xuat_Kho_Loi_Process_Thi_Khong_Tru_Kho_Khong_Doi_Trang_Thai()
    {
        var id = Seed();
        var fake = new FakeHanoiCheckOrderCommandClient { XuLyDon = (_, _) => OrderCommandResult.Loi("422 thiếu dữ liệu") };

        using var db = MoDb();
        var kq = await Svc(db, fake).XuatKhoAsync(id, null, "NS01");
        Assert.False(kq.ThanhCong);
        Assert.Contains("422 thiếu dữ liệu", kq.ThongBao);

        Assert.Equal(10m, await db.KhoGiaoDichs.Where(g => g.MaLo == "LO1").SumAsync(g => g.SoLuong)); // chưa trừ gì
        var don = await db.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.ChoXacNhan, don.TrangThai);
        Assert.Null(don.ThoiGianXuatKhoUtc);
    }

    [Fact]
    public async Task Xuat_Kho_Loi_Status_Thi_Khong_Tru_Kho_Khong_Doi_Trang_Thai()
    {
        var id = Seed();
        var fake = new FakeHanoiCheckOrderCommandClient
        {
            XuLyDon = (_, _) => OrderCommandResult.Ok(),
            DoiTrangThai = (_, _) => OrderCommandResult.Loi("Đơn chưa có người giao")
        };

        using var db = MoDb();
        var kq = await Svc(db, fake).XuatKhoAsync(id, null, "NS01");
        Assert.False(kq.ThanhCong);
        Assert.Contains("Đơn chưa có người giao", kq.ThongBao);

        Assert.Equal(10m, await db.KhoGiaoDichs.Where(g => g.MaLo == "LO1").SumAsync(g => g.SoLuong));
        var don = await db.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.ChoXacNhan, don.TrangThai);
    }

    [Fact]
    public async Task Xuat_Kho_Cong_Tac_Tat_Van_Xuat_Kho_Noi_Bo_Khong_Goi_HanoiCheck()
    {
        var id = Seed();
        var fake = new FakeHanoiCheckOrderCommandClient(); // mặc định ChuaCauHinh, không set callback
        var goiProcess = false;
        fake.XuLyDon = (_, _) => { goiProcess = true; return OrderCommandResult.ChuaCauHinhKq("tắt"); };

        using var db = MoDb();
        var kq = await Svc(db, fake).XuatKhoAsync(id, null, "NS01");
        Assert.True(kq.ThanhCong, kq.ThongBao);
        Assert.True(goiProcess);   // process vẫn được gọi (để thử), chỉ status bị bỏ qua vì ChuaCauHinh

        var don = await db.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.DangGiao, don.TrangThai);
    }
}
