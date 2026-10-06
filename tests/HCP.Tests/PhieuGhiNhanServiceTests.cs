using System.Text.Json;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BieuMau;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>Kiểm chứng nhập phiếu ghi nhận: lọc biểu mẫu theo vai trò, validate dòng/trường bắt buộc/hạng mục checklist.</summary>
public class PhieuGhiNhanServiceTests
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

    private async Task NapMauAsync()
    {
        using var db = MoDb();
        Assert.True((await new BieuMauService(db).NapMauMacDinhAsync()).ThanhCong);
    }

    private static string Json(params (string k, string v)[] kv) =>
        JsonSerializer.Serialize(kv.ToDictionary(x => x.k, x => x.v));

    [Fact]
    public async Task Loc_Bieu_Mau_Theo_Vai_Tro_Va_Kich_Hoat()
    {
        await NapMauAsync();
        using var db = MoDb();
        var svc = new PhieuGhiNhanService(db);

        // Nhân viên sản xuất: thấy nhiệt độ tủ + đèn UV + checklist (QuyenSanXuat), KHÔNG thấy vệ sinh xe (QuyenGiaoHang).
        var cuaSanXuat = await svc.LayBieuMauChoNhapAsync(new[] { "TenantSanXuat" });
        Assert.Equal(3, cuaSanXuat.Count);
        Assert.DoesNotContain(cuaSanXuat, b => b.MaHieu == "BM-GMP-ISO-06-01");

        // Nhân viên giao hàng: chỉ thấy vệ sinh xe.
        var cuaGiao = await svc.LayBieuMauChoNhapAsync(new[] { "TenantGiaoHang" });
        Assert.Single(cuaGiao);
        Assert.Equal("BM-GMP-ISO-06-01", cuaGiao[0].MaHieu);
    }

    [Fact]
    public async Task Tao_Phieu_Validate_Dong_Va_Truong_Bat_Buoc()
    {
        await NapMauAsync();
        int tuLanhId;
        using (var db = MoDb()) tuLanhId = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-GMP.08-04")).Id;

        // Không có dòng -> chặn.
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).TaoPhieuAsync(new PhieuGhiNhan { BieuMauId = tuLanhId });
            Assert.False(kq.ThanhCong);
            Assert.Contains("ít nhất một dòng", kq.ThongBao);
        }

        // Thiếu trường bắt buộc (nhiet_do_dong_sang) -> chặn.
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).TaoPhieuAsync(new PhieuGhiNhan
            {
                BieuMauId = tuLanhId,
                Dong = { new DongGhiNhan { GiaTriJson = Json(("nhiet_do_mat_sang", "5")) } }
            });
            Assert.False(kq.ThanhCong);
            Assert.Contains("bắt buộc", kq.ThongBao);
        }

        // Đủ trường bắt buộc -> lưu được.
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).TaoPhieuAsync(new PhieuGhiNhan
            {
                BieuMauId = tuLanhId, Ngay = new DateOnly(2026, 10, 7),
                Dong = { new DongGhiNhan { GiaTriJson = Json(("nhiet_do_dong_sang", "-18"), ("nhiet_do_mat_sang", "5")) } }
            });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }
        using (var db = MoDb()) Assert.Equal(1, await db.PhieuGhiNhans.CountAsync());
    }

    [Fact]
    public async Task Tao_Phieu_Checklist_Bat_Buoc_Gan_Dung_Hang_Muc()
    {
        await NapMauAsync();
        int checklistId, hangMucId;
        using (var db = MoDb())
        {
            var cl = await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-KT.KCS-01");
            checklistId = cl.Id;
            hangMucId = (await db.HangMucBieuMaus.Where(h => h.BieuMauId == checklistId).OrderBy(h => h.ThuTu).FirstAsync()).Id;
        }

        // Dòng checklist không gắn hạng mục -> chặn.
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).TaoPhieuAsync(new PhieuGhiNhan
            {
                BieuMauId = checklistId,
                Dong = { new DongGhiNhan { GiaTriJson = Json(("dau_ca", "X")) } }
            });
            Assert.False(kq.ThanhCong);
            Assert.Contains("hạng mục", kq.ThongBao);
        }

        // Gắn đúng hạng mục -> lưu được.
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).TaoPhieuAsync(new PhieuGhiNhan
            {
                BieuMauId = checklistId,
                Dong = { new DongGhiNhan { HangMucBieuMauId = hangMucId, GiaTriJson = Json(("dau_ca", "X"), ("cuoi_ca", "X")) } }
            });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }
    }
}
