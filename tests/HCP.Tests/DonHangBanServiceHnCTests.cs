using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BanHang;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Services.ThongBao;
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
        new(db, new MaTuSinhService(db), fake, new FakePushNotificationService());

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

    /// <summary>Thêm DonHangNhan "NCC-001" khớp đơn seed - tái hiện đúng bảng nguồn job đồng bộ ghi.</summary>
    private void SeedDonHangNhan(bool daLayChiTiet, params decimal?[] soLuongTungDong)
    {
        using var db = MoDb();
        db.DonHangNhans.Add(new DonHangNhan
        {
            TenantId = CoSo, MaDonHang = "NCC-001", DaLayChiTiet = daLayChiTiet,
            Dong = soLuongTungDong.Select(sl => new DonHangNhanDong
            {
                TenantId = CoSo, MaSanPham = "BANH_MI", SoLuong = sl
            }).ToList()
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Xac_Nhan_Chan_Khi_Don_HnC_Chua_Lay_Xong_Chi_Tiet()
    {
        var id = Seed();
        SeedDonHangNhan(daLayChiTiet: false);
        var fake = new FakeHanoiCheckOrderCommandClient();

        using var db = MoDb();
        var kq = await Svc(db, fake).XacNhanAsync(id);
        Assert.False(kq.ThanhCong);
        Assert.Contains("chưa lấy xong chi tiết", kq.ThongBao);

        var don = await db.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.ChoXacNhan, don.TrangThai);
    }

    [Fact]
    public async Task Xac_Nhan_Chan_Khi_Con_Dong_Chua_Co_So_Luong()
    {
        var id = Seed();
        // Đã lấy chi tiết một lần nhưng còn 1 dòng chưa có số lượng (VD trường vừa thêm mặt hàng).
        SeedDonHangNhan(daLayChiTiet: true, 4m, null);
        var fake = new FakeHanoiCheckOrderCommandClient();

        using var db = MoDb();
        var kq = await Svc(db, fake).XacNhanAsync(id);
        Assert.False(kq.ThanhCong);
        Assert.Contains("chưa lấy xong chi tiết", kq.ThongBao);

        var don = await db.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.ChoXacNhan, don.TrangThai);
    }

    [Fact]
    public async Task Xac_Nhan_Duoc_Khi_Da_Lay_Du_Chi_Tiet()
    {
        var id = Seed();
        SeedDonHangNhan(daLayChiTiet: true, 4m, 3m);
        var fake = new FakeHanoiCheckOrderCommandClient { DoiTrangThai = (_, _) => OrderCommandResult.Ok() };

        using var db = MoDb();
        var kq = await Svc(db, fake).XacNhanAsync(id);
        Assert.True(kq.ThanhCong, kq.ThongBao);

        var don = await db.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.DaXacNhan, don.TrangThai);
    }

    /// <summary>Tái hiện lỗi thật gặp trên production 18/09: mặt hàng thứ 2 của đơn được tạo thẳng
    /// trên HanoiCheck TRƯỚC, hệ thống chưa có trong danh mục lúc đồng bộ nên bị bỏ (đơn chỉ còn 1
    /// dòng) - sau đó thêm mặt hàng vào danh mục thì phải CHẶN xác nhận, không được lặng lẽ bỏ qua.</summary>
    [Fact]
    public async Task Xac_Nhan_Chan_Khi_Mat_Hang_Da_Duoc_Them_Vao_Danh_Muc_Nhung_Chua_Vao_Don()
    {
        var id = Seed();
        using (var db = MoDb())
        {
            db.Products.Add(new Product { MaSanPham = "BANH_NGOT", TenSanPham = "Bánh ngọt", MaLoaiSp = "x",
                                          LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái" });
            db.DonHangNhans.Add(new DonHangNhan
            {
                TenantId = CoSo, MaDonHang = "NCC-001", DaLayChiTiet = true,
                Dong = new List<DonHangNhanDong>
                {
                    new() { TenantId = CoSo, MaSanPham = "BANH_MI", SoLuong = 7 },
                    new() { TenantId = CoSo, MaSanPham = "BANH_NGOT", TenSanPham = "Bánh ngọt", SoLuong = 5 },
                }
            });
            db.SaveChanges();
        }
        var fake = new FakeHanoiCheckOrderCommandClient();

        using var db2 = MoDb();
        var kq = await Svc(db2, fake).XacNhanAsync(id);
        Assert.False(kq.ThanhCong);
        Assert.Contains("BANH_NGOT", kq.ThongBao);
        Assert.Contains("chưa được đưa vào đơn", kq.ThongBao);

        var don = await db2.DonHangBans.SingleAsync(d => d.Id == id);
        Assert.Equal(TrangThaiDonHangBan.ChoXacNhan, don.TrangThai);
    }

    /// <summary>Mặt hàng thiếu nhưng VẪN CHƯA có trong danh mục (chưa kịp thêm) - không chặn được gì
    /// (không có cách sửa), giữ hành vi cũ: vẫn xác nhận được, ghi chú đã cảnh báo sẵn trên đơn.</summary>
    [Fact]
    public async Task Xac_Nhan_Duoc_Khi_Mat_Hang_Thieu_Van_Chua_Co_Trong_Danh_Muc()
    {
        var id = Seed();
        using (var db = MoDb())
        {
            db.DonHangNhans.Add(new DonHangNhan
            {
                TenantId = CoSo, MaDonHang = "NCC-001", DaLayChiTiet = true,
                Dong = new List<DonHangNhanDong>
                {
                    new() { TenantId = CoSo, MaSanPham = "BANH_MI", SoLuong = 7 },
                    new() { TenantId = CoSo, MaSanPham = "CHUA_CO_TRONG_DANH_MUC", SoLuong = 5 },
                }
            });
            db.SaveChanges();
        }
        var fake = new FakeHanoiCheckOrderCommandClient { DoiTrangThai = (_, _) => OrderCommandResult.Ok() };

        using var db2 = MoDb();
        var kq = await Svc(db2, fake).XacNhanAsync(id);
        Assert.True(kq.ThanhCong, kq.ThongBao);

        var don = await db2.DonHangBans.SingleAsync(d => d.Id == id);
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
