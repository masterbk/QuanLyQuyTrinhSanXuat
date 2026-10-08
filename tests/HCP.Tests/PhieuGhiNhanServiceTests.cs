using System.Text.Json;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
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
    public async Task Loc_Bieu_Mau_Theo_Role_Nhap_Bieu_Mau_Va_Mau_Duoc_Giao()
    {
        await NapMauAsync();
        int tuLanhId, xeId;
        using (var db = MoDb())
        {
            db.Staff.AddRange(
                new Staff { MaNhanSu = "NS01", HoTen = "Chưa cấu hình", TrangThai = true },
                new Staff { MaNhanSu = "NS02", HoTen = "Tất cả", TrangThai = true },
                new Staff { MaNhanSu = "NS03", HoTen = "Hai mẫu", TrangThai = true });
            await db.SaveChangesAsync();
            tuLanhId = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-GMP.08-04")).Id;
            xeId = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-GMP-ISO-06-01")).Id;
            var ns2 = await db.Staff.FirstAsync(s => s.MaNhanSu == "NS02");
            var ns3 = await db.Staff.FirstAsync(s => s.MaNhanSu == "NS03");
            db.PhanQuyenBieuMaus.AddRange(
                new PhanQuyenBieuMau { NhanSuId = ns2.Id, TatCa = true },
                new PhanQuyenBieuMau { NhanSuId = ns3.Id, TatCa = false, BieuMauIdsCsv = $"{tuLanhId},{xeId}" });
            // Mẫu bị tắt không hiện cho ai.
            (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-KPH-01")).KichHoat = false;
            await db.SaveChangesAsync();
        }

        using var db2 = MoDb();
        var svc = new PhieuGhiNhanService(db2);
        const string Bm = "TenantBieuMau";

        // Quản trị + nhập liệu: mọi mẫu đang kích hoạt (10 - 1 tắt).
        Assert.Equal(9, (await svc.LayBieuMauChoNhapAsync(new[] { "TenantAdmin" }, null)).Count);
        Assert.Equal(9, (await svc.LayBieuMauChoNhapAsync(new[] { "TenantStaff" }, null)).Count);

        // Sản xuất / giao hàng KHÔNG còn tự được nhập biểu mẫu.
        Assert.Empty(await svc.LayBieuMauChoNhapAsync(new[] { "TenantSanXuat" }, "NS01"));
        Assert.Empty(await svc.LayBieuMauChoNhapAsync(new[] { "TenantGiaoHang" }, "NS01"));

        // Role nhập biểu mẫu: chưa cấu hình = tất cả; TatCa = tất cả; danh sách = đúng các mẫu được giao.
        Assert.Equal(9, (await svc.LayBieuMauChoNhapAsync(new[] { Bm }, "NS01")).Count);
        Assert.Equal(9, (await svc.LayBieuMauChoNhapAsync(new[] { Bm }, "NS02")).Count);
        var haiMau = await svc.LayBieuMauChoNhapAsync(new[] { "TenantSanXuat", Bm }, "NS03");
        Assert.Equal(new[] { tuLanhId, xeId }.OrderBy(x => x), haiMau.Select(b => b.Id).OrderBy(x => x));

        // Role nhập biểu mẫu nhưng tài khoản không gắn nhân sự -> không có mẫu nào.
        Assert.Empty(await svc.LayBieuMauChoNhapAsync(new[] { Bm }, null));
    }

    [Fact]
    public async Task Lich_Su_Do_Toi_Lap_Khop_Ma_Nhan_Su_Hoac_Tai_Khoan()
    {
        await NapMauAsync();
        using (var db = MoDb())
        {
            var uv = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-HD-SX-01-01")).Id;
            PhieuGhiNhan Phieu(int ngay, string? ma, string? uid) => new()
            {
                BieuMauId = uv, Ngay = new DateOnly(2026, 10, ngay), NguoiLap = ma, NguoiLapUserId = uid,
                Dong = { new DongGhiNhan { GiaTriJson = Json(("gio_bat", "17:00")) } }
            };
            db.PhieuGhiNhans.AddRange(
                Phieu(1, null, "user-chu-co-so"),   // chủ cơ sở: không gắn nhân sự
                Phieu(2, "NS01", "user-ns01"),
                Phieu(3, "NS02", "user-ns02"));
            await db.SaveChangesAsync();
        }

        using var db2 = MoDb();
        var svc = new PhieuGhiNhanService(db2);
        async Task<int> Dem((string?, string?)? toi) => (await svc.LayLichSuAsync(null, null, null, null, toi, 1, 20)).TongSo;

        Assert.Equal(3, await Dem(null));
        Assert.Equal(1, await Dem((null, "user-chu-co-so")));   // trước đây ra 0 vì chỉ so mã nhân sự
        Assert.Equal(1, await Dem(("NS01", "user-ns01")));
        Assert.Equal(1, await Dem(("NS02", "khac")));            // phiếu cũ chưa có tài khoản vẫn khớp theo mã
        Assert.Equal(0, await Dem((null, null)));
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
    public async Task Luu_Nhap_Cho_Thieu_Bat_Buoc_Roi_Nhap_Tiep_Den_Khi_Hoan_Thanh()
    {
        await NapMauAsync();
        int tuLanhId;
        using (var db = MoDb()) tuLanhId = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-GMP.08-04")).Id;
        var ngay = new DateOnly(2026, 10, 7);

        // Lưu NHÁP buổi sáng - thiếu trường bắt buộc nhiet_do_dong_sang vẫn lưu được.
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).TaoPhieuAsync(new PhieuGhiNhan
            {
                BieuMauId = tuLanhId, Ngay = ngay, NguoiLap = "NS01",
                Dong = { new DongGhiNhan { GiaTriJson = Json(("nhiet_do_mat_sang", "5")) } }
            }, hoanThanh: false);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        // Tìm được phiếu nháp để nhập tiếp.
        int phieuId;
        using (var db = MoDb())
        {
            var nhap = await new PhieuGhiNhanService(db).LayPhieuNhapAsync(tuLanhId, ngay, "NS01");
            Assert.NotNull(nhap);
            Assert.Equal(TrangThaiPhieu.Nhap, nhap!.TrangThai);
            phieuId = nhap.Id;
        }

        // Cập nhật + Hoàn thành: giờ phải đủ trường bắt buộc.
        using (var db = MoDb())
        {
            var thieu = await new PhieuGhiNhanService(db).CapNhatPhieuAsync(new PhieuGhiNhan
            {
                Id = phieuId, Dong = { new DongGhiNhan { GiaTriJson = Json(("nhiet_do_mat_sang", "5")) } }
            }, hoanThanh: true);
            Assert.False(thieu.ThanhCong);
            Assert.Contains("bắt buộc", thieu.ThongBao);
        }
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).CapNhatPhieuAsync(new PhieuGhiNhan
            {
                Id = phieuId,
                Dong = { new DongGhiNhan { GiaTriJson = Json(("nhiet_do_dong_sang", "-18"), ("nhiet_do_dong_chieu", "-17")) } }
            }, hoanThanh: true);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        // Đã hoàn thành: hết nháp, không sửa tiếp được.
        using (var db = MoDb())
        {
            Assert.Null(await new PhieuGhiNhanService(db).LayPhieuNhapAsync(tuLanhId, ngay, "NS01"));
            var k = await new PhieuGhiNhanService(db).CapNhatPhieuAsync(new PhieuGhiNhan
            {
                Id = phieuId, Dong = { new DongGhiNhan { GiaTriJson = Json(("nhiet_do_dong_sang", "-18")) } }
            }, hoanThanh: true);
            Assert.False(k.ThanhCong);
            Assert.Contains("đã hoàn thành", k.ThongBao);
        }
    }

    [Fact]
    public async Task Tao_Phieu_Nhieu_Dong_Tu_Do_Luu_Nhieu_Dong()
    {
        await NapMauAsync();
        int nuongId;
        using (var db = MoDb()) nuongId = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BMKS-01")).Id;

        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).TaoPhieuAsync(new PhieuGhiNhan
            {
                BieuMauId = nuongId, Ngay = new DateOnly(2026, 10, 7),
                Dong =
                {
                    new DongGhiNhan { GiaTriJson = Json(("san_pham", "BANH_MI"), ("nhiet_do", "180")) },
                    new DongGhiNhan { GiaTriJson = Json(("san_pham", "BANH_NGOT"), ("nhiet_do", "170")) },
                }
            });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }
        using (var db = MoDb())
        {
            var phieu = await db.PhieuGhiNhans.Include(p => p.Dong).SingleAsync();
            Assert.Equal(2, phieu.Dong.Count);
            Assert.Equal(new[] { 0, 1 }, phieu.Dong.OrderBy(d => d.ThuTu).Select(d => d.ThuTu));
        }
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

    [Fact]
    public async Task Mau_1_Phieu_Moi_Ngay_Chan_Tao_Trung()
    {
        await NapMauAsync();
        int tuLanhId;
        using (var db = MoDb()) tuLanhId = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-GMP.08-04")).Id;
        var ngay = new DateOnly(2026, 10, 7);

        // Tạo phiếu cho ngày.
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).TaoPhieuAsync(new PhieuGhiNhan
            {
                BieuMauId = tuLanhId, Ngay = ngay,
                Dong = { new DongGhiNhan { GiaTriJson = Json(("nhiet_do_dong_sang", "-18"), ("nhiet_do_mat_sang", "5")) } }
            });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        // Tạo phiếu thứ 2 cùng ngày -> chặn (khoá theo ngày).
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).TaoPhieuAsync(new PhieuGhiNhan
            {
                BieuMauId = tuLanhId, Ngay = ngay,
                Dong = { new DongGhiNhan { GiaTriJson = Json(("nhiet_do_dong_sang", "-19"), ("nhiet_do_mat_sang", "4")) } }
            });
            Assert.False(kq.ThanhCong);
            Assert.Contains("đã có phiếu", kq.ThongBao);
        }

        // Lấy phiếu theo ngày trả đúng phiếu (mọi trạng thái); chỉ có đúng 1 phiếu.
        using (var db = MoDb())
        {
            var p = await new PhieuGhiNhanService(db).LayPhieuTheoNgayAsync(tuLanhId, ngay);
            Assert.NotNull(p);
            Assert.Equal(ngay, p!.Ngay);
            Assert.Equal(1, await db.PhieuGhiNhans.CountAsync());
        }

        // Ngày khác chưa có phiếu -> null.
        using (var db = MoDb())
            Assert.Null(await new PhieuGhiNhanService(db).LayPhieuTheoNgayAsync(tuLanhId, ngay.AddDays(1)));
    }

    [Fact]
    public async Task Mau_Nhieu_Phieu_Moi_Ngay_Cho_Tao_Nhieu()
    {
        await NapMauAsync();
        int kphId;
        using (var db = MoDb()) kphId = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-KPH-01")).Id;
        var ngay = new DateOnly(2026, 10, 7);

        // KPH là mẫu "nhiều phiếu/ngày" -> tạo được nhiều phiếu cùng ngày.
        for (var i = 0; i < 2; i++)
        {
            using var db = MoDb();
            var kq = await new PhieuGhiNhanService(db).TaoPhieuAsync(new PhieuGhiNhan
            {
                BieuMauId = kphId, Ngay = ngay,
                Dong = { new DongGhiNhan { GiaTriJson = Json(("mo_ta", $"Sự cố {i}")) } }
            });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        using (var db = MoDb()) Assert.Equal(2, await db.PhieuGhiNhans.CountAsync());
    }
}
