using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Đặc tả HanoiCheck v2.7.1 (Phần III mục 8): dữ liệu đang dùng cho đơn "Đang giao" / "Đã giao hàng" mà nhà
/// trường chưa xác nhận nhận hàng bị khoá một phần. App phải chặn ngay lúc lưu, không để HnC trả 422 sau đó.
/// </summary>
public class KhoaDonDangGiaoTests
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

    private sealed class NoOpOutbox : ISyncOutboxWriter
    {
        public Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> DangBatAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task<string?> GuiAsync(object banGhi, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    /// <summary>Danh mục nền + 1 đơn HanoiCheck đang giao, xuất lô LO-01 của TP-BANH từ kho KHO01.</summary>
    private void Seed(TrangThaiDonHangBan trangThai = TrangThaiDonHangBan.DangGiao,
                      string? trangThaiHnC = "DANG_GIAO")
    {
        using var db = MoDb();

        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho chính", DiaChi = "HN" });
        db.ProductionSteps.Add(new ProductionStep { MaKhau = "SO_CHE", TenKhau = "Sơ chế" });
        db.ProductionProcesses.Add(new ProductionProcess
        {
            MaQuyTrinh = "QT-TP", TenQuyTrinh = "Làm bánh", LoaiApDung = LoaiQuyTrinh.ThucPham,
            DanhSachKhau = { new ProcessStepLine { MaKhau = "SO_CHE", ThuTu = 1 } }
        });
        db.ProductionProcesses.Add(new ProductionProcess
        {
            MaQuyTrinh = "QT-MON", TenQuyTrinh = "Chế biến món", LoaiApDung = LoaiQuyTrinh.MonAn,
            DanhSachKhau = { new ProcessStepLine { MaKhau = "SO_CHE", ThuTu = 1 } }
        });
        db.NhomTuoiHnCs.Add(new NhomTuoiHnC { Id = 1, Ten = "Mầm non" });

        db.StandardFoodCategories.Add(new StandardFoodCategory { Code = "DM-KG", Name = "Nhóm Kg", MeasureName = "Kg" });
        db.StandardFoodCategories.Add(new StandardFoodCategory { Code = "DM-LIT", Name = "Nhóm Lít", MeasureName = "Lít" });
        db.DonViTinhHnCs.Add(new DonViTinhHnC { Id = 1, Ten = "Kg" });
        db.DonViTinhHnCs.Add(new DonViTinhHnC { Id = 2, Ten = "Lít" });

        db.Products.Add(new Product { MaSanPham = "TP-BANH", TenSanPham = "Bánh mì", MaLoaiSp = "DM-KG",
                                      LoaiSanPham = LoaiSanPham.ThanhPham, MaQuyTrinh = "QT-TP" });
        db.Products.Add(new Product { MaSanPham = "TP-SUA", TenSanPham = "Sữa", MaLoaiSp = "DM-KG",
                                      LoaiSanPham = LoaiSanPham.ThanhPham, MaQuyTrinh = "QT-TP" });

        db.Batches.Add(new Batch
        {
            MaLo = "LO-01", MaSanPham = "TP-BANH", TenLo = "Lô bánh", NgayNhap = new DateOnly(2026, 11, 1),
            NgaySanXuat = new DateOnly(2026, 11, 1), HanSuDung = new DateOnly(2026, 11, 20), DiaChiThuMua = "Chợ HN",
            DanhSachKho = { new BatchWarehouse { MaKho = "KHO01" } },
            DanhSachKhau = { new BatchStep { MaBuocSx = "B1", MaKhau = "SO_CHE", ThuTu = 1 } },
            DanhSachFile = { new BatchFile { MaFile = "A1", TenFile = "a.jpg",
                                             DuongDan = "https://app.vn/uploads/a.jpg", Loai = "HINH_ANH" } }
        });

        db.Dishes.Add(new Dish
        {
            MaMonAn = "MON-001", TenMonAn = "Bánh sữa", NhomTuoiId = 1, MaQuyTrinh = "QT-MON",
            DanhSachNguyenLieu = { new DishIngredient { MaNguyenLieu = "TP-BANH", DinhLuong = 0.05m } },
            DanhSachKhau = { new DishStep { MaKhau = "SO_CHE", ThuTu = 1 } },
            DanhSachFile = { new DishFile { MaFile = "F1", TenFile = "F1",
                                            DuongDan = "https://app.vn/uploads/mon.jpg", Loai = "HINH_ANH" } }
        });

        db.KhachHangs.Add(new KhachHang { MaKhachHang = "KH01", TenKhachHang = "Trường A" });
        db.DonHangBans.Add(new DonHangBan
        {
            MaDonHang = "DH-TEST-001", MaKhachHang = "KH01", MaKho = "KHO01",
            NgayDat = new DateOnly(2026, 11, 2), TrangThai = trangThai,
            Nguon = NguonDonHang.HanoiCheck, MaDonHnC = "HNC-001", TrangThaiHnC = trangThaiHnC,
            Dong =
            {
                new DonHangBanDong
                {
                    MaThanhPham = "TP-BANH", SoLuong = 10, DonGia = 1000,
                    XuatLo = { new DonHangBanXuatLo { MaLo = "LO-01", SoLuong = 10 } }
                },
                new DonHangBanDong
                {
                    MaThanhPham = "MON-001", LaMonAn = true, SoLuong = 20, DonGia = 2000,
                    XuatLo = { new DonHangBanXuatLo { MaLo = "LO-01", MaNguyenLieu = "TP-BANH", SoLuong = 1 } }
                }
            }
        });

        db.SaveChanges();
    }

    // ===== DV7 a) Thực phẩm: không đổi đơn vị tính =====

    [Fact]
    public async Task Thuc_Pham_Trong_Don_Dang_Giao_Khong_Doi_Duoc_Don_Vi_Tinh()
    {
        Seed();
        using var db = MoDb();
        var svc = new ThucPhamService(db, new NoOpOutbox());

        var sp = (await svc.LayDeSuaAsync((await svc.LayTatCaAsync()).First(p => p.MaSanPham == "TP-BANH").Id))!;
        sp.DonViTinhHnCId = 2;   // trước đó chưa khai -> HnC đang dùng đơn vị mặc định của danh mục
        var kq = await svc.CapNhatAsync(sp);

        Assert.False(kq.ThanhCong, kq.ThongBao);
        Assert.Contains("DH-TEST-001", kq.ThongBao);
        Assert.Contains("đổi đơn vị tính", kq.ThongBao);
    }

    [Fact]
    public async Task Thuc_Pham_Trong_Don_Dang_Giao_Van_Sua_Duoc_Ten_Va_Mo_Ta()
    {
        Seed();
        using var db = MoDb();
        var svc = new ThucPhamService(db, new NoOpOutbox());

        var sp = (await svc.LayDeSuaAsync((await svc.LayTatCaAsync()).First(p => p.MaSanPham == "TP-BANH").Id))!;
        sp.TenSanPham = "Bánh mì bơ";
        sp.MoTa = "Đổi mô tả";
        var kq = await svc.CapNhatAsync(sp);

        Assert.True(kq.ThanhCong, kq.ThongBao);
    }

    [Fact]
    public async Task Doi_Danh_Muc_Lam_Doi_Don_Vi_Mac_Dinh_Cung_Bi_Chan()
    {
        Seed();
        using var db = MoDb();
        var svc = new ThucPhamService(db, new NoOpOutbox());

        var sp = (await svc.LayDeSuaAsync((await svc.LayTatCaAsync()).First(p => p.MaSanPham == "TP-BANH").Id))!;
        sp.MaLoaiSp = "DM-LIT";   // Kg -> Lít
        var kq = await svc.CapNhatAsync(sp);

        Assert.False(kq.ThanhCong, kq.ThongBao);
        Assert.Contains("đổi đơn vị tính", kq.ThongBao);
    }

    // ===== DV8 a) Lô: không đổi ma_san_pham, không bỏ kho đã xuất hàng =====

    [Fact]
    public async Task Lo_Da_Xuat_Cho_Don_Khong_Doi_Duoc_Thuc_Pham()
    {
        Seed();
        using var db = MoDb();
        var svc = new LoSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db));

        var lo = (await svc.LayDeSuaAsync((await svc.LayTatCaAsync()).Single().Id))!;
        lo.MaSanPham = "TP-SUA";
        var kq = await svc.CapNhatAsync(lo);

        Assert.False(kq.ThanhCong, kq.ThongBao);
        Assert.Contains("đổi thực phẩm", kq.ThongBao);
    }

    [Fact]
    public async Task Lo_Da_Xuat_Cho_Don_Khong_Bo_Duoc_Kho_Da_Xuat_Hang()
    {
        Seed();
        using (var dbKho = MoDb())
        {
            dbKho.Warehouses.Add(new Warehouse { MaKho = "KHO02", TenKho = "Kho 2", DiaChi = "HN" });
            dbKho.SaveChanges();
        }

        using var db = MoDb();
        var svc = new LoSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db));

        var lo = (await svc.LayDeSuaAsync((await svc.LayTatCaAsync()).Single().Id))!;
        lo.DanhSachKho.Clear();
        lo.DanhSachKho.Add(new BatchWarehouse { MaKho = "KHO02" });
        var kq = await svc.CapNhatAsync(lo);

        Assert.False(kq.ThanhCong, kq.ThongBao);
        Assert.Contains("KHO01", kq.ThongBao);
    }

    [Fact]
    public async Task Lo_Da_Xuat_Cho_Don_Van_Sua_Duoc_Ngay_Va_Ghi_Chu()
    {
        Seed();
        using var db = MoDb();
        var svc = new LoSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db));

        var lo = (await svc.LayDeSuaAsync((await svc.LayTatCaAsync()).Single().Id))!;
        lo.HanSuDung = new DateOnly(2026, 11, 25);
        lo.GhiChu = "Sửa ghi chú";
        var kq = await svc.CapNhatAsync(lo);

        Assert.True(kq.ThanhCong, kq.ThongBao);
    }

    [Fact]
    public async Task Lo_Dang_La_Nguon_Hang_Thi_Khong_Xoa_Duoc()
    {
        Seed();
        using var db = MoDb();
        var svc = new LoSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db));

        var kq = await svc.XoaAsync((await svc.LayTatCaAsync()).Single().Id);

        Assert.False(kq.ThanhCong, kq.ThongBao);
        Assert.Contains("DH-TEST-001", kq.ThongBao);
    }

    // ===== DV9 a) Món ăn: không đổi công thức =====

    [Fact]
    public async Task Mon_Trong_Don_Dang_Giao_Khong_Doi_Duoc_Cong_Thuc()
    {
        Seed();
        using var db = MoDb();
        var svc = new MonAnService(db, new NoOpOutbox());

        var mon = (await svc.LayDeSuaAsync((await svc.LayTatCaAsync()).Single().Id))!;
        mon.DanhSachNguyenLieu[0].DinhLuong = 0.09m;
        var kq = await svc.CapNhatAsync(mon);

        Assert.False(kq.ThanhCong, kq.ThongBao);
        Assert.Contains("đổi định lượng", kq.ThongBao);
    }

    [Fact]
    public async Task Mon_Trong_Don_Dang_Giao_Van_Sua_Duoc_Ten()
    {
        Seed();
        using var db = MoDb();
        var svc = new MonAnService(db, new NoOpOutbox());

        var mon = (await svc.LayDeSuaAsync((await svc.LayTatCaAsync()).Single().Id))!;
        mon.TenMonAn = "Bánh sữa mới";
        var kq = await svc.CapNhatAsync(mon);

        Assert.True(kq.ThanhCong, kq.ThongBao);
    }

    [Fact]
    public async Task Mon_Trong_Don_Dang_Giao_Khong_Xoa_Duoc()
    {
        Seed();
        using var db = MoDb();
        var svc = new MonAnService(db, new NoOpOutbox());

        var kq = await svc.XoaAsync((await svc.LayTatCaAsync()).Single().Id);

        Assert.False(kq.ThanhCong, kq.ThongBao);
        Assert.Contains("DH-TEST-001", kq.ThongBao);
    }

    // ===== Kho: không xoá kho đang dùng cho đơn =====

    [Fact]
    public async Task Kho_Dang_Dung_Cho_Don_Khong_Xoa_Duoc()
    {
        Seed();
        using var db = MoDb();
        var svc = new KhoService(db, new NoOpOutbox());

        var kq = await svc.XoaAsync((await svc.LayTatCaAsync()).Single(k => k.MaKho == "KHO01").Id);

        Assert.False(kq.ThanhCong, kq.ThongBao);
        Assert.Contains("DH-TEST-001", kq.ThongBao);
    }

    // ===== Khoá mở khi trường đã xác nhận / đơn chưa xuất kho =====

    [Fact]
    public async Task Truong_Xac_Nhan_Giao_Thanh_Cong_Thi_Mo_Khoa()
    {
        Seed(TrangThaiDonHangBan.DaGiao, "GIAO_HANG_THANH_CONG");
        using var db = MoDb();
        var svc = new LoSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db));

        var lo = (await svc.LayDeSuaAsync((await svc.LayTatCaAsync()).Single().Id))!;
        lo.MaSanPham = "TP-SUA";
        var kq = await svc.CapNhatAsync(lo);

        Assert.True(kq.ThanhCong, kq.ThongBao);
    }

    [Fact]
    public async Task Don_Chua_Xuat_Kho_Thi_Khong_Khoa()
    {
        Seed(TrangThaiDonHangBan.DaXacNhan, "DANG_CHUAN_BI");
        using var db = MoDb();
        var svc = new MonAnService(db, new NoOpOutbox());

        var mon = (await svc.LayDeSuaAsync((await svc.LayTatCaAsync()).Single().Id))!;
        mon.DanhSachNguyenLieu[0].DinhLuong = 0.09m;
        var kq = await svc.CapNhatAsync(mon);

        Assert.True(kq.ThanhCong, kq.ThongBao);
    }
}
