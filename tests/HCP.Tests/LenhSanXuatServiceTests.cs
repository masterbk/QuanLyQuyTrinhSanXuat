using HCP.Domain.Entities.Business;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services;
using HCP.Infrastructure.Services.Kho;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng lệnh sản xuất nhiều sản phẩm: mỗi sản phẩm là một lô có quy trình + khâu (người thực hiện,
/// cơ sở); trừ nguyên liệu CỘNG DỒN theo định mức đúng FEFO, cộng thành phẩm từng lô, chặn khi thiếu
/// tồn/thiếu ảnh/thiếu người thực hiện, huỷ bằng bút toán đảo.
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
        return new LenhSanXuatService(db, new SyncOutboxWriter(db, accessor), new MaTuSinhService(db));
    }

    /// <summary>
    /// Thành phẩm BANH_MI (0.1kg bột/cái) và BANH_NGOT (0.2kg bột/cái), nguyên liệu BOT_MI, kho KHO01,
    /// quy trình QT01 gồm 2 khâu (KHAU01 Phối trộn, KHAU02 Nướng), cơ sở CS01, nhân sự NS01 + NS02.
    /// </summary>
    private void SeedDanhMuc(decimal haoHut = 0m)
    {
        using var db = MoDb();
        var banh = new Product { MaSanPham = "BANH_MI", TenSanPham = "Bánh mì", MaLoaiSp = "x",
                                 LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái", MaQuyTrinh = "QT01" };
        banh.DanhSachDinhMuc.Add(new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0.1m, HaoHutPhanTram = haoHut });
        db.Products.Add(banh);
        var ngot = new Product { MaSanPham = "BANH_NGOT", TenSanPham = "Bánh ngọt", MaLoaiSp = "x",
                                 LoaiSanPham = LoaiSanPham.ThanhPham, DonViTinh = "cái", MaQuyTrinh = "QT01" };
        ngot.DanhSachDinhMuc.Add(new DinhMucNguyenLieu { MaNguyenLieu = "BOT_MI", SoLuong = 0.2m });
        db.Products.Add(ngot);
        db.Products.Add(new Product { MaSanPham = "BOT_MI", TenSanPham = "Bột mì",
                                      LoaiSanPham = LoaiSanPham.NguyenLieu, DonViTinh = "kg" });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });

        db.ProductionSteps.Add(new ProductionStep { MaKhau = "KHAU01", TenKhau = "Phối trộn" });
        db.ProductionSteps.Add(new ProductionStep { MaKhau = "KHAU02", TenKhau = "Nướng" });
        var qt = new ProductionProcess { MaQuyTrinh = "QT01", TenQuyTrinh = "Làm bánh" };
        qt.DanhSachKhau.Add(new ProcessStepLine { MaKhau = "KHAU01", ThuTu = 1 });
        qt.DanhSachKhau.Add(new ProcessStepLine { MaKhau = "KHAU02", ThuTu = 2 });
        db.ProductionProcesses.Add(qt);

        db.Facilities.Add(new Facility { MaCoSo = "CS01", TenCoSo = "Xưởng 1", DiaChi = "12 Láng Hạ" });
        db.Staff.Add(new Staff { MaNhanSu = "NS01", HoTen = "Thợ A" });
        db.Staff.Add(new Staff { MaNhanSu = "NS02", HoTen = "Thợ B" });
        // Cơ sở bật đồng bộ HanoiCheck - để kiểm cả phần sinh Lô sản xuất và hàng đợi.
        db.TenantHnCCredentials.Add(new TenantHnCCredential { TenantId = CoSo, BatDongBo = true });
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

    private static List<LenhSanXuatKhau> Khau(string nguoi = "NS01") => new()
    {
        new() { MaKhau = "KHAU01", ThuTu = 1, MaCoSo = "CS01", NguoiThucHienCsv = nguoi },
        new() { MaKhau = "KHAU02", ThuTu = 2, MaCoSo = "CS01", NguoiThucHienCsv = nguoi }
    };

    private static LenhSanXuatSanPham DongSp(string maTp, decimal sl, string loTp) => new()
    {
        MaThanhPham = maTp, SoLuong = sl, MaLoThanhPham = loTp, MaQuyTrinh = "QT01", Khau = Khau()
    };

    /// <summary>Lệnh một sản phẩm BANH_MI lô LOTP1.</summary>
    private static LenhSanXuat Lenh(decimal sl) => new()
    {
        MaLenh = "LSX-20260906-001", MaKho = "KHO01", NgaySanXuat = new DateOnly(2026, 9, 6),
        SanPham = new() { DongSp("BANH_MI", sl, "LO-20260906-001") }
    };

    /// <summary>Ảnh mẫu cho MỌI dòng sản phẩm của lệnh - lệnh nào cũng phải có ảnh từng lô mới hoàn thành được.</summary>
    private async Task<List<AnhTheoSanPham>> AnhAsync(int idLenh, int soAnh = 1)
    {
        using var db = MoDb();
        var ids = await db.LenhSanXuatSanPhams.Where(s => s.LenhSanXuatId == idLenh)
            .OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
        return ids.Select(id => new AnhTheoSanPham(id, Enumerable.Range(1, soAnh)
            .Select(i => new AnhLoSanXuat($"lo{id}-{i}.jpg", $"/uploads/coso-a/2026/09/{id}-{i}.jpg"))
            .ToList())).ToList();
    }

    private async Task<KetQuaThaoTac> HoanThanhAsync(int id)
    {
        var anh = await AnhAsync(id);
        using var db = MoDb();
        return await Svc(db).ThucHienAsync(id, anh);
    }

    private async Task<int> TaoAsync(LenhSanXuat lenh)
    {
        using var db = MoDb();
        var kq = await Svc(db).TaoAsync(lenh);
        Assert.True(kq.ThanhCong, kq.ThongBao);
        return lenh.Id;
    }

    // ==================== Trừ kho ====================

    [Fact]
    public async Task Thuc_Hien_Tru_Nguyen_Lieu_FEFO_Va_Cong_Thanh_Pham()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 0.6m, new DateOnly(2026, 1, 1));  // hết hạn sớm -> xuất trước
        NhapBot("LO_B", 0.6m, new DateOnly(2026, 6, 1));

        var id = await TaoAsync(Lenh(10));                 // cần 10 * 0.1 = 1.0 kg bột
        var kq = await HoanThanhAsync(id);
        Assert.True(kq.ThanhCong, kq.ThongBao);

        // FEFO: LO_A (0.6, hết hạn sớm) hết sạch, LO_B trừ 0.4 còn 0.2.
        Assert.Equal(0m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(0.2m, TonLo("BOT_MI", "LO_B"));
        Assert.Equal(10m, TonLo("BANH_MI", "LO-20260906-001"));

        using var db = MoDb();
        var lenh = await db.LenhSanXuats.Include(l => l.SanPham).ThenInclude(s => s.TieuHao).SingleAsync();
        Assert.Equal(TrangThaiLenhSX.HoanThanh, lenh.TrangThai);
        var tieuHao = lenh.SanPham.Single().TieuHao;
        Assert.Equal(2, tieuHao.Count);
        Assert.Equal(0.6m, tieuHao.Single(t => t.MaLo == "LO_A").SoLuong);
        Assert.Equal(0.4m, tieuHao.Single(t => t.MaLo == "LO_B").SoLuong);
    }

    [Fact]
    public async Task Thieu_Nguyen_Lieu_Thi_Khong_Tru_Gi()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 0.5m, new DateOnly(2026, 1, 1)); // chỉ 0.5kg, cần 1.0kg

        var id = await TaoAsync(Lenh(10));
        var kq = await HoanThanhAsync(id);
        Assert.False(kq.ThanhCong);
        Assert.Contains("Không đủ", kq.ThongBao);

        Assert.Equal(0.5m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(0m, TonLo("BANH_MI", "LO-20260906-001"));
        using var db = MoDb();
        Assert.Equal(TrangThaiLenhSX.MoiTao, (await db.LenhSanXuats.SingleAsync()).TrangThai);
    }

    [Fact]
    public async Task Khong_Thuc_Hien_Hai_Lan()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 5m, new DateOnly(2026, 1, 1));

        var id = await TaoAsync(Lenh(10));
        Assert.True((await HoanThanhAsync(id)).ThanhCong);
        Assert.False((await HoanThanhAsync(id)).ThanhCong);

        Assert.Equal(4.0m, TonLo("BOT_MI", "LO_A"));   // chỉ trừ 1 lần
    }

    [Fact]
    public async Task Hao_Hut_Phan_Tram_Lam_Tang_Luong_Tru()
    {
        // Định mức 0.1 kg/cái + hao hụt 10% -> làm 10 cái cần 10 × 0.1 × 1.1 = 1.1 kg.
        SeedDanhMuc(haoHut: 10m);
        NhapBot("LO1", 2m, new DateOnly(2026, 1, 1));

        var id = await TaoAsync(Lenh(10));
        Assert.True((await HoanThanhAsync(id)).ThanhCong);
        Assert.Equal(0.9m, TonLo("BOT_MI", "LO1"));
    }

    // ==================== Nhiều sản phẩm ====================

    [Fact]
    public async Task Nhieu_San_Pham_Cong_Don_Nguyen_Lieu_Truoc_Khi_So_Ton()
    {
        SeedDanhMuc();
        // BANH_MI 10 cần 1.0kg, BANH_NGOT 5 cần 1.0kg: riêng lẻ dòng nào cũng "đủ" với 1.5kg, tổng 2.0kg thì thiếu.
        NhapBot("LO_A", 1.5m, new DateOnly(2026, 1, 1));

        var lenh = Lenh(10);
        lenh.SanPham.Add(DongSp("BANH_NGOT", 5, "LO-20260906-002"));
        var id = await TaoAsync(lenh);

        using (var db = MoDb())
        {
            var xemTruoc = await Svc(db).TinhNguyenLieuCanAsync(
                new List<(string, decimal)> { ("BANH_MI", 10), ("BANH_NGOT", 5) }, "KHO01");
            var bot = Assert.Single(xemTruoc);
            Assert.Equal(2.0m, bot.Can);
            Assert.False(bot.Du);
        }

        var kq = await HoanThanhAsync(id);
        Assert.False(kq.ThanhCong);
        Assert.Contains("Không đủ", kq.ThongBao);
        Assert.Equal(1.5m, TonLo("BOT_MI", "LO_A"));
    }

    [Fact]
    public async Task Nhieu_San_Pham_Nhap_Tung_Lo_Va_Chia_Tieu_Hao_Theo_Nhu_Cau()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 1.2m, new DateOnly(2026, 1, 1));
        NhapBot("LO_B", 1.0m, new DateOnly(2026, 6, 1));

        var lenh = Lenh(10);                                  // 1.0kg
        lenh.SanPham.Add(DongSp("BANH_NGOT", 5, "LO-20260906-002"));    // 1.0kg
        lenh.TaoLoDongBo = true;
        var id = await TaoAsync(lenh);

        var kq = await HoanThanhAsync(id);
        Assert.True(kq.ThanhCong, kq.ThongBao);

        // Trừ gộp 2.0kg FEFO: LO_A hết 1.2, LO_B còn 0.2.
        Assert.Equal(0m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(0.2m, TonLo("BOT_MI", "LO_B"));
        Assert.Equal(10m, TonLo("BANH_MI", "LO-20260906-001"));
        Assert.Equal(5m, TonLo("BANH_NGOT", "LO-20260906-002"));

        using var db = MoDb();
        var sp = await db.LenhSanXuatSanPhams.Include(s => s.TieuHao).OrderBy(s => s.Id).ToListAsync();
        // Mỗi dòng cần một nửa tổng -> nhận một nửa từ mỗi lô nguyên liệu.
        Assert.All(sp, s => Assert.Equal(1.0m, s.TieuHao.Sum(t => t.SoLuong)));
        Assert.Equal(0.6m, sp[0].TieuHao.Single(t => t.MaLo == "LO_A").SoLuong);
        Assert.Equal(0.4m, sp[1].TieuHao.Single(t => t.MaLo == "LO_B").SoLuong);

        // Mỗi dòng sinh một Batch riêng.
        Assert.Equal("LO-20260906-001", sp[0].MaLoDaTao);
        Assert.Equal("LO-20260906-002", sp[1].MaLoDaTao);
        Assert.Equal(2, await db.Batches.CountAsync());
        Assert.Equal(2, await db.SyncOutboxItems.CountAsync(o => o.EntityType == "Batch"));
    }

    [Fact]
    public async Task Moi_San_Pham_Deu_Phai_Co_Anh()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 5m, new DateOnly(2026, 1, 1));

        var lenh = Lenh(10);
        lenh.SanPham.Add(DongSp("BANH_NGOT", 5, "LO-20260906-002"));
        var id = await TaoAsync(lenh);

        var anh = await AnhAsync(id);
        using (var db = MoDb())
        {
            var kq = await Svc(db).ThucHienAsync(id, anh.Take(1).ToList());   // chỉ có ảnh lô đầu
            Assert.False(kq.ThanhCong);
            Assert.Contains("LO-20260906-002", kq.ThongBao);
        }
        using (var db = MoDb())
        {
            // Ảnh chỉ có đường dẫn rỗng cũng không tính.
            var rong = anh.Select(a => new AnhTheoSanPham(a.SanPhamId, new List<AnhLoSanXuat> { new("a.jpg", "  ") })).ToList();
            Assert.False((await Svc(db).ThucHienAsync(id, rong)).ThanhCong);
        }

        Assert.Equal(5m, TonLo("BOT_MI", "LO_A"));
        using (var db = MoDb())
            Assert.Equal(TrangThaiLenhSX.MoiTao, (await db.LenhSanXuats.SingleAsync()).TrangThai);
    }

    // ==================== Quy trình / khâu ====================

    [Fact]
    public async Task Tao_Lenh_Bat_Buoc_Quy_Trinh_Khau_Nguoi_Va_Co_So()
    {
        SeedDanhMuc();
        using var db = MoDb();
        var svc = Svc(db);

        async Task<string> Loi(Action<LenhSanXuat> sua)
        {
            var l = Lenh(10);
            sua(l);
            var kq = await svc.TaoAsync(l);
            Assert.False(kq.ThanhCong);
            return kq.ThongBao;
        }

        Assert.Contains("ít nhất một sản phẩm", await Loi(l => l.SanPham.Clear()));
        Assert.Contains("quy trình", await Loi(l => l.SanPham[0].MaQuyTrinh = ""));
        Assert.Contains("Không tìm thấy quy trình", await Loi(l => l.SanPham[0].MaQuyTrinh = "QT_LA"));
        Assert.Contains("KHAU02", await Loi(l => l.SanPham[0].Khau.RemoveAt(1)));              // thiếu khâu của quy trình
        Assert.Contains("người thực hiện", await Loi(l => l.SanPham[0].Khau[1].NguoiThucHienCsv = " , "));
        Assert.Contains("cơ sở", await Loi(l => l.SanPham[0].Khau[0].MaCoSo = ""));
        Assert.Contains("CS_LA", await Loi(l => l.SanPham[0].Khau[0].MaCoSo = "CS_LA"));
        Assert.Contains("NS_LA", await Loi(l => l.SanPham[0].Khau[0].NguoiThucHienCsv = "NS01,NS_LA"));
        Assert.Contains("không phải thành phẩm", await Loi(l => l.SanPham[0].MaThanhPham = "BOT_MI"));

        Assert.False(await db.LenhSanXuats.AnyAsync());

        // Hợp lệ: nhiều người, có khoảng trắng -> được chuẩn hoá.
        var ok = Lenh(10);
        ok.SanPham[0].Khau[0].NguoiThucHienCsv = " NS01 , NS02 ";
        Assert.True((await svc.TaoAsync(ok)).ThanhCong);
        Assert.Equal("NS01,NS02", (await db.LenhSanXuatKhaus.SingleAsync(k => k.MaKhau == "KHAU01")).NguoiThucHienCsv);
    }

    [Fact]
    public async Task Tao_Lo_Dong_Bo_Moi_Khau_La_Mot_Buoc_Kem_Nguoi_Thuc_Hien()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 0.6m, new DateOnly(2026, 1, 1));
        NhapBot("LO_B", 0.6m, new DateOnly(2026, 6, 1));

        var lenh = Lenh(10);
        lenh.TaoLoDongBo = true;
        lenh.SanPham[0].Khau[1].NguoiThucHienCsv = "NS02";
        var id = await TaoAsync(lenh);
        Assert.True((await HoanThanhAsync(id)).ThanhCong);

        using var db = MoDb();
        var batch = await db.Batches
            .Include(b => b.DanhSachKho).Include(b => b.DanhSachKhau).Include(b => b.DanhSachFile)
            .SingleAsync(b => b.MaLo == "LO-20260906-001");
        Assert.Equal("BANH_MI", batch.MaSanPham);
        Assert.Equal("CS01", batch.MaCoSo);
        Assert.Equal("KHO01", Assert.Single(batch.DanhSachKho).MaKho);

        var buoc = batch.DanhSachKhau.OrderBy(b => b.ThuTu).ToList();
        Assert.Equal(new[] { "KHAU01", "KHAU02" }, buoc.Select(b => b.MaKhau));
        Assert.Equal("NS01", buoc[0].NguoiThucHienCsv);
        Assert.Equal("NS02", buoc[1].NguoiThucHienCsv);
        Assert.All(buoc, b => Assert.Equal("LO-20260906-001", b.MaLoSanXuat));
        Assert.All(buoc, b => Assert.Equal("12 Láng Hạ", b.DiaChi));
        // Khâu đầu truy ngược về các lô nguyên liệu đã tiêu hao.
        Assert.Contains("LO_A", buoc[0].MaLoNguyenLieu);
        Assert.Contains("LO_B", buoc[0].MaLoNguyenLieu);

        // Ảnh lô thành file của Batch, mã file đánh theo mã lô thành phẩm.
        var file = Assert.Single(batch.DanhSachFile);
        Assert.Equal("LO-20260906-001-A1", file.MaFile);
        Assert.Equal("HINH_ANH", file.Loai);
        Assert.Equal("KHAU01", file.MaKhau);

        Assert.True(await db.SyncOutboxItems.AnyAsync(o => o.EntityType == "Batch" && o.EntityKey == "LO-20260906-001"));
    }

    [Fact]
    public async Task Sua_Nguoi_Thuc_Hien_Luc_Hoan_Thanh()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 5m, new DateOnly(2026, 1, 1));

        var lenh = Lenh(10);
        lenh.TaoLoDongBo = true;
        var id = await TaoAsync(lenh);
        var anh = await AnhAsync(id);

        List<LenhSanXuatKhau> khauLuu;
        using (var db = MoDb()) khauLuu = await db.LenhSanXuatKhaus.AsNoTracking().OrderBy(k => k.ThuTu).ToListAsync();

        // Sửa sai (bỏ trống người) -> chặn, không động vào kho.
        using (var db = MoDb())
        {
            var sai = new List<LenhSanXuatKhau> { new() { Id = khauLuu[1].Id, MaCoSo = "CS01", NguoiThucHienCsv = "" } };
            var kq = await Svc(db).ThucHienAsync(id, anh, sai);
            Assert.False(kq.ThanhCong);
            Assert.Contains("người thực hiện", kq.ThongBao);
        }
        Assert.Equal(5m, TonLo("BOT_MI", "LO_A"));

        // Sửa đúng: khâu 2 do NS02 làm.
        using (var db = MoDb())
        {
            var dung = new List<LenhSanXuatKhau> { new() { Id = khauLuu[1].Id, MaCoSo = "CS01", NguoiThucHienCsv = "NS02" } };
            var kq = await Svc(db).ThucHienAsync(id, anh, dung);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        using (var db = MoDb())
        {
            Assert.Equal("NS02", (await db.LenhSanXuatKhaus.SingleAsync(k => k.Id == khauLuu[1].Id)).NguoiThucHienCsv);
            Assert.Equal("NS01", (await db.LenhSanXuatKhaus.SingleAsync(k => k.Id == khauLuu[0].Id)).NguoiThucHienCsv);
            var buoc2 = await db.BatchSteps.SingleAsync(b => b.MaKhau == "KHAU02");
            Assert.Equal("NS02", buoc2.NguoiThucHienCsv);
        }
    }

    // ==================== Huỷ / xoá ====================

    [Fact]
    public async Task Huy_Lenh_Da_Thuc_Hien_Tra_Lai_Nguyen_Lieu_Va_Thu_Hoi_Moi_Lo()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 1.2m, new DateOnly(2026, 1, 1));
        NhapBot("LO_B", 1.0m, new DateOnly(2026, 6, 1));

        var lenh = Lenh(10);
        lenh.SanPham.Add(DongSp("BANH_NGOT", 5, "LO-20260906-002"));
        var id = await TaoAsync(lenh);
        Assert.True((await HoanThanhAsync(id)).ThanhCong);

        using (var db = MoDb())
        {
            var kq = await Svc(db).HuyAsync(id, "Nhập nhầm số lượng");
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        Assert.Equal(1.2m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(1.0m, TonLo("BOT_MI", "LO_B"));
        Assert.Equal(0m, TonLo("BANH_MI", "LO-20260906-001"));
        Assert.Equal(0m, TonLo("BANH_NGOT", "LO-20260906-002"));

        using var db2 = MoDb();
        var l = await db2.LenhSanXuats.SingleAsync();
        Assert.Equal(TrangThaiLenhSX.DaHuy, l.TrangThai);
        Assert.Equal("Nhập nhầm số lượng", l.LyDoHuy);
        Assert.NotNull(l.ThoiGianHuyUtc);

        // Dữ liệu gốc GIỮ LẠI: 2 dòng xuất nguyên liệu + 2 dòng nhập thành phẩm, và đúng 4 dòng đảo.
        Assert.Equal(4, await db2.KhoGiaoDichs.CountAsync(
            g => g.ChungTu == "LSX-20260906-001" && g.Loai != LoaiGiaoDichKho.HoanTacSanXuat));
        Assert.Equal(4, await db2.KhoGiaoDichs.CountAsync(g => g.Loai == LoaiGiaoDichKho.HoanTacSanXuat));
        var daoLoA = await db2.KhoGiaoDichs.SingleAsync(
            g => g.Loai == LoaiGiaoDichKho.HoanTacSanXuat && g.MaLo == "LO_A");
        Assert.Equal(new DateOnly(2026, 1, 1), daoLoA.HanSuDung);
    }

    [Fact]
    public async Task Khong_Huy_Duoc_Khi_Mot_Lo_Thanh_Pham_Da_Ban_Bot()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 5m, new DateOnly(2026, 1, 1));

        var lenh = Lenh(10);
        lenh.SanPham.Add(DongSp("BANH_NGOT", 5, "LO-20260906-002"));
        var id = await TaoAsync(lenh);
        Assert.True((await HoanThanhAsync(id)).ThanhCong);

        // Bán bớt 3 cái từ lô thứ HAI -> cả lệnh không thu hồi được.
        using (var db = MoDb())
        {
            db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = "BANH_NGOT", MaKho = "KHO01", MaLo = "LO-20260906-002", SoLuong = -3m,
                Loai = LoaiGiaoDichKho.XuatBan, ThoiGianUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var db = MoDb())
        {
            var kq = await Svc(db).HuyAsync(id, "Thử huỷ");
            Assert.False(kq.ThanhCong);
            Assert.Contains("LO-20260906-002", kq.ThongBao);
            Assert.Contains("không thu hồi được", kq.ThongBao);
        }

        Assert.Equal(3m, TonLo("BOT_MI", "LO_A"));
        Assert.Equal(10m, TonLo("BANH_MI", "LO-20260906-001"));
        using (var db = MoDb())
            Assert.Equal(TrangThaiLenhSX.HoanThanh, (await db.LenhSanXuats.SingleAsync()).TrangThai);
    }

    [Fact]
    public async Task Huy_Lenh_Xoa_Lo_Dong_Bo_Va_Go_Khoi_Hang_Doi_Khi_Chua_Gui()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        var lenh = Lenh(10);
        lenh.TaoLoDongBo = true;
        var id = await TaoAsync(lenh);
        Assert.True((await HoanThanhAsync(id)).ThanhCong);
        using (var db = MoDb()) Assert.True(await db.Batches.AnyAsync(b => b.MaLo == "LO-20260906-001"));

        using (var db = MoDb())
        {
            var kq = await Svc(db).HuyAsync(id, "Hỏng mẻ bánh");
            Assert.True(kq.ThanhCong, kq.ThongBao);
            Assert.Contains("chưa gửi sang HanoiCheck", kq.ThongBao);
        }

        using (var db = MoDb())
        {
            Assert.False(await db.Batches.AnyAsync(b => b.MaLo == "LO-20260906-001"));
            Assert.False(await db.SyncOutboxItems.AnyAsync(o => o.EntityType == "Batch" && o.EntityKey == "LO-20260906-001"));
        }
    }

    [Fact]
    public async Task Huy_Lenh_Da_Gui_HnC_Van_Huy_Nhung_Canh_Bao()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        var lenh = Lenh(10);
        lenh.TaoLoDongBo = true;
        var id = await TaoAsync(lenh);
        Assert.True((await HoanThanhAsync(id)).ThanhCong);

        using (var db = MoDb())
        {
            var item = await db.SyncOutboxItems.SingleAsync(o => o.EntityKey == "LO-20260906-001");
            item.Status = SyncOutboxStatus.Success;
            await db.SaveChangesAsync();
        }

        using (var db = MoDb())
        {
            var kq = await Svc(db).HuyAsync(id, "Hỏng mẻ bánh");
            Assert.True(kq.ThanhCong, kq.ThongBao);
            Assert.Contains("CẢNH BÁO", kq.ThongBao);
            Assert.DoesNotContain("chưa gửi", kq.ThongBao);
        }

        using (var db = MoDb())
        {
            Assert.True(await db.SyncOutboxItems.AnyAsync(o => o.EntityKey == "LO-20260906-001"));
            Assert.Equal(TrangThaiLenhSX.DaHuy, (await db.LenhSanXuats.SingleAsync()).TrangThai);
        }
    }

    [Fact]
    public async Task Huy_Bat_Buoc_Ly_Do_Va_Khong_Huy_Lenh_Nhap()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        var id = await TaoAsync(Lenh(10));
        using (var db = MoDb())
        {
            var kq = await Svc(db).HuyAsync(id, "Lý do gì đó");
            Assert.False(kq.ThanhCong);
            Assert.Contains("Xoá", kq.ThongBao);
        }

        Assert.True((await HoanThanhAsync(id)).ThanhCong);
        using (var db = MoDb()) Assert.False((await Svc(db).HuyAsync(id, "  ")).ThanhCong);
        using (var db = MoDb()) Assert.True((await Svc(db).HuyAsync(id, "Sai định mức")).ThanhCong);
        using (var db = MoDb()) Assert.False((await Svc(db).HuyAsync(id, "Lần hai")).ThanhCong);

        Assert.Equal(2m, TonLo("BOT_MI", "LO_A"));   // chỉ đảo đúng 1 lần
    }

    [Fact]
    public async Task Khong_Xoa_Duoc_Lenh_Da_Thuc_Hien_Va_Da_Huy()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        var id = await TaoAsync(Lenh(10));
        Assert.True((await HoanThanhAsync(id)).ThanhCong);
        using (var db = MoDb()) Assert.False((await Svc(db).XoaAsync(id)).ThanhCong);

        using (var db = MoDb()) Assert.True((await Svc(db).HuyAsync(id, "Huỷ thử")).ThanhCong);
        using (var db = MoDb()) Assert.False((await Svc(db).XoaAsync(id)).ThanhCong);
        Assert.False((await HoanThanhAsync(id)).ThanhCong);

        using (var db = MoDb()) Assert.Equal(1, await db.LenhSanXuats.CountAsync());
    }

    [Fact]
    public async Task Xoa_Lenh_Nhap_Xoa_Luon_San_Pham_Va_Khau()
    {
        SeedDanhMuc();
        var lenh = Lenh(10);
        lenh.SanPham.Add(DongSp("BANH_NGOT", 5, "LO-20260906-002"));
        var id = await TaoAsync(lenh);

        using (var db = MoDb()) Assert.True((await Svc(db).XoaAsync(id)).ThanhCong);
        using (var db = MoDb())
        {
            Assert.False(await db.LenhSanXuatSanPhams.AnyAsync());
            Assert.False(await db.LenhSanXuatKhaus.AnyAsync());
        }
    }

    // ==================== Mã tự sinh ====================

    [Fact]
    public async Task Tao_Lenh_Tu_Sinh_Ma_Lenh_Va_Ma_Lo_Theo_Ngay_San_Xuat()
    {
        SeedDanhMuc();

        var lenh = Lenh(10);
        lenh.MaLenh = "GO-TAY";                                // mã gửi lên bị bỏ qua
        lenh.SanPham[0].MaLoThanhPham = "LO-GO-TAY";
        lenh.SanPham.Add(DongSp("BANH_NGOT", 5, "LO-GO-TAY-2"));
        await TaoAsync(lenh);

        var lenh2 = Lenh(3);
        lenh2.NgaySanXuat = new DateOnly(2026, 9, 7);
        await TaoAsync(lenh2);

        using var db = MoDb();
        var ds = await db.LenhSanXuats.Include(l => l.SanPham).OrderBy(l => l.Id).ToListAsync();
        Assert.Equal("LSX-20260906-001", ds[0].MaLenh);
        Assert.Equal(new[] { "LO-20260906-001", "LO-20260906-002" },
                     ds[0].SanPham.OrderBy(x => x.Id).Select(x => x.MaLoThanhPham));
        // Ngày khác -> dãy số riêng của ngày đó.
        Assert.Equal("LSX-20260907-001", ds[1].MaLenh);
        Assert.Equal("LO-20260907-001", ds[1].SanPham.Single().MaLoThanhPham);
    }

    // ==================== Sửa ====================

    private const string Lo1 = "LO-20260906-001";

    /// <summary>Bản sửa gửi từ màn hình: đối tượng MỚI mang Id, giống cách UI làm (không sửa entity đang theo dõi).</summary>
    private static LenhSanXuat BanSua(int id, decimal sl, string loTp = Lo1) => new()
    {
        Id = id, MaLenh = "MA-GUI-LEN", MaKho = "KHO01", NgaySanXuat = new DateOnly(2026, 9, 12),
        GhiChu = "  sửa lại  ", TaoLoDongBo = true,
        SanPham = new() { DongSp("BANH_MI", sl, loTp) }
    };

    [Fact]
    public async Task Sua_Lenh_Giu_Ma_Lenh_Giu_Ma_Lo_Cu_Va_Cap_Ma_Cho_Dong_Moi()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 5m, new DateOnly(2026, 1, 1));

        var lenhGoc = Lenh(10);
        lenhGoc.SanPham.Add(DongSp("BANH_NGOT", 5, ""));
        var id = await TaoAsync(lenhGoc);                      // LO-20260906-001, -002

        using (var db = MoDb())
        {
            // Giữ dòng lô 001 (số lượng 20), bỏ dòng 002, thêm một dòng mới (mã trống).
            var ban = BanSua(id, 20m);
            ban.SanPham.Add(DongSp("BANH_MI", 1, ""));
            var kq = await Svc(db).CapNhatAsync(ban);
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        using (var db = MoDb())
        {
            var lenh = await db.LenhSanXuats.Include(l => l.SanPham).SingleAsync();
            Assert.Equal("LSX-20260906-001", lenh.MaLenh);     // mã lệnh không đổi
            Assert.Equal(new DateOnly(2026, 9, 12), lenh.NgaySanXuat);
            Assert.Equal("sửa lại", lenh.GhiChu);
            Assert.True(lenh.TaoLoDongBo);
            var sp = lenh.SanPham.OrderBy(x => x.Id).ToList();
            Assert.Equal(2, sp.Count);
            Assert.Equal(Lo1, sp[0].MaLoThanhPham);
            Assert.Equal(20m, sp[0].SoLuong);
            // Dòng mới lấy ngày sản xuất đã sửa.
            Assert.Equal("LO-20260912-001", sp[1].MaLoThanhPham);
            Assert.Equal(4, await db.LenhSanXuatKhaus.CountAsync());
        }

        Assert.Equal(5m, TonLo("BOT_MI", "LO_A"));
        Assert.True((await HoanThanhAsync(id)).ThanhCong);
        Assert.Equal(2.9m, TonLo("BOT_MI", "LO_A"));            // (20 + 1) × 0.1
        Assert.Equal(20m, TonLo("BANH_MI", Lo1));
    }

    [Fact]
    public async Task Khong_Sua_Duoc_Lenh_Da_Hoan_Thanh_Hoac_Da_Huy()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 5m, new DateOnly(2026, 1, 1));

        var id = await TaoAsync(Lenh(10));
        Assert.True((await HoanThanhAsync(id)).ThanhCong);

        using (var db = MoDb())
        {
            var kq = await Svc(db).CapNhatAsync(BanSua(id, 99m));
            Assert.False(kq.ThanhCong);
            Assert.Contains("chưa hoàn thành", kq.ThongBao);
        }
        using (var db = MoDb()) Assert.True((await Svc(db).HuyAsync(id, "thử")).ThanhCong);
        using (var db = MoDb()) Assert.False((await Svc(db).CapNhatAsync(BanSua(id, 99m))).ThanhCong);

        using (var db = MoDb()) Assert.Equal(10m, (await db.LenhSanXuatSanPhams.SingleAsync()).SoLuong);
    }

    [Fact]
    public async Task Sua_Kiem_Tra_Du_Lieu_Nhu_Luc_Tao_Va_Khong_Cho_Doi_Ma_Lo()
    {
        SeedDanhMuc();
        var id = await TaoAsync(Lenh(10));                    // LSX-20260906-001 / LO-20260906-001
        await TaoAsync(Lenh(5));                              // LSX-20260906-002 / LO-20260906-002

        using (var db = MoDb())
        {
            var svc = Svc(db);
            // Mã lô gõ tay hoặc mượn mã lô của lệnh KHÁC -> chặn.
            Assert.Contains("không sửa được", (await svc.CapNhatAsync(BanSua(id, 10m, "LOTP-MOI"))).ThongBao);
            Assert.Contains("không sửa được", (await svc.CapNhatAsync(BanSua(id, 10m, "LO-20260906-002"))).ThongBao);
            // Hai dòng cùng mã lô cũ -> chặn.
            var trung = BanSua(id, 10m); trung.SanPham.Add(DongSp("BANH_NGOT", 1, Lo1.ToLowerInvariant()));
            Assert.Contains("bị lặp", (await svc.CapNhatAsync(trung)).ThongBao);
            Assert.True((await svc.CapNhatAsync(BanSua(id, 12m))).ThanhCong);
        }
        using (var db = MoDb())
        {
            var svc = Svc(db);
            Assert.False((await svc.CapNhatAsync(BanSua(id, 0m))).ThanhCong);
            var thieuNguoi = BanSua(id, 10m); thieuNguoi.SanPham[0].Khau[0].NguoiThucHienCsv = "";
            Assert.False((await svc.CapNhatAsync(thieuNguoi)).ThanhCong);
            Assert.False((await svc.CapNhatAsync(BanSua(9999, 10m))).ThanhCong);
        }

        using (var db = MoDb())
        {
            var sp = await db.LenhSanXuatSanPhams.SingleAsync(x => x.LenhSanXuatId == id);
            Assert.Equal(12m, sp.SoLuong);                    // chỉ lần hợp lệ được lưu
            Assert.Equal(Lo1, sp.MaLoThanhPham);
        }
    }

    [Fact]
    public async Task Khong_Bat_Tao_Lo_Thi_Khong_Sinh_Batch()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 2m, new DateOnly(2026, 1, 1));

        var id = await TaoAsync(Lenh(10));   // TaoLoDongBo mặc định false
        Assert.True((await HoanThanhAsync(id)).ThanhCong);

        using var db = MoDb();
        Assert.False(await db.Batches.AnyAsync());
        Assert.False(await db.SyncOutboxItems.AnyAsync(o => o.EntityType == "Batch"));
        // Ảnh vẫn được lưu theo từng dòng sản phẩm.
        Assert.Equal("LO-20260906-001-A1", (await db.LenhSanXuatAnhs.SingleAsync()).MaFile);
    }

    [Fact]
    public async Task Moi_Lo_Toi_Da_3_Anh_Theo_Gioi_Han_HanoiCheck()
    {
        SeedDanhMuc();
        NhapBot("LO_A", 5m, new DateOnly(2026, 1, 1));
        var lenhAnh = Lenh(10);
        lenhAnh.TaoLoDongBo = true;          // giới hạn album chỉ áp khi lệnh tạo Lô đồng bộ
        var id = await TaoAsync(lenhAnh);

        var bonAnh = await AnhAsync(id, soAnh: 4);
        using (var db = MoDb())
        {
            var kq = await Svc(db).ThucHienAsync(id, bonAnh);
            Assert.False(kq.ThanhCong);
            Assert.Contains("tối đa 3 ảnh", kq.ThongBao);
        }
        Assert.Equal(5m, TonLo("BOT_MI", "LO_A"));              // chưa động vào kho

        var baAnh = await AnhAsync(id, soAnh: 3);
        using (var db = MoDb()) Assert.True((await Svc(db).ThucHienAsync(id, baAnh)).ThanhCong);
    }
}
