using System.Text.Json;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
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
    public async Task Cap_Nhat_Phieu_Chan_Ghi_De_Khi_Nguoi_Khac_Vua_Luu()
    {
        await NapMauAsync();
        int id;
        DateTime mocMo;
        using (var db = MoDb())
        {
            var uv = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-HD-SX-01-01")).Id;
            var p = new PhieuGhiNhan
            {
                BieuMauId = uv, Ngay = new DateOnly(2026, 10, 8), TenNguoiLap = "Ca sáng",
                Dong = { new DongGhiNhan { GiaTriJson = Json(("gio_bat", "07:00")) } }
            };
            Assert.True((await new PhieuGhiNhanService(db).TaoPhieuAsync(p, hoanThanh: false)).ThanhCong);
            id = p.Id;
            mocMo = p.ThoiGianUtc;   // hai người cùng mở phiếu ở mốc này
        }

        PhieuGhiNhan Sua(string gio, string ten) => new()
        {
            Id = id, TenNguoiCapNhat = ten,
            Dong = { new DongGhiNhan { GiaTriJson = Json(("gio_bat", gio)) } }
        };

        // Người A lưu trước: được.
        using (var db = MoDb())
            Assert.True((await new PhieuGhiNhanService(db).CapNhatPhieuAsync(Sua("07:05", "Người A"), false, mocMo)).ThanhCong);

        // Người B vẫn cầm mốc cũ: bị chặn, báo tên người A, dữ liệu A giữ nguyên.
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db).CapNhatPhieuAsync(Sua("09:00", "Người B"), false, mocMo);
            Assert.False(kq.ThanhCong);
            Assert.True(kq.XungDot);
            Assert.Contains("Người A", kq.ThongBao);
        }
        using (var db = MoDb())
        {
            var p = await db.PhieuGhiNhans.Include(x => x.Dong).FirstAsync(x => x.Id == id);
            Assert.Contains("07:05", p.Dong.Single().GiaTriJson);

            // B tải lại (mốc mới) rồi lưu: được. Không gửi mốc (app cũ) thì không kiểm.
            var svc = new PhieuGhiNhanService(db);
            db.ChangeTracker.Clear();
            Assert.True((await svc.CapNhatPhieuAsync(Sua("09:00", "Người B"), false, p.ThoiGianUtc)).ThanhCong);
            db.ChangeTracker.Clear();
            Assert.True((await svc.CapNhatPhieuAsync(Sua("10:00", "App cũ"), false)).ThanhCong);
        }
    }

    [Fact]
    public async Task Cap_Nhat_Trong_Cung_DbContext_Lau_Dai_Van_Phat_Hien_Nguoi_Khac_Vua_Luu()
    {
        await NapMauAsync();
        // Màn web giữ MỘT DbContext suốt phiên (Blazor): tạo, rồi lưu nháp lần 2 ngay trên context đó.
        using var web = MoDb();
        var svcWeb = new PhieuGhiNhanService(web);
        var uv = (await web.BieuMaus.FirstAsync(b => b.MaHieu == "BM-HD-SX-01-01")).Id;
        var p = new PhieuGhiNhan { BieuMauId = uv, Ngay = new DateOnly(2026, 10, 9), TenNguoiLap = "Web",
                                   Dong = { new DongGhiNhan { GiaTriJson = Json(("gio_bat", "07:00")) } } };
        Assert.True((await svcWeb.TaoPhieuAsync(p, false)).ThanhCong);
        var moc = (await svcWeb.LayPhieuTheoIdAsync(p.Id))!.ThoiGianUtc;
        Assert.True((await svcWeb.CapNhatPhieuAsync(new PhieuGhiNhan { Id = p.Id,
            Dong = { new DongGhiNhan { GiaTriJson = Json(("gio_bat", "07:01")) } } }, false, moc)).ThanhCong);
        moc = (await svcWeb.LayPhieuTheoIdAsync(p.Id))!.ThoiGianUtc;

        // Người khác (app, context riêng) lưu chen vào.
        await Task.Delay(5);
        using (var app = MoDb())
            Assert.True((await new PhieuGhiNhanService(app).CapNhatPhieuAsync(new PhieuGhiNhan { Id = p.Id, TenNguoiCapNhat = "App",
                Dong = { new DongGhiNhan { GiaTriJson = Json(("gio_bat", "08:00")) } } }, false, moc)).ThanhCong);

        // Web vẫn cầm mốc cũ: phải báo xung đột (trước đây EF trả bản cũ đang theo dõi -> không phát hiện, ghi thì văng lỗi).
        var kq = await svcWeb.CapNhatPhieuAsync(new PhieuGhiNhan { Id = p.Id,
            Dong = { new DongGhiNhan { GiaTriJson = Json(("gio_bat", "09:00")) } } }, false, moc);
        Assert.True(kq.XungDot, kq.ThongBao);
        Assert.Contains("App", kq.ThongBao);
    }

    [Fact]
    public async Task Hoan_Thanh_Ghi_Chu_Ky_Ma_Bam_Va_Tra_Cuu_Phat_Hien_Sua_Sau_Khi_Ky()
    {
        var ky = new KyPhieuMayChu(System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256));
        HCP.Infrastructure.Services.TraCuu.TraCuuCongKhaiService TraCuu(AppDbContext db) => new(db, null, ky);
        await NapMauAsync();
        int id;
        using (var db = MoDb())
        {
            var uv = (await db.BieuMaus.FirstAsync(b => b.MaHieu == "BM-HD-SX-01-01")).Id;
            var p = new PhieuGhiNhan { BieuMauId = uv, Ngay = new DateOnly(2026, 10, 8), TenNguoiLap = "Nghĩa",
                                       GiaTriDauJson = Json(("khu_vuc", "Phòng đóng gói")),
                                       Dong = { new DongGhiNhan { GiaTriJson = Json(("gio_bat", "07:00")) } } };
            Assert.True((await new PhieuGhiNhanService(db).TaoPhieuAsync(p, hoanThanh: false)).ThanhCong);
            id = p.Id;
            Assert.Null(p.KyLucUtc);            // nháp: chưa ký, chưa có mã QR
            Assert.Null(p.MaTraCuu);
        }

        // Hoàn thành kèm chữ ký.
        using (var db = MoDb())
        {
            var kq = await new PhieuGhiNhanService(db, ky).CapNhatPhieuAsync(new PhieuGhiNhan
            {
                Id = id, GiaTriDauJson = Json(("khu_vuc", "Phòng đóng gói")), ChuKyAnh = "/uploads/coso-a/ky.png",
                TenNguoiKy = "Nguyễn Thị Nghĩa", NguoiKyUserId = "u-nghia",
                Dong = { new DongGhiNhan { GiaTriJson = Json(("gio_bat", "07:00"), ("den_hoat_dong", "Đạt")) } }
            }, hoanThanh: true);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        string ma, bam;
        using (var db = MoDb())
        {
            var p = await db.PhieuGhiNhans.Include(x => x.Dong).FirstAsync(x => x.Id == id);
            Assert.NotNull(p.KyLucUtc);
            Assert.Equal("Nguyễn Thị Nghĩa", p.TenNguoiKy);
            Assert.Equal("/uploads/coso-a/ky.png", p.ChuKyAnh);
            Assert.Matches("^[0-9a-f]{32}$", p.MaTraCuu!);
            Assert.Equal(PhieuGhiNhanService.TinhMaBam(p), p.MaBamNoiDung);   // đọc lại từ CSDL vẫn khớp
            Assert.True(ky.XacMinh(p.MaTraCuu!, p.MaBamNoiDung!, p.ChuKyMayChu));   // có chữ ký số máy chủ
            ma = p.MaTraCuu!;
            bam = p.MaBamNoiDung!;
        }

        // Trang tra cứu công khai (quét QR): nội dung khớp, có người ký + giá trị đã đổi ra chữ.
        using (var db = MoDb())
        {
            var tc = await TraCuu(db).TraCuuPhieuAsync(ma);
            Assert.NotNull(tc);
            Assert.True(tc!.NoiDungKhop);
            Assert.True(tc.ChuKyMayHopLe);
            Assert.Null(tc.PdfKhop);                                                   // QR cũ không mang mã băm
            Assert.True((await TraCuu(db).TraCuuPhieuAsync(ma, bam.ToUpperInvariant()))!.PdfKhop);
            Assert.False((await TraCuu(db).TraCuuPhieuAsync(ma, new string('0', 64)))!.PdfKhop);   // PDF bản khác
            Assert.True(tc.DaHoanThanh);
            Assert.Equal("Nguyễn Thị Nghĩa", tc.TenNguoiKy);
            Assert.Contains(tc.DauPhieu, g => g.GiaTri == "Phòng đóng gói");
            Assert.Contains(tc.Dong.Single().GiaTri, g => g.GiaTri == "07:00");
        }

        // Ai đó sửa thẳng dữ liệu trong CSDL sau khi ký -> tra cứu báo KHÔNG khớp.
        using (var db = MoDb())
        {
            var d = await db.DongGhiNhans.FirstAsync(x => x.PhieuGhiNhanId == id);
            d.GiaTriJson = Json(("gio_bat", "09:30"), ("den_hoat_dong", "Đạt"));
            await db.SaveChangesAsync();
        }
        using (var db = MoDb())
            Assert.False((await TraCuu(db).TraCuuPhieuAsync(ma))!.NoiDungKhop);

        // Sửa xong còn tính lại cả mã băm cho khớp -> vẫn bị lộ vì không có khoá để ký lại.
        using (var db = MoDb())
        {
            var p = await db.PhieuGhiNhans.Include(x => x.Dong).FirstAsync(x => x.Id == id);
            p.MaBamNoiDung = PhieuGhiNhanService.TinhMaBam(p);
            await db.SaveChangesAsync();
            bam = p.MaBamNoiDung;
        }
        using (var db = MoDb())
        {
            var tc = (await TraCuu(db).TraCuuPhieuAsync(ma))!;
            Assert.False(tc.NoiDungKhop);
            Assert.False(tc.ChuKyMayHopLe);
            // Không có khoá (dịch vụ tra cứu cũ) thì mã băm tự tính lại sẽ lọt - đúng lý do cần chữ ký máy chủ.
            Assert.True((await new HCP.Infrastructure.Services.TraCuu.TraCuuCongKhaiService(db).TraCuuPhieuAsync(ma))!.NoiDungKhop);
        }

        // Xoá luôn chữ ký máy chủ để giả làm phiếu cũ -> vẫn KHÔNG hợp lệ.
        using (var db = MoDb())
        {
            (await db.PhieuGhiNhans.FirstAsync(x => x.Id == id)).ChuKyMayChu = null;
            await db.SaveChangesAsync();
        }
        using (var db = MoDb())
            Assert.False((await TraCuu(db).TraCuuPhieuAsync(ma))!.NoiDungKhop);

        // Mã sai định dạng / không tồn tại -> null.
        using (var db = MoDb())
        {
            var svc = new HCP.Infrastructure.Services.TraCuu.TraCuuCongKhaiService(db);
            Assert.Null(await svc.TraCuuPhieuAsync("khong-hop-le"));
            Assert.Null(await svc.TraCuuPhieuAsync(new string('a', 32)));
        }
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

    [Fact]
    public void Chu_Ky_May_Chu_Gan_Voi_Ma_Tra_Cuu_Va_Khoa_Luu_File_Doc_Lai_Duoc()
    {
        var file = Path.Combine(Path.GetTempPath(), "hcp-test-" + Guid.NewGuid().ToString("N"), "khoa.pem");
        try
        {
            var ky = KyPhieuMayChu.TaiHoacTao(file);
            Assert.True(ky.CanKyBu);
            var bam = new string('a', 64);
            var chuKy = ky.Ky("ma-1", bam);

            var docLai = KyPhieuMayChu.TaiHoacTao(file);                    // khởi động lại: đọc đúng khoá cũ
            Assert.True(docLai.CanKyBu);                                      // chưa ghi dấu -> vẫn phải ký bù
            docLai.DanhDauDaKyBu();
            Assert.False(KyPhieuMayChu.TaiHoacTao(file).CanKyBu);
            Assert.True(docLai.XacMinh("ma-1", bam, chuKy));
            Assert.False(docLai.XacMinh("ma-2", bam, chuKy));               // chép chữ ký sang phiếu khác
            Assert.False(docLai.XacMinh("ma-1", new string('b', 64), chuKy));
            Assert.False(docLai.XacMinh("ma-1", bam, null));
            Assert.False(docLai.XacMinh("ma-1", bam, "khong-phai-base64!"));

            var khoaKhac = new KyPhieuMayChu(System.Security.Cryptography.ECDsa.Create(
                System.Security.Cryptography.ECCurve.NamedCurves.nistP256));
            Assert.False(khoaKhac.XacMinh("ma-1", bam, chuKy));             // khoá khác không xác minh được
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
        }
    }
}

