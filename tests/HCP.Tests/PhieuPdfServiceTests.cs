using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services;
using HCP.Infrastructure.Services.BieuMau;
using BieuMauEntity = HCP.Domain.Entities.Business.BieuMau;
using HCP.Infrastructure.Services.DanhMuc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

namespace HCP.Tests;

/// <summary>Danh mục giả trả rỗng - PDF chỉ cần tra tên, thiếu thì hiển thị nguyên mã.</summary>
public sealed class FakeDanhMuc<T> : IDanhMucService<T> where T : class
{
    public Task<IReadOnlyList<T>> LayTatCaAsync(CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<T>)Array.Empty<T>());
    public Task<T?> LayTheoIdAsync(int id, CancellationToken ct = default) => Task.FromResult<T?>(null);
    public Task<KetQuaThaoTac> ThemAsync(T entity, CancellationToken ct = default) => Task.FromResult(KetQuaThaoTac.Ok(""));
    public Task<KetQuaThaoTac> CapNhatAsync(T entity, CancellationToken ct = default) => Task.FromResult(KetQuaThaoTac.Ok(""));
    public Task<KetQuaThaoTac> XoaAsync(int id, CancellationToken ct = default) => Task.FromResult(KetQuaThaoTac.Ok(""));
}

/// <summary>Kiểm chứng sinh PDF phiếu ghi nhận không lỗi và ra đúng định dạng PDF.</summary>
public class PhieuPdfServiceTests
{
    private const string CoSo = "coso-a";
    private readonly string _dbName = Guid.NewGuid().ToString();

    static PhieuPdfServiceTests() => QuestPDF.Settings.License = LicenseType.Community;

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private PhieuPdfService Pdf(AppDbContext db) => new(
        new PhieuGhiNhanService(db), db,
        new FakeDanhMuc<Staff>(), new FakeDanhMuc<Product>(),
        new FakeDanhMuc<SubSupplier>(), new FakeDanhMuc<Facility>());

    [Fact]
    public async Task Sinh_Pdf_Phieu_Ra_Dung_Dinh_Dang()
    {
        using (var db = MoDb()) await new BieuMauService(db).NapMauMacDinhAsync();
        int tuLanhId;
        using (var db = MoDb()) tuLanhId = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-GMP.08-04")).Id;

        int phieuId;
        using (var db = MoDb())
        {
            var phieu = new PhieuGhiNhan
            {
                BieuMauId = tuLanhId, Ngay = new DateOnly(2026, 10, 7),
                GiaTriDauJson = "{\"khu_vuc\":\"Khu sản xuất\"}", NguoiLap = "NS01",
                Dong = { new DongGhiNhan { GiaTriJson = "{\"nhiet_do_dong_sang\":\"-18\",\"tinh_trang_sang\":\"Đạt\"}" } }
            };
            Assert.True((await new PhieuGhiNhanService(db).TaoPhieuAsync(phieu)).ThanhCong);
            phieuId = phieu.Id;
        }

        using (var db = MoDb())
        {
            var bytes = await Pdf(db).TaoPdfAsync(phieuId);
            Assert.NotNull(bytes);
            Assert.True(bytes!.Length > 1000);
            // Chữ ký file PDF: "%PDF".
            Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, bytes.Take(4).ToArray());
        }

        // Phiếu không tồn tại -> null.
        using (var db = MoDb()) Assert.Null(await Pdf(db).TaoPdfAsync(999999));
    }

    [Fact]
    public async Task Sinh_Bao_Cao_Thang_Ra_Dung_Dinh_Dang()
    {
        using (var db = MoDb()) await new BieuMauService(db).NapMauMacDinhAsync();
        int tuLanhId;
        using (var db = MoDb()) tuLanhId = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-GMP.08-04")).Id;

        // Hai phiếu khác ngày trong tháng 10/2026.
        foreach (var ngay in new[] { new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 8) })
        {
            using var db = MoDb();
            var p = new PhieuGhiNhan
            {
                BieuMauId = tuLanhId, Ngay = ngay,
                Dong = { new DongGhiNhan { GiaTriJson = "{\"nhiet_do_dong_sang\":\"-18\"}" } }
            };
            Assert.True((await new PhieuGhiNhanService(db).TaoPhieuAsync(p)).ThanhCong);
        }

        using (var db = MoDb())
        {
            var bytes = await Pdf(db).TaoBaoCaoThangAsync(tuLanhId, 2026, 10);
            Assert.NotNull(bytes);
            Assert.True(bytes!.Length > 1000);
            Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, bytes.Take(4).ToArray());
        }

        // Tháng không có phiếu vẫn ra PDF (bảng rỗng), không lỗi.
        using (var db = MoDb()) Assert.NotNull(await Pdf(db).TaoBaoCaoThangAsync(tuLanhId, 2026, 1));
        // Biểu mẫu không tồn tại -> null.
        using (var db = MoDb()) Assert.Null(await Pdf(db).TaoBaoCaoThangAsync(999999, 2026, 10));
    }

    [Fact]
    public async Task Sinh_Pdf_Checklist_Khong_Loi()
    {
        using (var db = MoDb()) await new BieuMauService(db).NapMauMacDinhAsync();
        int clId, hangMucId;
        using (var db = MoDb())
        {
            clId = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-KT.KCS-01")).Id;
            hangMucId = (await db.HangMucBieuMaus.Where(h => h.BieuMauId == clId).OrderBy(h => h.ThuTu).FirstAsync()).Id;
        }
        int phieuId;
        using (var db = MoDb())
        {
            var phieu = new PhieuGhiNhan
            {
                BieuMauId = clId, Ngay = new DateOnly(2026, 10, 7),
                Dong = { new DongGhiNhan { HangMucBieuMauId = hangMucId, GiaTriJson = "{\"dau_ca\":\"Đạt\"}" } }
            };
            Assert.True((await new PhieuGhiNhanService(db).TaoPhieuAsync(phieu)).ThanhCong);
            phieuId = phieu.Id;
        }
        using (var db = MoDb())
        {
            var bytes = await Pdf(db).TaoPdfAsync(phieuId);
            Assert.NotNull(bytes);
            Assert.True(bytes!.Length > 1000);
        }
    }

    [Fact]
    public async Task Sinh_Pdf_Co_Truong_Anh_Khong_Loi()
    {
        int mauId;
        using (var db = MoDb())
        {
            var mau = new BieuMauEntity
            {
                MaHieu = "BM-ANH", Ten = "Mẫu có ảnh", BoCuc = BoCucBieuMau.NhieuDongTuDo,
                Truong =
                {
                    new TruongBieuMau { Ten = "Ghi chú", Ma = "ghi_chu", Kieu = KieuTruongBieuMau.Text },
                    new TruongBieuMau { Ten = "Ảnh minh chứng", Ma = "anh", Kieu = KieuTruongBieuMau.Anh }
                }
            };
            Assert.True((await new BieuMauService(db).LuuAsync(mau)).ThanhCong);
            mauId = mau.Id;
        }

        int phieuId;
        using (var db = MoDb())
        {
            var phieu = new PhieuGhiNhan
            {
                BieuMauId = mauId, Ngay = new DateOnly(2026, 10, 7),
                Dong = { new DongGhiNhan { GiaTriJson = "{\"ghi_chu\":\"ok\",\"anh\":\"/uploads/x/2026/10/abc.jpg\"}" } }
            };
            Assert.True((await new PhieuGhiNhanService(db).TaoPhieuAsync(phieu)).ThanhCong);
            phieuId = phieu.Id;
        }

        // docAnh = null -> ô Ảnh in "Có ảnh", không có mục ảnh; vẫn ra PDF hợp lệ.
        using (var db = MoDb())
        {
            var bytes = await Pdf(db).TaoPdfAsync(phieuId);
            Assert.NotNull(bytes);
            Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, bytes!.Take(4).ToArray());
        }
    }
}
