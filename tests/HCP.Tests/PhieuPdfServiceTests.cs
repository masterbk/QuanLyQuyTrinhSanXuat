using System.Text.Json;
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
                BieuMauId = tuLanhId, Ngay = ngay, GiaTriDauJson = "{\"khu_vuc\":\"Kho lạnh\"}",
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
            if (Environment.GetEnvironmentVariable("PDF_OUT_DIR") is { Length: > 0 } dir)
                await File.WriteAllBytesAsync(Path.Combine(dir, "bang-phang.pdf"), bytes);
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
    public void Gom_Ma_Tran_Checklist_X_O_Va_Phieu_Sau_Ghi_De_O_Co_Nhap()
    {
        var cot = new List<TruongBieuMau>
        {
            new() { Ma = "dau_ca", Kieu = KieuTruongBieuMau.DatKhongDat },
            new() { Ma = "cuoi_ca", Kieu = KieuTruongBieuMau.DatKhongDat },
        };
        var ngay = new DateOnly(2026, 10, 7);
        var dsPhieu = new[]
        {
            new PhieuGhiNhan { Id = 1, Ngay = ngay, Dong =
            {
                new DongGhiNhan { HangMucBieuMauId = 10, GiaTriJson = "{\"dau_ca\":\"Đạt\",\"cuoi_ca\":\"Không đạt\"}" },
                new DongGhiNhan { HangMucBieuMauId = 11, GiaTriJson = "{\"dau_ca\":\"Đạt\"}" },
            } },
            // Phiếu thứ 2 cùng ngày chỉ nhập cuối ca hạng mục 11 -> không xoá đầu ca đã có.
            new PhieuGhiNhan { Id = 2, Ngay = ngay, Dong =
            {
                new DongGhiNhan { HangMucBieuMauId = 11, GiaTriJson = "{\"cuoi_ca\":\"Đạt\"}" },
            } },
        };

        var mt = PhieuPdfService.GomMaTranChecklist(dsPhieu, cot);

        Assert.Equal(new[] { "X", "O" }, mt[(10, 7)]);
        Assert.Equal(new[] { "X", "X" }, mt[(11, 7)]);
        Assert.False(mt.ContainsKey((10, 8)));
    }

    [Theory]
    [InlineData(2)] // 2 ca: 31 ngày × 2 = 62 cột con -> 1 trang
    [InlineData(3)] // 3 ca: quá 62 cột con -> tách nửa tháng
    public async Task Bao_Cao_Thang_Checklist_Ma_Tran_Ra_Pdf(int soCa)
    {
        int mauId;
        List<int> hangMuc;
        using (var db = MoDb())
        {
            var mau = new BieuMauEntity
            {
                MaHieu = "BM-CL", Ten = "Checklist thử", BoCuc = BoCucBieuMau.Checklist,
                GhiChuChan = "Đạt ghi X, Không đạt ghi O.",
            };
            mau.Truong.Add(new TruongBieuMau { Ten = "Khu vực", Ma = "khu_vuc", Kieu = KieuTruongBieuMau.Text, LaDauPhieu = true });
            foreach (var ca in new[] { "Sáng", "Chiều", "Tối" }.Take(soCa))
                mau.Truong.Add(new TruongBieuMau { Ten = $"Ca {ca}", Ma = "", Kieu = KieuTruongBieuMau.DatKhongDat });
            mau.Truong.Add(new TruongBieuMau { Ten = "Ghi chú", Ma = "ghi_chu", Kieu = KieuTruongBieuMau.Text });
            for (var i = 1; i <= 20; i++) mau.HangMuc.Add(new HangMucBieuMau { Ten = $"Hạng mục {i}" });
            Assert.True((await new BieuMauService(db).LuuAsync(mau)).ThanhCong);
            mauId = mau.Id;
            hangMuc = mau.HangMuc.OrderBy(h => h.ThuTu).Select(h => h.Id).ToList();
        }

        string[] maCa;
        using (var db = MoDb())
            maCa = (await db.TruongBieuMaus.Where(t => t.BieuMauId == mauId && t.Kieu == KieuTruongBieuMau.DatKhongDat)
                .OrderBy(t => t.ThuTu).Select(t => t.Ma).ToListAsync()).ToArray();

        foreach (var n in new[] { 1, 2, 15, 31 })
        {
            using var db = MoDb();
            var p = new PhieuGhiNhan { BieuMauId = mauId, Ngay = new DateOnly(2026, 10, n), GiaTriDauJson = "{\"khu_vuc\":\"Xưởng 1\"}" };
            foreach (var hm in hangMuc)
            {
                var gt = maCa.ToDictionary(m => m, _ => "Đạt");
                if (n == 15 && hm == hangMuc[3]) { gt[maCa[^1]] = "Không đạt"; gt["ghi_chu"] = "Đèn hỏng, đã báo"; }
                p.Dong.Add(new DongGhiNhan { HangMucBieuMauId = hm, GiaTriJson = JsonSerializer.Serialize(gt) });
            }
            Assert.True((await new PhieuGhiNhanService(db).TaoPhieuAsync(p)).ThanhCong);
        }

        using (var db = MoDb())
        {
            var bytes = await Pdf(db).TaoBaoCaoThangAsync(mauId, 2026, 10);
            Assert.NotNull(bytes);
            Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, bytes!.Take(4).ToArray());
            // Đặt biến môi trường để xuất file xem bằng mắt khi cần.
            if (Environment.GetEnvironmentVariable("PDF_OUT_DIR") is { Length: > 0 } dir)
                await File.WriteAllBytesAsync(Path.Combine(dir, $"checklist-{soCa}ca.pdf"), bytes);
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
