using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.Kho;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>Kiểm chứng danh mục khách hàng: thêm/sửa, chặn trùng mã, chặn xoá khi đã có đơn hàng.</summary>
public class KhachHangServiceTests
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

    [Fact]
    public async Task Them_Va_Sua_Khach_Hang()
    {
        using (var db = MoDb())
        {
            var kq = await new KhachHangService(db).LuuAsync(
                new KhachHang { MaKhachHang = "KH01", TenKhachHang = "Cửa hàng A", Loai = LoaiKhachHang.CuaHang });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        int id;
        using (var db = MoDb())
        {
            var kh = await db.KhachHangs.SingleAsync();
            id = kh.Id;
            Assert.Equal("Cửa hàng A", kh.TenKhachHang);
        }

        using (var db = MoDb())
        {
            var kq = await new KhachHangService(db).LuuAsync(
                new KhachHang { Id = id, MaKhachHang = "KH01", TenKhachHang = "Đại lý B", Loai = LoaiKhachHang.DaiLy });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        using (var db = MoDb())
        {
            var kh = await db.KhachHangs.SingleAsync();
            Assert.Equal("Đại lý B", kh.TenKhachHang);
            Assert.Equal(LoaiKhachHang.DaiLy, kh.Loai);
        }
    }

    [Fact]
    public async Task Chan_Trung_Ma()
    {
        using (var db = MoDb())
            await new KhachHangService(db).LuuAsync(new KhachHang { MaKhachHang = "KH01", TenKhachHang = "A" });

        using (var db = MoDb())
        {
            var kq = await new KhachHangService(db).LuuAsync(new KhachHang { MaKhachHang = "KH01", TenKhachHang = "B" });
            Assert.False(kq.ThanhCong);
            Assert.Contains("đã tồn tại", kq.ThongBao);
        }
    }

    [Fact]
    public async Task Chan_Xoa_Khi_Da_Co_Don_Hang()
    {
        int id;
        using (var db = MoDb())
        {
            await new KhachHangService(db).LuuAsync(new KhachHang { MaKhachHang = "KH01", TenKhachHang = "A" });
            id = (await db.KhachHangs.SingleAsync()).Id;
            db.DonHangBans.Add(new DonHangBan { MaDonHang = "DH-1", MaKhachHang = "KH01", MaKho = "KHO01",
                                                NgayDat = new DateOnly(2026, 9, 6) });
            await db.SaveChangesAsync();
        }

        using (var db = MoDb())
        {
            var kq = await new KhachHangService(db).XoaAsync(id);
            Assert.False(kq.ThanhCong);
            Assert.Contains("đơn hàng", kq.ThongBao);
        }
    }
}
