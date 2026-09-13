using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BanHang;
using HCP.Infrastructure.Services.MaTuSinh;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng việc biến đơn HanoiCheck (DonHangNhan) thành Đơn hàng bán: tự khớp/tạo khách trường học,
/// cập nhật khi còn chờ xác nhận (giữ đơn giá), chỉ gắn cờ sau khi xác nhận, tự huỷ khi HnC huỷ mà chưa xuất kho,
/// gợi ý lô ưu tiên lô HnC đã phân bổ.
/// </summary>
public class DonHangHnCServiceTests
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

    private static DonHangHnCService Svc(AppDbContext db) => new(db, new MaTuSinhService(db));
    private static DonHangBanService SvcBan(AppDbContext db) => new(db, new MaTuSinhService(db));

    /// <summary>BANH_MI tồn LO_A 10 (HSD 20/09, hết hạn trước) và LO_B 10 (HSD 30/09); 2 kho; người giao VC-03.</summary>
    private void Seed()
    {
        using var db = MoDb();
        db.Products.Add(new Product { MaSanPham = "BANH_MI", TenSanPham = "Bánh mì", MaLoaiSp = "x", LoaiSanPham = LoaiSanPham.ThanhPham });
        db.Products.Add(new Product { MaSanPham = "BOT_MI", TenSanPham = "Bột mì", LoaiSanPham = LoaiSanPham.NguyenLieu });
        db.Warehouses.Add(new Warehouse { MaKho = "K-01", TenKho = "Kho 1", DiaChi = "HN" });
        db.Warehouses.Add(new Warehouse { MaKho = "K-02", TenKho = "Kho 2", DiaChi = "HN" });
        db.Staff.Add(new Staff { MaNhanSu = "VC-03", HoTen = "Người giao" });
        foreach (var (lo, hsd) in new[] { ("LO_A", new DateOnly(2026, 9, 20)), ("LO_B", new DateOnly(2026, 9, 30)) })
        {
            db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = "BANH_MI", MaKho = "K-02", MaLo = lo, SoLuong = 10, HanSuDung = hsd,
                Loai = LoaiGiaoDichKho.NhapThanhPham, ThoiGianUtc = DateTime.UtcNow
            });
        }
        db.SaveChanges();
    }

    /// <summary>Ghi (hoặc thay) một đơn HanoiCheck như job kéo về.</summary>
    private void NhanDon(string ma, string truong, string trangThai, DateOnly? ngayGiao,
                         params (string MaSp, decimal SoLuong, string? MaLo, string? MaKho)[] dong)
    {
        using var db = MoDb();
        var cu = db.DonHangNhans.Include(d => d.Dong).ThenInclude(l => l.PhanBo).FirstOrDefault(d => d.MaDonHang == ma);
        if (cu is not null) db.DonHangNhans.Remove(cu);
        db.DonHangNhans.Add(new DonHangNhan
        {
            TenantId = CoSo, MaDonHang = ma, TenTruong = truong, TrangThai = trangThai, NgayGiao = ngayGiao,
            MaNguoiGiao = "VC-03", NgayTaoTrenHnC = new DateTime(2026, 9, 13, 10, 0, 0), DaLayChiTiet = true,
            LanDongBoUtc = DateTime.UtcNow,
            Dong = dong.Select(d => new DonHangNhanDong
            {
                TenantId = CoSo, MaSanPham = d.MaSp, TenSanPham = d.MaSp, SoLuong = d.SoLuong,
                PhanBo = d.MaLo is null ? new() : new() { new DonHangNhanPhanBo { TenantId = CoSo, MaLo = d.MaLo, MaKho = d.MaKho, SoLuong = d.SoLuong } }
            }).ToList()
        });
        db.SaveChanges();
    }

    private async Task<int> DongBoAsync()
    {
        using var db = MoDb();
        return await Svc(db).DongBoVaoDonHangBanAsync();
    }

    private DonHangBan LayDon(string maHnC)
    {
        using var db = MoDb();
        return db.DonHangBans.Include(d => d.Dong).Single(d => d.MaDonHnC == maHnC);
    }

    [Fact]
    public async Task Tao_Don_Ban_Tu_Don_HnC_Va_Tu_Tao_Khach_Truong_Hoc()
    {
        Seed();
        NhanDon("HNC-1", "MN Hoa Sen", "DANG_CHUAN_BI", new DateOnly(2026, 9, 20),
                ("BANH_MI", 8, "LO_B", "K-02"), ("MON_LA", 5, null, null));

        Assert.Equal(1, await DongBoAsync());

        var don = LayDon("HNC-1");
        Assert.Equal(NguonDonHang.HanoiCheck, don.Nguon);
        Assert.Equal("DH-20260913-001", don.MaDonHang);
        Assert.Equal(TrangThaiDonHangBan.ChoXacNhan, don.TrangThai);   // app tự quản lý trạng thái
        Assert.Equal("DANG_CHUAN_BI", don.TrangThaiHnC);               // trạng thái HnC chỉ để đối chiếu
        Assert.Equal("K-02", don.MaKho);                               // kho HnC đã phân bổ
        Assert.Equal(new DateOnly(2026, 9, 20), don.NgayGiao);
        Assert.Equal("VC-03", don.MaNguoiGiao);
        var dong = Assert.Single(don.Dong);                            // dòng không khớp thành phẩm bị bỏ
        Assert.Equal(("BANH_MI", 8m, 0m), (dong.MaThanhPham, dong.SoLuong, dong.DonGia));
        Assert.Contains("MON_LA", don.GhiChu);

        using var db = MoDb();
        var khach = await db.KhachHangs.SingleAsync();
        Assert.Equal(("KH-0001", "MN Hoa Sen", LoaiKhachHang.TruongHoc), (khach.MaKhachHang, khach.TenKhachHang, khach.Loai));
        Assert.Equal(khach.MaKhachHang, don.MaKhachHang);
    }

    [Fact]
    public async Task Dong_Bo_Lai_Khong_Nhan_Ban_Va_Khop_Khach_Truong_Theo_Ten()
    {
        Seed();
        using (var db = MoDb())
        {
            db.KhachHangs.Add(new KhachHang { MaKhachHang = "KH01", TenKhachHang = "MN Hoa Sen", Loai = LoaiKhachHang.TruongHoc, DiaChi = "5 Hoa Sen" });
            await db.SaveChangesAsync();
        }
        NhanDon("HNC-1", "MN Hoa Sen", "CHO_XAC_NHAN", new DateOnly(2026, 9, 20), ("BANH_MI", 8, null, null));
        NhanDon("HNC-2", "MN Hoa Sen", "CHO_XAC_NHAN", new DateOnly(2026, 9, 21), ("BANH_MI", 3, "LO_A", "K-02"));

        Assert.Equal(2, await DongBoAsync());
        Assert.Equal(0, await DongBoAsync());                          // chạy lại không đổi gì

        using var db2 = MoDb();
        Assert.Equal(2, await db2.DonHangBans.CountAsync());
        Assert.Single(await db2.KhachHangs.ToListAsync());             // không tạo trùng khách
        Assert.All(await db2.DonHangBans.ToListAsync(), d =>
        {
            Assert.Equal("KH01", d.MaKhachHang);
            Assert.Equal("5 Hoa Sen", d.DiaChiGiao);                   // HnC không có địa chỉ -> lấy của khách
        });
    }

    [Fact]
    public async Task HnC_Doi_Don_Cap_Nhat_Khi_Cho_Xac_Nhan_Giu_Don_Gia_Da_Xac_Nhan_Chi_Gan_Co()
    {
        Seed();
        NhanDon("HNC-1", "MN Hoa Sen", "CHO_XAC_NHAN", new DateOnly(2026, 9, 20), ("BANH_MI", 8, null, null));
        await DongBoAsync();

        // NCC nhập đơn giá.
        using (var db = MoDb())
        {
            (await db.DonHangBanDongs.SingleAsync()).DonGia = 5000;
            await db.SaveChangesAsync();
        }

        // HnC đổi số lượng khi app còn Chờ xác nhận -> cập nhật, giữ đơn giá.
        NhanDon("HNC-1", "MN Hoa Sen", "CHO_XAC_NHAN", new DateOnly(2026, 9, 22), ("BANH_MI", 9, null, null));
        Assert.Equal(1, await DongBoAsync());
        var don = LayDon("HNC-1");
        Assert.Equal((9m, 5000m), (don.Dong.Single().SoLuong, don.Dong.Single().DonGia));
        Assert.Equal(new DateOnly(2026, 9, 22), don.NgayGiao);
        Assert.False(don.HnCCoThayDoi);

        using (var db = MoDb()) Assert.True((await SvcBan(db).XacNhanAsync(don.Id)).ThanhCong);

        // Đã xác nhận: HnC đổi tiếp -> KHÔNG ghi đè, chỉ gắn cờ.
        NhanDon("HNC-1", "MN Hoa Sen", "CHO_XAC_NHAN", new DateOnly(2026, 9, 22), ("BANH_MI", 12, null, null));
        await DongBoAsync();
        don = LayDon("HNC-1");
        Assert.Equal(9m, don.Dong.Single().SoLuong);
        Assert.True(don.HnCCoThayDoi);

        // NCC xem lại và lưu đơn -> tắt cờ.
        using (var db = MoDb())
        {
            var ban = new DonHangBan
            {
                Id = don.Id, MaKhachHang = don.MaKhachHang, MaKho = don.MaKho, NgayDat = don.NgayDat, NgayGiao = don.NgayGiao,
                Dong = { new DonHangBanDong { MaThanhPham = "BANH_MI", SoLuong = 12, DonGia = 5000 } }
            };
            Assert.True((await SvcBan(db).CapNhatAsync(ban)).ThanhCong);
        }
        Assert.False(LayDon("HNC-1").HnCCoThayDoi);
    }

    [Fact]
    public async Task HnC_Huy_Thi_Tu_Huy_Neu_Chua_Xuat_Kho_Da_Xuat_Thi_Chi_Gan_Co()
    {
        Seed();
        NhanDon("HNC-A", "MN Hoa Sen", "CHO_XAC_NHAN", new DateOnly(2026, 9, 20), ("BANH_MI", 2, "LO_A", "K-02"));
        NhanDon("HNC-B", "MN Hoa Sen", "CHO_XAC_NHAN", new DateOnly(2026, 9, 20), ("BANH_MI", 3, "LO_A", "K-02"));
        await DongBoAsync();

        using (var db = MoDb()) Assert.True((await SvcBan(db).XuatKhoAsync(LayDon("HNC-B").Id, null, null)).ThanhCong);

        NhanDon("HNC-A", "MN Hoa Sen", "HUY", new DateOnly(2026, 9, 20), ("BANH_MI", 2, "LO_A", "K-02"));
        NhanDon("HNC-B", "MN Hoa Sen", "TU_CHOI", new DateOnly(2026, 9, 20), ("BANH_MI", 3, "LO_A", "K-02"));
        await DongBoAsync();

        var a = LayDon("HNC-A");
        Assert.Equal(TrangThaiDonHangBan.DaHuy, a.TrangThai);
        Assert.Contains("HanoiCheck", a.LyDoHuy);

        var b = LayDon("HNC-B");
        Assert.Equal(TrangThaiDonHangBan.DangGiao, b.TrangThai);       // không tự trả hàng về kho
        Assert.True(b.HnCCoThayDoi);
        Assert.Equal("TU_CHOI", b.TrangThaiHnC);

        // HnC "hồi sinh" đơn đã huỷ trong app -> app giữ nguyên Đã huỷ.
        NhanDon("HNC-A", "MN Hoa Sen", "CHO_XAC_NHAN", new DateOnly(2026, 9, 20), ("BANH_MI", 2, "LO_A", "K-02"));
        await DongBoAsync();
        Assert.Equal(TrangThaiDonHangBan.DaHuy, LayDon("HNC-A").TrangThai);
    }

    [Fact]
    public async Task Khong_Tao_Don_Khi_Khong_Dong_Nao_Khop_Hoac_HnC_Da_Huy()
    {
        Seed();
        NhanDon("HNC-1", "MN Hoa Sen", "CHO_XAC_NHAN", null, ("MON_LA", 5, null, null), ("BOT_MI", 1, null, null));
        NhanDon("HNC-2", "MN Cúc", "HUY", null, ("BANH_MI", 5, null, null));

        Assert.Equal(0, await DongBoAsync());
        using var db = MoDb();
        Assert.False(await db.DonHangBans.AnyAsync());
        Assert.False(await db.KhachHangs.AnyAsync());
    }

    [Fact]
    public async Task Goi_Y_Lo_Uu_Tien_Lo_HanoiCheck_Da_Phan_Bo_Va_Khong_Xoa_Duoc_Don_HnC()
    {
        Seed();
        // HnC phân bổ lô LO_B dù LO_A hết hạn trước.
        NhanDon("HNC-1", "MN Hoa Sen", "DANG_CHUAN_BI", new DateOnly(2026, 9, 20), ("BANH_MI", 8, "LO_B", "K-02"));
        await DongBoAsync();
        var id = LayDon("HNC-1").Id;

        using var db = MoDb();
        var goiY = Assert.Single(await SvcBan(db).GoiYXuatKhoAsync(id));
        Assert.Equal(("LO_B", 8m), (goiY.Lo[0].MaLo, goiY.Lo[0].GoiY));
        Assert.Equal(("LO_A", 0m), (goiY.Lo[1].MaLo, goiY.Lo[1].GoiY));

        var xoa = await SvcBan(db).XoaAsync(id);
        Assert.False(xoa.ThanhCong);
        Assert.Contains("HanoiCheck", xoa.ThongBao);
    }
}
