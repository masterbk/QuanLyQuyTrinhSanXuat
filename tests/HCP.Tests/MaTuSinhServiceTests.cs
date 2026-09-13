using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng mã tự sinh: đúng format, đánh số riêng từng cơ sở, không trùng mã cũ, không cấp lại mã
/// đã xoá, hai người sinh cùng lúc không trùng số, và mã bị khoá khi sửa.
/// </summary>
public class MaTuSinhServiceTests
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

    private sealed class NoOpOutbox : ISyncOutboxWriter
    {
        public Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> DangBatAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task<string?> GuiAsync(object banGhi, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    private static CoSoSanXuatService CoSoSvc(AppDbContext db) => new(db, new NoOpOutbox(), new MaTuSinhService(db));

    private async Task<string> ThemCoSoAsync(string tenant = CoSoA, string maGuiLen = "GO-TAY")
    {
        using var db = MoDb(tenant);
        var coSo = new Facility { MaCoSo = maGuiLen, TenCoSo = "Xưởng" };
        var kq = await CoSoSvc(db).ThemAsync(coSo);
        Assert.True(kq.ThanhCong, kq.ThongBao);
        return coSo.MaCoSo;
    }

    [Fact]
    public async Task Danh_Muc_Sinh_Ma_Tang_Dan_Dung_Format_Bo_Qua_Ma_Gui_Len()
    {
        Assert.Equal("CS-0001", await ThemCoSoAsync());
        Assert.Equal("CS-0002", await ThemCoSoAsync());

        using var db = MoDb();
        var khau = new ProductionStep { MaKhau = "SO_CHE", TenKhau = "Sơ chế" };
        Assert.True((await new KhauSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db)).ThemAsync(khau)).ThanhCong);
        Assert.Equal("KHAU-0001", khau.MaKhau);

        var ncc = new SubSupplier
        {
            MaNccDauVao = "NCC-GO-TAY", Ten = "Công ty rau",
            NhomThucPham = { new SubSupplierFoodGroup { MaNhom = "RAU" } }
        };
        Assert.True((await new NccDauVaoService(db, new NoOpOutbox(), new MaTuSinhService(db)).ThemAsync(ncc)).ThanhCong);
        Assert.Equal("NCC-0001", ncc.MaNccDauVao);
    }

    [Fact]
    public async Task Moi_Co_So_Mot_Day_So_Rieng()
    {
        Assert.Equal("CS-0001", await ThemCoSoAsync(CoSoA));
        Assert.Equal("CS-0002", await ThemCoSoAsync(CoSoA));
        Assert.Equal("CS-0001", await ThemCoSoAsync(CoSoB));
    }

    [Fact]
    public async Task Bat_Dau_Sau_Ma_Cu_Cung_Format_De_Khong_Trung()
    {
        using (var db = MoDb())
        {
            db.Facilities.Add(new Facility { MaCoSo = "CS-0007", TenCoSo = "Nhập tay trước đây" });
            db.Facilities.Add(new Facility { MaCoSo = "CS-HN", TenCoSo = "Mã khác format" });
            await db.SaveChangesAsync();
        }

        Assert.Equal("CS-0008", await ThemCoSoAsync());
    }

    [Fact]
    public async Task Khong_Cap_Lai_Ma_Da_Xoa()
    {
        await ThemCoSoAsync();
        var ma2 = await ThemCoSoAsync();
        using (var db = MoDb())
        {
            var id = (await db.Facilities.SingleAsync(f => f.MaCoSo == ma2)).Id;
            Assert.True((await CoSoSvc(db).XoaAsync(id)).ThanhCong);
        }

        // CS-0002 có thể đã gửi HanoiCheck - cấp lại sẽ ghi đè dữ liệu cũ bên đó.
        Assert.Equal("CS-0003", await ThemCoSoAsync());
    }

    [Fact]
    public async Task Lo_Va_Lenh_Danh_So_Theo_Ngay_Lo_Dung_Chung_Mot_Day()
    {
        var ngay = new DateOnly(2026, 9, 13);
        using var db = MoDb();
        var svc = new MaTuSinhService(db);

        Assert.Equal("LO-20260913-001", await svc.SinhAsync(LoaiMaTuSinh.LoSanXuat, ngay));
        Assert.Equal(new[] { "LO-20260913-002", "LO-20260913-003" },
                     await svc.SinhNhieuAsync(LoaiMaTuSinh.LoSanXuat, 2, ngay));
        Assert.Equal("LO-20260914-001", await svc.SinhAsync(LoaiMaTuSinh.LoSanXuat, ngay.AddDays(1)));
        Assert.Equal("LSX-20260913-001", await svc.SinhAsync(LoaiMaTuSinh.LenhSanXuat, ngay));
    }

    [Fact]
    public async Task Day_Lo_Bat_Dau_Sau_Ma_Lo_Da_Co_Trong_So_Kho_Va_Lenh()
    {
        using (var db = MoDb())
        {
            db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = "BOT_MI", MaKho = "KHO01", MaLo = "LO-20260913-004", SoLuong = 1,
                ThoiGianUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var db2 = MoDb();
        Assert.Equal("LO-20260913-005",
            await new MaTuSinhService(db2).SinhAsync(LoaiMaTuSinh.LoSanXuat, new DateOnly(2026, 9, 13)));
    }

    [Fact]
    public async Task Hai_Nguoi_Sinh_Cung_Luc_Khong_Trung_So()
    {
        Assert.Equal("CS-0001", await ThemCoSoAsync());

        using var dbCham = MoDb();
        // Người thứ nhất đã đọc bộ đếm (giá trị 1) nhưng chưa kịp ghi...
        _ = await dbCham.BoDemMas.SingleAsync();

        // ...thì người thứ hai cấp xong CS-0002.
        using (var dbNhanh = MoDb())
            Assert.Equal("CS-0002", await new MaTuSinhService(dbNhanh).SinhAsync(LoaiMaTuSinh.CoSo));

        // Người thứ nhất ghi đè bằng số cũ sẽ đụng concurrency token -> tự đọc lại và nhận CS-0003.
        Assert.Equal("CS-0003", await new MaTuSinhService(dbCham).SinhAsync(LoaiMaTuSinh.CoSo));
    }

    [Fact]
    public async Task Sua_Danh_Muc_Khong_Doi_Duoc_Ma()
    {
        var ma = await ThemCoSoAsync();
        using (var db = MoDb())
        {
            var id = (await db.Facilities.SingleAsync()).Id;
            var kq = await CoSoSvc(db).CapNhatAsync(new Facility { Id = id, MaCoSo = "CS-KHAC", TenCoSo = "Tên mới" });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        using var db2 = MoDb();
        var coSo = await db2.Facilities.SingleAsync();
        Assert.Equal(ma, coSo.MaCoSo);
        Assert.Equal("Tên mới", coSo.TenCoSo);
    }
}
