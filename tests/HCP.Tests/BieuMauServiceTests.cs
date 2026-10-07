using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BieuMau;
using Microsoft.EntityFrameworkCore;
using BieuMauEntity = HCP.Domain.Entities.Business.BieuMau;

namespace HCP.Tests;

/// <summary>Kiểm chứng định nghĩa biểu mẫu: nạp mẫu mặc định (idempotent), validate trường, tự sinh khoá, chặn xoá khi có phiếu.</summary>
public class BieuMauServiceTests
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

    private static BieuMauService Svc(AppDbContext db) => new(db);

    [Fact]
    public async Task Nap_Mau_Mac_Dinh_Tao_Bo_Mau_Va_Idempotent()
    {
        using (var db = MoDb()) Assert.True((await Svc(db).NapMauMacDinhAsync()).ThanhCong);

        using (var db = MoDb())
        {
            Assert.Equal(7, await db.BieuMaus.CountAsync());                 // 3 nhóm A + 1 checklist + 3 nhóm C
            Assert.Equal(20, await db.HangMucBieuMaus.CountAsync());         // check list vệ sinh 20 hạng mục
            var checklist = await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-KT.KCS-01");
            Assert.Equal(BoCucBieuMau.Checklist, checklist.BoCuc);
            var tuLanh = await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-GMP.08-04");
            Assert.Equal(9, await db.TruongBieuMaus.CountAsync(t => t.BieuMauId == tuLanh.Id)); // 8 cột + 1 trường đầu phiếu (khu vực)
        }

        // Gọi lại không tạo trùng.
        using (var db = MoDb())
        {
            var kq = await Svc(db).NapMauMacDinhAsync();
            Assert.True(kq.ThanhCong);
            Assert.Contains("đã có sẵn", kq.ThongBao);
        }
        using (var db = MoDb()) Assert.Equal(7, await db.BieuMaus.CountAsync());
    }

    [Fact]
    public async Task Luu_Validate_Va_Tu_Sinh_Khoa_Truong()
    {
        using var db = MoDb();
        var svc = Svc(db);

        // Thiếu mã hiệu / tên.
        Assert.Contains("mã hiệu", (await svc.LuuAsync(new BieuMauEntity { Ten = "X", Truong = { new TruongBieuMau { Ten = "A" } } })).ThongBao);
        Assert.Contains("tên biểu mẫu", (await svc.LuuAsync(new BieuMauEntity { MaHieu = "BM-X", Truong = { new TruongBieuMau { Ten = "A" } } })).ThongBao);
        // Không có trường.
        Assert.Contains("ít nhất một trường", (await svc.LuuAsync(new BieuMauEntity { MaHieu = "BM-X", Ten = "X" })).ThongBao);

        // Hợp lệ: khoá trường bỏ trống -> tự sinh từ tên (bỏ dấu).
        var mau = new BieuMauEntity
        {
            MaHieu = "BM-X", Ten = "Mẫu X",
            Truong = { new TruongBieuMau { Ten = "Nhiệt độ tủ", Kieu = KieuTruongBieuMau.So } }
        };
        Assert.True((await svc.LuuAsync(mau)).ThanhCong);
        Assert.Equal("nhiet_do_tu", (await db.TruongBieuMaus.SingleAsync(t => t.BieuMauId == mau.Id)).Ma);

        // Trùng mã hiệu -> chặn.
        Assert.Contains("đã tồn tại",
            (await svc.LuuAsync(new BieuMauEntity { MaHieu = "BM-X", Ten = "Khác", Truong = { new TruongBieuMau { Ten = "A" } } })).ThongBao);

        // Trùng khoá trường trong cùng mẫu -> chặn.
        var trungKhoa = new BieuMauEntity
        {
            MaHieu = "BM-Y", Ten = "Mẫu Y",
            Truong =
            {
                new TruongBieuMau { Ten = "Giờ", Ma = "gio" },
                new TruongBieuMau { Ten = "Giờ 2", Ma = "gio" }
            }
        };
        Assert.Contains("bị lặp", (await svc.LuuAsync(trungKhoa)).ThongBao);
    }

    [Fact]
    public async Task Xoa_Chan_Khi_Da_Co_Phieu_Ghi_Nhan()
    {
        int id;
        using (var db = MoDb())
        {
            var mau = new BieuMauEntity { MaHieu = "BM-Z", Ten = "Mẫu Z", Truong = { new TruongBieuMau { Ten = "A" } } };
            Assert.True((await Svc(db).LuuAsync(mau)).ThanhCong);
            id = mau.Id;
        }
        using (var db = MoDb())
        {
            db.PhieuGhiNhans.Add(new PhieuGhiNhan { BieuMauId = id, Ngay = new DateOnly(2026, 10, 7) });
            await db.SaveChangesAsync();
        }
        using (var db = MoDb())
        {
            var kq = await Svc(db).XoaAsync(id);
            Assert.False(kq.ThanhCong);
            Assert.Contains("đã có phiếu", kq.ThongBao);
        }
        using (var db = MoDb()) Assert.True(await db.BieuMaus.AnyAsync(b => b.Id == id));
    }

    [Fact]
    public async Task Sua_Mau_Checklist_Giu_Id_Hang_Muc_Cho_Phieu_Cu()
    {
        int mauId, hm1Id;
        using (var db = MoDb())
        {
            var mau = new BieuMauEntity
            {
                MaHieu = "BM-CL", Ten = "Checklist X", BoCuc = BoCucBieuMau.Checklist,
                Truong = { new TruongBieuMau { Ten = "Kết quả", Kieu = KieuTruongBieuMau.DatKhongDat } },
                HangMuc = { new HangMucBieuMau { Ten = "Sàn nhà" }, new HangMucBieuMau { Ten = "Tường" } }
            };
            Assert.True((await Svc(db).LuuAsync(mau)).ThanhCong);
            mauId = mau.Id;
        }
        using (var db = MoDb())
            hm1Id = (await db.HangMucBieuMaus.Where(h => h.BieuMauId == mauId).OrderBy(h => h.ThuTu).FirstAsync()).Id;

        // Phiếu cũ gắn hạng mục 1.
        using (var db = MoDb())
        {
            db.PhieuGhiNhans.Add(new PhieuGhiNhan
            {
                BieuMauId = mauId, Ngay = new DateOnly(2026, 10, 7),
                Dong = { new DongGhiNhan { HangMucBieuMauId = hm1Id, GiaTriJson = "{}" } }
            });
            await db.SaveChangesAsync();
        }

        // Sửa mẫu: đổi tên hạng mục 1, thêm hạng mục mới -> phải GIỮ Id cũ.
        // Load và lưu ở 2 context khác nhau (giống web dựng bản sao detached rồi mới gọi LuuAsync).
        BieuMauEntity sua;
        using (var db = MoDb()) sua = (await Svc(db).LayTheoIdAsync(mauId))!;
        sua.HangMuc[0].Ten = "Sàn nhà (đã đổi)";
        sua.HangMuc.Add(new HangMucBieuMau { Ten = "Trần" });
        using (var db = MoDb()) Assert.True((await Svc(db).LuuAsync(sua)).ThanhCong);

        using (var db = MoDb())
        {
            var hm1 = await db.HangMucBieuMaus.FirstOrDefaultAsync(h => h.Id == hm1Id);
            Assert.NotNull(hm1);                                   // Id cũ còn nguyên
            Assert.Equal("Sàn nhà (đã đổi)", hm1!.Ten);           // nội dung đã cập nhật
            var phieu = await db.PhieuGhiNhans.Include(p => p.Dong).SingleAsync();
            Assert.Equal(hm1Id, phieu.Dong.Single().HangMucBieuMauId); // phiếu cũ vẫn liên kết đúng
            Assert.Equal(3, await db.HangMucBieuMaus.CountAsync(h => h.BieuMauId == mauId)); // 2 cũ + 1 mới
        }
    }
}
