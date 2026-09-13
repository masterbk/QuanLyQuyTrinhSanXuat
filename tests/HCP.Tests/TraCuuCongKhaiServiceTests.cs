using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.TraCuu;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Mã QR tra cứu: đơn HanoiCheck dùng traceability_url; đơn nội bộ và lô dùng trang công khai theo mã ngẫu nhiên,
/// tra được khi KHÔNG đăng nhập (không có tenant) nhưng không lộ dữ liệu khi mã sai.
/// </summary>
public class TraCuuCongKhaiServiceTests
{
    private const string CoSo = "coso-a";
    private const string LinkHnC = "https://tracuu.hanoicheck.com.vn/NCC-2026-000140/truy-xuat/DH260911Y67NC3";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb(bool coTenant = true)
    {
        var accessor = new TestMultiTenantContextAccessor();
        if (coTenant) accessor.SetTenant(CoSo);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private (int noiBo, int hnc, int hncChuaLink) Seed()
    {
        using var db = MoDb();
        db.KhachHangs.Add(new KhachHang { MaKhachHang = "KH-0001", TenKhachHang = "Cửa hàng Minh An" });
        db.Products.Add(new Product { MaSanPham = "SP01", TenSanPham = "Bánh mì", DonViTinh = "cái", LoaiSanPham = LoaiSanPham.ThanhPham });
        db.Batches.Add(new Batch { MaSanPham = "SP01", MaLo = "LO-1", TenLo = "Lô 1", NgayNhap = new DateOnly(2026, 9, 13),
                                   NgaySanXuat = new DateOnly(2026, 9, 13), HanSuDung = new DateOnly(2026, 9, 20) });
        db.DonHangNhans.Add(new DonHangNhan { TenantId = CoSo, MaDonHang = "DH-HNC-1", LinkTruyXuat = LinkHnC });

        DonHangBan Don(string ma, NguonDonHang nguon, string? maHnC) => new()
        {
            MaDonHang = ma, MaKhachHang = "KH-0001", MaKho = "K-01", NgayDat = new DateOnly(2026, 9, 14),
            Nguon = nguon, MaDonHnC = maHnC, TrangThai = TrangThaiDonHangBan.DangGiao,
            Dong =
            {
                new DonHangBanDong
                {
                    MaThanhPham = "SP01", SoLuong = 10, DonGia = 5000,
                    XuatLo = { new DonHangBanXuatLo { MaLo = "LO-1", SoLuong = 10, HanSuDung = new DateOnly(2026, 9, 20) } }
                }
            }
        };
        var noiBo = Don("DH-20260914-001", NguonDonHang.NoiBo, null);
        var hnc = Don("DH-20260914-002", NguonDonHang.HanoiCheck, "DH-HNC-1");
        var hncChuaLink = Don("DH-20260914-003", NguonDonHang.HanoiCheck, "DH-HNC-KHONG-CO");
        db.DonHangBans.AddRange(noiBo, hnc, hncChuaLink);
        db.SaveChanges();
        return (noiBo.Id, hnc.Id, hncChuaLink.Id);
    }

    [Fact]
    public async Task Don_HanoiCheck_Dung_Link_Truy_Xuat_Don_Noi_Bo_Dung_Trang_He_Thong()
    {
        var (noiBo, hnc, hncChuaLink) = Seed();
        using var db = MoDb();
        var svc = new TraCuuCongKhaiService(db);

        var qrHnC = await svc.LayQrDonHangAsync(hnc);
        Assert.Equal(LinkHnC, qrHnC!.LinkNgoai);
        Assert.Null(qrHnC.MaTraCuu);

        var qrNoiBo = await svc.LayQrDonHangAsync(noiBo);
        Assert.Null(qrNoiBo!.LinkNgoai);
        Assert.Matches("^[0-9a-f]{32}$", qrNoiBo.MaTraCuu);
        Assert.Equal(qrNoiBo.MaTraCuu, (await svc.LayQrDonHangAsync(noiBo))!.MaTraCuu);   // mở lại: giữ nguyên mã

        // Đơn HanoiCheck chưa có link truy xuất thì dùng trang của hệ thống.
        Assert.NotNull((await svc.LayQrDonHangAsync(hncChuaLink))!.MaTraCuu);
        Assert.Null(await svc.LayQrDonHangAsync(9999));
    }

    [Fact]
    public async Task Tra_Cuu_Cong_Khai_Khong_Can_Dang_Nhap_Ma_Sai_Thi_Khong_Thay()
    {
        var (noiBo, _, _) = Seed();
        string maDon;
        using (var db = MoDb())
            maDon = (await new TraCuuCongKhaiService(db).LayQrDonHangAsync(noiBo))!.MaTraCuu!;

        using (var db = MoDb(coTenant: false))
        {
            var svc = new TraCuuCongKhaiService(db);
            var don = await svc.TraCuuDonHangAsync(maDon);
            Assert.NotNull(don);
            Assert.Equal("DH-20260914-001", don!.MaDonHang);
            Assert.Equal("Cửa hàng Minh An", don.TenKhachHang);
            Assert.Equal("Đang giao", don.TrangThai);
            var dong = Assert.Single(don.Dong);
            Assert.Equal("Bánh mì", dong.TenSanPham);
            var lo = Assert.Single(dong.Lo);
            Assert.Equal(new DateOnly(2026, 9, 13), lo.NgaySanXuat);
            Assert.NotNull(lo.MaTraCuu);                      // lô đã xuất có trang riêng

            var chiTietLo = await svc.TraCuuLoAsync(lo.MaTraCuu!);
            Assert.Equal("LO-1", chiTietLo!.MaLo);
            Assert.Equal("Bánh mì", chiTietLo.TenSanPham);

            Assert.Null(await svc.TraCuuDonHangAsync(Guid.NewGuid().ToString("N")));
            Assert.Null(await svc.TraCuuDonHangAsync("DH-20260914-001"));   // không tra được bằng mã đơn
            Assert.Null(await svc.TraCuuLoAsync(maDon));                    // mã đơn không mở được trang lô
        }
    }

    [Fact]
    public async Task Qr_Lo_Sinh_Ma_Mot_Lan()
    {
        Seed();
        using var db = MoDb();
        var id = (await db.Batches.SingleAsync()).Id;
        var svc = new TraCuuCongKhaiService(db);

        var ma = (await svc.LayQrLoAsync(id))!.MaTraCuu;
        Assert.Matches("^[0-9a-f]{32}$", ma);
        Assert.Equal(ma, (await svc.LayQrLoAsync(id))!.MaTraCuu);
    }
}
