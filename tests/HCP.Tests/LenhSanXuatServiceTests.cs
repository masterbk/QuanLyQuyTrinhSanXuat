using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.Kho;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng lệnh sản xuất: trừ nguyên liệu theo định mức đúng FEFO (lô hết hạn trước xuất
/// trước), cộng thành phẩm, chặn khi thiếu tồn và không cho thực hiện hai lần.
/// </summary>
public class LenhSanXuatServiceTests
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

    private static LenhSanXuatService Svc(AppDbContext db)
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);
        return new LenhSanXuatService(db, new SyncOutboxWriter(db, accessor));
    }

    /// <summary>Thành phẩm BANH_MI (định mức 0.1kg bột/cái), nguyên liệu BOT_MI, kho KHO01.</summary>
    private void SeedDanhMuc()
    {
        using var db = MoDb();
        var banh = new Product { MaSanPham = "BANH_MI", TenSanPham = "Bánh mì", MaLoaiSp = "x",
                                 LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái" };
        banh.DanhSachDinhMuc.Add(new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0.1m });
        db.Products.Add(banh);
        db.Products.Add(new Product { MaSanPham = "BOT_MI", TenSanPham = "Bột mì",
                                      LoaiSanPham = LoaiSanPham.NguyenLieu, DonViTinh = "kg" });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
        db.SaveChanges();
    }

    private void NhapBot(string lo, decimal sl, DateOnly hsd)
    {
        using var db = MoDb();
        db.KhoGiaoDichs.Add(new KhoGiaoDich
        {
            MaSanPham = "BOT_MI", MaKho = "KHO01", MaLo = lo, SoLuong = sl, HanSuDung = hsd,
            Loai = LoaiGiaoDichKho.NhapNguyenLieu, ThoiGianUtc = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private decimal TonLo(string maSp, string lo)
    {
        using var db = MoDb();
        return db.KhoGiaoDichs.Where(g => g.MaSanPham == maSp && g.MaLo == lo).Sum(g => (decimal?)g.SoLuong) ?? 0m;
    }

    private static LenhSanXuat Lenh(decimal sl) => new()
    {
        MaLenh = "LSX-001", MaThanhPham = "BANH_MI", SoLuong = sl, MaKho = "KHO01",
        MaLoThanhPham = "LOTP1", NgaySanXuat = new DateOnly(2026, 9, 6)
    };

    [Fact]
    public async Task Thuc_Hien_Tru_Nguyen_Lieu_FEFO_Va_Cong_Thanh_Pham()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 0.6m, new DateOnly(2026, 1, 1));  // hết hạn sớm -> xuất trước
        NhapBot("LO_B", 0.6m, new DateOnly(2026, 6, 1));

        int id;
        using (var db = MoDb())
        {
            var svc = Svc(db);
            var kq = await svc.TaoAsync(Lenh(10));   // cần 10 * 0.1 = 1.0 kg bột
            Assert.True(kq.ThanhCong, kq.ThongBao);
            id = (await db.LenhSanXuats.SingleAsync()).Id;
        }

        using (var db = MoDb())
        {
            var kq = await Svc(db).ThucHienAsync(id);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        // FEFO: LO_A (0.6, hết hạn sớm) hết sạch, LO_B trừ 0.4 còn 0.2.
        Assert.Equal(0m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(0.2m, TonLo("BOT_MI", "LO_B"));
        // Thành phẩm nhập kho 10 cái.
        Assert.Equal(10m, TonLo("BANH_MI", "LOTP1"));

        using (var db = MoDb())
        {
            var lenh = await db.LenhSanXuats.Include(l => l.TieuHao).SingleAsync();
            Assert.Equal(TrangThaiLenhSX.HoanThanh, lenh.TrangThai);
            Assert.Equal(2, lenh.TieuHao.Count); // tiêu hao 2 lô
            Assert.Equal(0.6m, lenh.TieuHao.Single(t => t.MaLo == "LO_A").SoLuong);
            Assert.Equal(0.4m, lenh.TieuHao.Single(t => t.MaLo == "LO_B").SoLuong);
        }
    }

    [Fact]
    public async Task Thieu_Nguyen_Lieu_Thi_Khong_Tru_Gi()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 0.5m, new DateOnly(2026, 1, 1)); // chỉ 0.5kg, cần 1.0kg

        int id;
        using (var db = MoDb())
        {
            id = (await Tao(db, 10)).Id;
        }

        using (var db = MoDb())
        {
            var kq = await Svc(db).ThucHienAsync(id);
            Assert.False(kq.ThanhCong);
            Assert.Contains("Không đủ", kq.ThongBao);
        }

        // Không phát sinh giao dịch nào ngoài dòng nhập ban đầu.
        Assert.Equal(0.5m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(0m, TonLo("BANH_MI", "LOTP1"));
        using (var db = MoDb())
            Assert.Equal(TrangThaiLenhSX.MoiTao, (await db.LenhSanXuats.SingleAsync()).TrangThai);
    }

    [Fact]
    public async Task Khong_Thuc_Hien_Hai_Lan()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 5m, new DateOnly(2026, 1, 1));

        int id;
        using (var db = MoDb()) id = (await Tao(db, 10)).Id;
        using (var db = MoDb()) Assert.True((await Svc(db).ThucHienAsync(id)).ThanhCong);
        using (var db = MoDb()) Assert.False((await Svc(db).ThucHienAsync(id)).ThanhCong);

        // Chỉ trừ 1 lần (10*0.1=1.0) -> còn 4.0.
        Assert.Equal(4.0m, TonLo("BOT_MI", "LO_A"));
    }

    private static async Task<LenhSanXuat> Tao(AppDbContext db, decimal sl)
    {
        await Svc(db).TaoAsync(Lenh(sl));
        return await db.LenhSanXuats.OrderByDescending(l => l.Id).FirstAsync();
    }

    [Fact]
    public async Task Tao_Lo_Dong_Bo_Sinh_Batch_Va_Hang_Doi()
    {
        SeedDanhMuc();
        // Khâu + quy trình cho thành phẩm để sinh bước truy xuất.
        using (var db = MoDb())
        {
            db.ProductionSteps.Add(new ProductionStep { MaKhau = "KHAU01", TenKhau = "Phối trộn" });
            var qt = new ProductionProcess { MaQuyTrinh = "QT01", TenQuyTrinh = "Làm bánh" };
            qt.DanhSachKhau.Add(new ProcessStepLine { MaKhau = "KHAU01", ThuTu = 1 });
            db.ProductionProcesses.Add(qt);
            var banh = await db.Products.SingleAsync(p => p.MaSanPham == "BANH_MI");
            banh.MaQuyTrinh = "QT01";
            await db.SaveChangesAsync();
        }
        NhapBot("LO_A", 0.6m, new DateOnly(2026, 1, 1));
        NhapBot("LO_B", 0.6m, new DateOnly(2026, 6, 1));

        int id;
        using (var db = MoDb())
        {
            var lenh = Lenh(10);
            lenh.TaoLoDongBo = true;
            await Svc(db).TaoAsync(lenh);
            id = (await db.LenhSanXuats.SingleAsync()).Id;
        }

        using (var db = MoDb())
            Assert.True((await Svc(db).ThucHienAsync(id)).ThanhCong);

        using (var db = MoDb())
        {
            var lenh = await db.LenhSanXuats.SingleAsync();
            Assert.Equal("LOTP1", lenh.MaLoDaTao);

            var batch = await db.Batches
                .Include(b => b.DanhSachKho).Include(b => b.DanhSachKhau)
                .SingleAsync(b => b.MaLo == "LOTP1");
            Assert.Equal("BANH_MI", batch.MaSanPham);
            Assert.Single(batch.DanhSachKho);
            Assert.Equal("KHO01", batch.DanhSachKho[0].MaKho);
            // Một bước cho mỗi lô nguyên liệu tiêu hao (LO_A, LO_B), đều trỏ về lô thành phẩm.
            Assert.Equal(2, batch.DanhSachKhau.Count);
            Assert.All(batch.DanhSachKhau, s => Assert.Equal("LOTP1", s.MaLoSanXuat));
            Assert.All(batch.DanhSachKhau, s => Assert.Equal("KHAU01", s.MaKhau));
            Assert.Contains(batch.DanhSachKhau, s => s.MaLoNguyenLieu == "LO_A");
            Assert.Contains(batch.DanhSachKhau, s => s.MaLoNguyenLieu == "LO_B");

            // Đã đưa vào hàng đợi đồng bộ HanoiCheck.
            Assert.True(await db.SyncOutboxItems.AnyAsync(o => o.EntityType == "Batch" && o.EntityKey == "LOTP1"));
        }
    }

    [Fact]
    public async Task Hao_Hut_Phan_Tram_Lam_Tang_Luong_Tru()
    {
        // Định mức 0.1 kg/cái + hao hụt 10% -> làm 10 cái cần 10 × 0.1 × 1.1 = 1.1 kg.
        using (var db = MoDb())
        {
            var banh = new Product { MaSanPham = "BANH_MI", TenSanPham = "Bánh mì", MaLoaiSp = "x",
                                     LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái" };
            banh.DanhSachDinhMuc.Add(new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0.1m, HaoHutPhanTram = 10m });
            db.Products.Add(banh);
            db.Products.Add(new Product { MaSanPham = "BOT_MI", TenSanPham = "Bột mì",
                                          LoaiSanPham = LoaiSanPham.NguyenLieu, DonViTinh = "kg" });
            db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
            db.SaveChanges();
        }
        NhapBot("LO1", 2m, new DateOnly(2026, 1, 1));

        int id;
        using (var db = MoDb()) { await Svc(db).TaoAsync(Lenh(10)); id = (await db.LenhSanXuats.SingleAsync()).Id; }
        using (var db = MoDb()) Assert.True((await Svc(db).ThucHienAsync(id)).ThanhCong);

        // Trừ 1.1 kg -> còn 0.9.
        Assert.Equal(0.9m, TonLo("BOT_MI", "LO1"));
    }

    [Fact]
    public async Task Huy_Lenh_Da_Thuc_Hien_Tra_Lai_Nguyen_Lieu_Va_Thu_Hoi_Thanh_Pham()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 0.6m, new DateOnly(2026, 1, 1));
        NhapBot("LO_B", 0.6m, new DateOnly(2026, 6, 1));

        int id;
        using (var db = MoDb()) id = (await Tao(db, 10)).Id;      // cần 1.0 kg bột
        using (var db = MoDb()) Assert.True((await Svc(db).ThucHienAsync(id)).ThanhCong);

        using (var db = MoDb())
        {
            var kq = await Svc(db).HuyAsync(id, "Nhập nhầm số lượng");
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        // Tồn về đúng như trước khi sản xuất, đảo đúng từng lô nguyên liệu.
        Assert.Equal(0.6m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(0.6m, TonLo("BOT_MI", "LO_B"));
        Assert.Equal(0m, TonLo("BANH_MI", "LOTP1"));

        using (var db = MoDb())
        {
            var lenh = await db.LenhSanXuats.SingleAsync();
            Assert.Equal(TrangThaiLenhSX.DaHuy, lenh.TrangThai);
            Assert.Equal("Nhập nhầm số lượng", lenh.LyDoHuy);
            Assert.NotNull(lenh.ThoiGianHuyUtc);

            // Dữ liệu gốc được GIỮ LẠI (không xoá), chỉ thêm dòng đảo tương ứng.
            Assert.Equal(3, await db.KhoGiaoDichs.CountAsync(
                g => g.ChungTu == "LSX-001" && g.Loai != LoaiGiaoDichKho.HoanTacSanXuat));
            Assert.Equal(3, await db.KhoGiaoDichs.CountAsync(g => g.Loai == LoaiGiaoDichKho.HoanTacSanXuat));

            // Dòng đảo trả nguyên liệu giữ nguyên hạn dùng của lô để FEFO về sau không sai.
            var daoLoA = await db.KhoGiaoDichs.SingleAsync(
                g => g.Loai == LoaiGiaoDichKho.HoanTacSanXuat && g.MaLo == "LO_A");
            Assert.Equal(0.6m, daoLoA.SoLuong);
            Assert.Equal(new DateOnly(2026, 1, 1), daoLoA.HanSuDung);
        }
    }

    [Fact]
    public async Task Khong_Huy_Duoc_Khi_Thanh_Pham_Da_Ban_Bot()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        int id;
        using (var db = MoDb()) id = (await Tao(db, 10)).Id;
        using (var db = MoDb()) Assert.True((await Svc(db).ThucHienAsync(id)).ThanhCong);

        // Bán bớt 3 cái từ lô thành phẩm -> không thu hồi đủ 10 nữa.
        using (var db = MoDb())
        {
            db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = "BANH_MI", MaKho = "KHO01", MaLo = "LOTP1", SoLuong = -3m,
                Loai = LoaiGiaoDichKho.XuatBan, ThoiGianUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var db = MoDb())
        {
            var kq = await Svc(db).HuyAsync(id, "Thử huỷ");
            Assert.False(kq.ThanhCong);
            Assert.Contains("không thu hồi được", kq.ThongBao);
        }

        // Không đảo gì cả: tồn giữ nguyên, lệnh vẫn Hoàn thành.
        Assert.Equal(1.0m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(7m, TonLo("BANH_MI", "LOTP1"));
        using (var db = MoDb())
            Assert.Equal(TrangThaiLenhSX.HoanThanh, (await db.LenhSanXuats.SingleAsync()).TrangThai);
    }

    [Fact]
    public async Task Huy_Lenh_Xoa_Lo_Dong_Bo_Va_Go_Khoi_Hang_Doi_Khi_Chua_Gui()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        int id;
        using (var db = MoDb())
        {
            var lenh = Lenh(10);
            lenh.TaoLoDongBo = true;
            await Svc(db).TaoAsync(lenh);
            id = (await db.LenhSanXuats.SingleAsync()).Id;
        }
        using (var db = MoDb()) Assert.True((await Svc(db).ThucHienAsync(id)).ThanhCong);
        using (var db = MoDb()) Assert.True(await db.Batches.AnyAsync(b => b.MaLo == "LOTP1"));

        using (var db = MoDb())
        {
            var kq = await Svc(db).HuyAsync(id, "Hỏng mẻ bánh");
            Assert.True(kq.ThanhCong, kq.ThongBao);
            Assert.Contains("chưa gửi sang HanoiCheck", kq.ThongBao);
        }

        using (var db = MoDb())
        {
            Assert.False(await db.Batches.AnyAsync(b => b.MaLo == "LOTP1"));
            Assert.False(await db.SyncOutboxItems.AnyAsync(o => o.EntityType == "Batch" && o.EntityKey == "LOTP1"));
        }
    }

    [Fact]
    public async Task Huy_Lenh_Da_Gui_HnC_Van_Huy_Nhung_Canh_Bao()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        int id;
        using (var db = MoDb())
        {
            var lenh = Lenh(10);
            lenh.TaoLoDongBo = true;
            await Svc(db).TaoAsync(lenh);
            id = (await db.LenhSanXuats.SingleAsync()).Id;
        }
        using (var db = MoDb()) Assert.True((await Svc(db).ThucHienAsync(id)).ThanhCong);

        // Giả lập job nền đã gửi thành công sang HanoiCheck.
        using (var db = MoDb())
        {
            var item = await db.SyncOutboxItems.SingleAsync(o => o.EntityKey == "LOTP1");
            item.Status = SyncOutboxStatus.Success;
            await db.SaveChangesAsync();
        }

        using (var db = MoDb())
        {
            var kq = await Svc(db).HuyAsync(id, "Hỏng mẻ bánh");
            Assert.True(kq.ThanhCong, kq.ThongBao);
            Assert.Contains("CẢNH BÁO", kq.ThongBao);
        }

        using (var db = MoDb())
        {
            // Bản ghi hàng đợi đã gửi được GIỮ LẠI làm bằng chứng đã đẩy đi.
            Assert.True(await db.SyncOutboxItems.AnyAsync(o => o.EntityKey == "LOTP1"));
            Assert.Equal(TrangThaiLenhSX.DaHuy, (await db.LenhSanXuats.SingleAsync()).TrangThai);
        }
    }

    [Fact]
    public async Task Huy_Bat_Buoc_Ly_Do_Va_Khong_Huy_Lenh_Nhap()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        int id;
        using (var db = MoDb()) id = (await Tao(db, 10)).Id;

        // Lệnh còn nháp thì dùng Xoá, không huỷ.
        using (var db = MoDb())
        {
            var kq = await Svc(db).HuyAsync(id, "Lý do gì đó");
            Assert.False(kq.ThanhCong);
            Assert.Contains("Xoá", kq.ThongBao);
        }

        using (var db = MoDb()) Assert.True((await Svc(db).ThucHienAsync(id)).ThanhCong);

        // Thiếu lý do -> chặn.
        using (var db = MoDb()) Assert.False((await Svc(db).HuyAsync(id, "  ")).ThanhCong);
        using (var db = MoDb()) Assert.True((await Svc(db).HuyAsync(id, "Sai định mức")).ThanhCong);
        // Không huỷ hai lần (tránh đảo kho hai lượt).
        using (var db = MoDb()) Assert.False((await Svc(db).HuyAsync(id, "Lần hai")).ThanhCong);

        Assert.Equal(2m, TonLo("BOT_MI", "LO_A"));   // chỉ đảo đúng 1 lần
    }

    [Fact]
    public async Task Khong_Xoa_Duoc_Lenh_Da_Thuc_Hien_Va_Da_Huy()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        int id;
        using (var db = MoDb()) id = (await Tao(db, 10)).Id;
        using (var db = MoDb()) Assert.True((await Svc(db).ThucHienAsync(id)).ThanhCong);
        using (var db = MoDb()) Assert.False((await Svc(db).XoaAsync(id)).ThanhCong);

        using (var db = MoDb()) Assert.True((await Svc(db).HuyAsync(id, "Huỷ thử")).ThanhCong);
        using (var db = MoDb()) Assert.False((await Svc(db).XoaAsync(id)).ThanhCong);
        // Lệnh đã huỷ cũng không chạy lại được.
        using (var db = MoDb()) Assert.False((await Svc(db).ThucHienAsync(id)).ThanhCong);

        using (var db = MoDb()) Assert.Equal(1, await db.LenhSanXuats.CountAsync());
    }

    [Fact]
    public async Task Khong_Bat_Tao_Lo_Thi_Khong_Sinh_Batch()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        int id;
        using (var db = MoDb()) id = (await Tao(db, 10)).Id;   // TaoLoDongBo mặc định false
        using (var db = MoDb()) Assert.True((await Svc(db).ThucHienAsync(id)).ThanhCong);

        using (var db = MoDb())
        {
            Assert.False(await db.Batches.AnyAsync());
            Assert.False(await db.SyncOutboxItems.AnyAsync(o => o.EntityType == "Batch"));
        }
    }
}
