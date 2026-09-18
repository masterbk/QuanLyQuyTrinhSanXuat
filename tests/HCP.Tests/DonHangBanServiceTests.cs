using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services;
using HCP.Infrastructure.Services.BanHang;
using HCP.Infrastructure.Services.MaTuSinh;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>Fake không gọi mạng - các test ở đây chỉ dùng đơn nguồn nội bộ nên không bao giờ được gọi thật;
/// đơn nguồn HanoiCheck có test riêng ở <see cref="DonHangBanServiceHnCTests"/>.</summary>
public sealed class FakeHanoiCheckOrderCommandClient : IHanoiCheckOrderCommandClient
{
    public Func<string, ProcessOrderRequest, OrderCommandResult>? XuLyDon;
    public Func<string, string, OrderCommandResult>? DoiTrangThai;

    public Task<OrderCommandResult> XuLyDonAsync(string tenantId, string maDon, ProcessOrderRequest noiDung,
                                                 CancellationToken ct = default) =>
        Task.FromResult(XuLyDon?.Invoke(maDon, noiDung) ?? OrderCommandResult.ChuaCauHinhKq("stub"));

    public Task<OrderCommandResult> DoiTrangThaiAsync(string tenantId, string maDon, string trangThai, string? ghiChu,
                                                      CancellationToken ct = default) =>
        Task.FromResult(DoiTrangThai?.Invoke(maDon, trangThai) ?? OrderCommandResult.ChuaCauHinhKq("stub"));
}

/// <summary>
/// Kiểm chứng đơn hàng bán: lập đơn (mã tự sinh, tổng tiền), vòng đời trạng thái, xuất kho trừ tồn theo lô
/// (gợi ý FEFO cộng dồn các dòng, cho người dùng đổi lô), chặn phân bổ sai, huỷ khi đang giao trả hàng về lô.
/// </summary>
public class DonHangBanServiceTests
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

    private static DonHangBanService Svc(AppDbContext db) => new(db, new MaTuSinhService(db), new FakeHanoiCheckOrderCommandClient());

    /// <summary>Xác nhận đã giao bắt buộc có ảnh chứng minh.</summary>
    internal static readonly IReadOnlyList<AnhDauVao> AnhGiaoMau =
        new[] { new AnhDauVao("giao.jpg", "/uploads/coso-a/2026/09/giao.jpg") };

    /// <summary>BANH_MI tồn 2 lô: LOTP_A 5 (HSD 20/09, hết hạn trước), LOTP_B 10 (HSD 30/09).</summary>
    private void Seed()
    {
        using var db = MoDb();
        db.Products.Add(new Product { MaSanPham = "BANH_MI", TenSanPham = "Bánh mì", MaLoaiSp = "x",
                                      LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái" });
        db.Products.Add(new Product { MaSanPham = "BOT_MI", TenSanPham = "Bột mì", LoaiSanPham = LoaiSanPham.NguyenLieu });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
        db.KhachHangs.Add(new KhachHang { MaKhachHang = "KH01", TenKhachHang = "Cửa hàng A", DiaChi = "12 Láng Hạ" });
        db.Staff.Add(new Staff { MaNhanSu = "NS01", HoTen = "Người giao" });
        foreach (var (lo, sl, hsd) in new[] { ("LOTP_B", 10m, new DateOnly(2026, 9, 30)), ("LOTP_A", 5m, new DateOnly(2026, 9, 20)) })
        {
            db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = "BANH_MI", MaKho = "KHO01", MaLo = lo, SoLuong = sl, HanSuDung = hsd,
                Loai = LoaiGiaoDichKho.NhapThanhPham, ThoiGianUtc = DateTime.UtcNow
            });
        }
        db.SaveChanges();
    }

    private decimal TonLo(string lo)
    {
        using var db = MoDb();
        return db.KhoGiaoDichs.Where(g => g.MaSanPham == "BANH_MI" && g.MaLo == lo).Sum(g => (decimal?)g.SoLuong) ?? 0m;
    }

    private static DonHangBan Don(params (decimal SoLuong, decimal DonGia)[] dong) => new()
    {
        MaKhachHang = "KH01", MaKho = "KHO01", NgayDat = new DateOnly(2026, 9, 13),
        Dong = dong.Select(d => new DonHangBanDong { MaThanhPham = "BANH_MI", SoLuong = d.SoLuong, DonGia = d.DonGia }).ToList()
    };

    private async Task<int> TaoAsync(DonHangBan don)
    {
        using var db = MoDb();
        var kq = await Svc(db).TaoAsync(don);
        Assert.True(kq.ThanhCong, kq.ThongBao);
        return don.Id;
    }

    private async Task<KetQuaThaoTac> ChayAsync(Func<DonHangBanService, Task<KetQuaThaoTac>> f)
    {
        using var db = MoDb();
        return await f(Svc(db));
    }

    [Fact]
    public async Task Tao_Don_Tu_Sinh_Ma_Tinh_Tong_Tien_Lay_Dia_Chi_Khach_Chua_Dong_Kho()
    {
        Seed();
        var id = await TaoAsync(Don((3, 5000), (2, 4000)));
        var id2 = await TaoAsync(Don((1, 5000)));

        using var db = MoDb();
        var don = await db.DonHangBans.Include(d => d.Dong).SingleAsync(d => d.Id == id);
        Assert.Equal("DH-20260913-001", don.MaDonHang);
        Assert.Equal("DH-20260913-002", (await db.DonHangBans.SingleAsync(d => d.Id == id2)).MaDonHang);
        Assert.Equal(23000m, don.TongTien);
        Assert.Equal("12 Láng Hạ", don.DiaChiGiao);
        Assert.Equal(TrangThaiDonHangBan.ChoXacNhan, don.TrangThai);
        Assert.Equal(NguonDonHang.NoiBo, don.Nguon);
        Assert.Equal(5m, TonLo("LOTP_A"));      // lập đơn chưa trừ kho
    }

    [Fact]
    public async Task Tao_Don_Kiem_Tra_Du_Lieu()
    {
        Seed();
        using var db = MoDb();
        var svc = Svc(db);

        async Task<string> Loi(Action<DonHangBan> sua)
        {
            var d = Don((1, 1000));
            sua(d);
            var kq = await svc.TaoAsync(d);
            Assert.False(kq.ThanhCong);
            return kq.ThongBao;
        }

        Assert.Contains("khách hàng", await Loi(d => d.MaKhachHang = "KH_LA"));
        Assert.Contains("kho", await Loi(d => d.MaKho = ""));
        Assert.Contains("ít nhất một dòng", await Loi(d => d.Dong.Clear()));
        Assert.Contains("không phải thành phẩm", await Loi(d => d.Dong[0].MaThanhPham = "BOT_MI"));
        Assert.Contains("lớn hơn 0", await Loi(d => d.Dong[0].SoLuong = 0));
        Assert.Contains("không được âm", await Loi(d => d.Dong[0].DonGia = -1));
        Assert.Contains("Ngày giao", await Loi(d => d.NgayGiao = new DateOnly(2026, 9, 1)));
        Assert.Contains("NS_LA", await Loi(d => d.MaNguoiGiao = "NS_LA"));
        Assert.False(await db.DonHangBans.AnyAsync());
    }

    [Fact]
    public async Task Goi_Y_Xuat_Kho_FEFO_Cong_Don_Cac_Dong_Cung_Thanh_Pham()
    {
        Seed();
        var id = await TaoAsync(Don((4, 5000), (3, 5000)));

        using var db = MoDb();
        var goiY = await Svc(db).GoiYXuatKhoAsync(id);

        Assert.Equal(2, goiY.Count);
        // Dòng 1 lấy 4 từ lô hết hạn trước; dòng 2 lấy nốt 1 ở LOTP_A rồi 2 ở LOTP_B.
        Assert.Equal(new[] { ("LOTP_A", 4m), ("LOTP_B", 0m) }, goiY[0].Lo.Select(x => (x.MaLo, x.GoiY)));
        Assert.Equal(new[] { ("LOTP_A", 1m), ("LOTP_B", 2m) }, goiY[1].Lo.Select(x => (x.MaLo, x.GoiY)));
    }

    [Fact]
    public async Task Xuat_Kho_Theo_Goi_Y_Tru_Ton_Luu_Lo_Va_Chuyen_Dang_Giao()
    {
        Seed();
        var id = await TaoAsync(Don((4, 5000), (3, 5000)));

        var kq = await ChayAsync(s => s.XuatKhoAsync(id, null, "NS01"));
        Assert.True(kq.ThanhCong, kq.ThongBao);

        Assert.Equal(0m, TonLo("LOTP_A"));
        Assert.Equal(8m, TonLo("LOTP_B"));

        using var db = MoDb();
        var don = await db.DonHangBans.Include(d => d.Dong).ThenInclude(l => l.XuatLo).SingleAsync();
        Assert.Equal(TrangThaiDonHangBan.DangGiao, don.TrangThai);
        Assert.Equal("NS01", don.MaNguoiGiao);
        Assert.NotNull(don.ThoiGianXuatKhoUtc);
        Assert.Equal(3, don.Dong.SelectMany(l => l.XuatLo).Count());
        Assert.Equal(3, await db.KhoGiaoDichs.CountAsync(g => g.ChungTu == don.MaDonHang && g.Loai == LoaiGiaoDichKho.XuatBan));
    }

    [Fact]
    public async Task Xuat_Kho_Theo_Lo_Nguoi_Dung_Chon()
    {
        Seed();
        var id = await TaoAsync(Don((3, 5000)));
        int dongId;
        using (var db = MoDb()) dongId = (await db.DonHangBanDongs.SingleAsync()).Id;

        // Khách yêu cầu lô mới hơn: lấy cả 3 từ LOTP_B thay vì LOTP_A.
        var kq = await ChayAsync(s => s.XuatKhoAsync(id, new[] { new PhanBoLoRequest(dongId, "LOTP_B", 3) }, null));
        Assert.True(kq.ThanhCong, kq.ThongBao);
        Assert.Equal(5m, TonLo("LOTP_A"));
        Assert.Equal(7m, TonLo("LOTP_B"));
    }

    [Fact]
    public async Task Xuat_Kho_Chan_Phan_Bo_Sai_Va_Khong_Tru_Gi()
    {
        Seed();
        var id = await TaoAsync(Don((6, 5000)));
        int dongId;
        using (var db = MoDb()) dongId = (await db.DonHangBanDongs.SingleAsync()).Id;

        Assert.Contains("cần đúng", (await ChayAsync(s => s.XuatKhoAsync(id, new[] { new PhanBoLoRequest(dongId, "LOTP_B", 5) }, null))).ThongBao);
        Assert.Contains("chỉ còn", (await ChayAsync(s => s.XuatKhoAsync(id, new[] { new PhanBoLoRequest(dongId, "LOTP_A", 6) }, null))).ThongBao);
        Assert.Contains("chỉ còn", (await ChayAsync(s => s.XuatKhoAsync(id, new[] { new PhanBoLoRequest(dongId, "LO_LA", 6) }, null))).ThongBao);
        Assert.Contains("không thuộc đơn", (await ChayAsync(s => s.XuatKhoAsync(id, new[] { new PhanBoLoRequest(9999, "LOTP_B", 6) }, null))).ThongBao);

        // Đặt nhiều hơn tổng tồn -> gợi ý tự động cũng không đủ.
        var idLon = await TaoAsync(Don((20, 5000)));
        Assert.Contains("Không đủ tồn", (await ChayAsync(s => s.XuatKhoAsync(idLon, null, null))).ThongBao);

        Assert.Equal(5m, TonLo("LOTP_A"));
        Assert.Equal(10m, TonLo("LOTP_B"));
        using var db2 = MoDb();
        Assert.All(await db2.DonHangBans.ToListAsync(), d => Assert.Equal(TrangThaiDonHangBan.ChoXacNhan, d.TrangThai));
    }

    [Fact]
    public async Task Huy_Khi_Dang_Giao_Tra_Hang_Ve_Dung_Lo()
    {
        Seed();
        var id = await TaoAsync(Don((7, 5000)));
        Assert.True((await ChayAsync(s => s.XuatKhoAsync(id, null, null))).ThanhCong);
        Assert.Equal(0m, TonLo("LOTP_A"));
        Assert.Equal(8m, TonLo("LOTP_B"));

        Assert.False((await ChayAsync(s => s.HuyAsync(id, "  "))).ThanhCong);     // bắt buộc lý do
        var kq = await ChayAsync(s => s.HuyAsync(id, "Khách đổi ý"));
        Assert.True(kq.ThanhCong, kq.ThongBao);
        Assert.Contains("trả hàng", kq.ThongBao);

        Assert.Equal(5m, TonLo("LOTP_A"));
        Assert.Equal(10m, TonLo("LOTP_B"));

        using var db = MoDb();
        var dao = await db.KhoGiaoDichs.Where(g => g.Loai == LoaiGiaoDichKho.HoanTacXuatBan).ToListAsync();
        Assert.Equal(2, dao.Count);
        Assert.Equal(new DateOnly(2026, 9, 20), dao.Single(g => g.MaLo == "LOTP_A").HanSuDung);   // giữ HSD để FEFO đúng
        var don = await db.DonHangBans.SingleAsync();
        Assert.Equal(TrangThaiDonHangBan.DaHuy, don.TrangThai);
        Assert.Equal("Khách đổi ý", don.LyDoHuy);

        // Đơn đã xuất kho thì không xoá được (giữ lịch sử kho), không huỷ lần hai.
        Assert.False((await ChayAsync(s => s.XoaAsync(id))).ThanhCong);
        Assert.False((await ChayAsync(s => s.HuyAsync(id, "lần hai"))).ThanhCong);
        Assert.Equal(5m, TonLo("LOTP_A"));
    }

    [Fact]
    public async Task Vong_Doi_Trang_Thai_Dung_Thu_Tu()
    {
        Seed();
        var id = await TaoAsync(Don((2, 5000)));

        Assert.False((await ChayAsync(s => s.HoanTatGiaoAsync(id, AnhGiaoMau))).ThanhCong);   // chưa xuất kho
        Assert.True((await ChayAsync(s => s.XacNhanAsync(id))).ThanhCong);
        Assert.False((await ChayAsync(s => s.XacNhanAsync(id))).ThanhCong);      // không xác nhận hai lần
        Assert.True((await ChayAsync(s => s.XuatKhoAsync(id, null, null))).ThanhCong);
        Assert.False((await ChayAsync(s => s.XuatKhoAsync(id, null, null))).ThanhCong);   // không xuất kho hai lần

        var sua = Don((9, 1)); sua.Id = id;
        Assert.Contains("chưa xuất kho", (await ChayAsync(s => s.CapNhatAsync(sua))).ThongBao);

        Assert.True((await ChayAsync(s => s.HoanTatGiaoAsync(id, AnhGiaoMau))).ThanhCong);
        Assert.Contains("đã giao", (await ChayAsync(s => s.HuyAsync(id, "thử"))).ThongBao);
        Assert.Equal(3m, TonLo("LOTP_A"));                                          // chỉ trừ đúng 1 lần

        using var db = MoDb();
        var don = await db.DonHangBans.SingleAsync();
        Assert.Equal(TrangThaiDonHangBan.DaGiao, don.TrangThai);
        Assert.NotNull(don.ThoiGianGiaoUtc);
    }

    [Fact]
    public async Task Sua_Va_Xoa_Don_Chua_Xuat_Kho()
    {
        Seed();
        var id = await TaoAsync(Don((2, 5000), (1, 5000)));

        var ban = Don((5, 6000)); ban.Id = id; ban.GhiChu = "  giao sáng  "; ban.MaNguoiGiao = "NS01";
        var kq = await ChayAsync(s => s.CapNhatAsync(ban));
        Assert.True(kq.ThanhCong, kq.ThongBao);

        using (var db = MoDb())
        {
            var don = await db.DonHangBans.Include(d => d.Dong).SingleAsync();
            Assert.Equal("DH-20260913-001", don.MaDonHang);      // mã không đổi
            Assert.Equal(30000m, Assert.Single(don.Dong).ThanhTien);
            Assert.Equal("giao sáng", don.GhiChu);
            Assert.Equal("NS01", don.MaNguoiGiao);
        }

        Assert.True((await ChayAsync(s => s.XoaAsync(id))).ThanhCong);
        using var db2 = MoDb();
        Assert.False(await db2.DonHangBans.AnyAsync());
        Assert.False(await db2.DonHangBanDongs.AnyAsync());
    }
}
