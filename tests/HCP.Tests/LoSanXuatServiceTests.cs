using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm tra dữ liệu Lô sản xuất theo đặc tả HanoiCheck v2.2 NGAY TRONG APP, để người dùng thấy lỗi lúc lưu
/// thay vì chờ HanoiCheck trả 422: album ảnh 1-3 ảnh, tệp minh chứng gắn đúng khâu, tối đa 3 tệp/khâu.
/// </summary>
public class LoSanXuatServiceTests
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

    private sealed class NoOpOutbox : ISyncOutboxWriter
    {
        public Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> DangBatAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task<string?> GuiAsync(object banGhi, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    private void Seed()
    {
        using var db = MoDb();
        // v2.7: thực phẩm của lô phải có quy trình mẫu ≥1 khâu -> SP01 gán QT01 có 1 khâu.
        db.Products.Add(new Product { MaSanPham = "SP01", TenSanPham = "Bánh", MaLoaiSp = "x",
                                      LoaiSanPham = LoaiSanPham.ThanhPham, MaQuyTrinh = "QT01" });
        db.ProductionProcesses.Add(new ProductionProcess { MaQuyTrinh = "QT01", TenQuyTrinh = "Quy trình bánh",
            DanhSachKhau = { new ProcessStepLine { MaKhau = "KHAU-0001", ThuTu = 1 } } });
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
        db.ProductionSteps.Add(new ProductionStep { MaKhau = "KHAU-0001", TenKhau = "Nướng" });
        db.SaveChanges();
    }

    private static BatchFile Anh(string ma, string duongDan = "https://app.vn/uploads/a.jpg") =>
        new() { MaFile = ma, TenFile = ma + ".jpg", DuongDan = duongDan, Loai = "HINH_ANH" };

    private static BatchFile Tep(string ma, string maBuoc, string duongDan = "https://app.vn/kd.pdf") =>
        new() { MaFile = ma, TenFile = ma, DuongDan = duongDan, Loai = "GIAY_KIEM_DICH", MaBuocSx = maBuoc };

    private static Batch Lo(params BatchFile[] file)
    {
        // v2.7: lô đồng bộ HnC bắt buộc ngày SX + hạn dùng + (địa chỉ thu mua hoặc NCC) -> điền sẵn để các test
        // tập trung kiểm tra file; test riêng bên dưới mới bỏ trống các trường này.
        var b = new Batch
        {
            MaSanPham = "SP01", TenLo = "Lô thử", NgayNhap = new DateOnly(2026, 9, 13),
            NgaySanXuat = new DateOnly(2026, 9, 13), HanSuDung = new DateOnly(2026, 9, 20), DiaChiThuMua = "Chợ HN",
            DanhSachKho = { new BatchWarehouse { MaKho = "KHO01" } },
            DanhSachKhau = { new BatchStep { MaBuocSx = "B1", MaKhau = "KHAU-0001", ThuTu = 1 } }
        };
        b.DanhSachFile.AddRange(file);
        return b;
    }

    private async Task<string> ThemLoiAsync(Batch b)
    {
        using var db = MoDb();
        var kq = await new LoSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db)).ThemAsync(b);
        Assert.False(kq.ThanhCong, "Lẽ ra phải bị chặn: " + kq.ThongBao);
        return kq.ThongBao;
    }

    [Fact]
    public async Task Bat_Buoc_1_Den_10_Anh_Chung_Cua_Lo()
    {
        Seed();
        Assert.Contains("từ 1 đến 10 ảnh", await ThemLoiAsync(Lo()));
        Assert.Contains("từ 1 đến 10 ảnh", await ThemLoiAsync(Lo(Tep("F1", "B1"))));   // chỉ có tệp khâu, không có ảnh lô
        // v2.7: nới lên 10 -> 11 ảnh mới bị chặn.
        var qua = Enumerable.Range(1, 11).Select(i => Anh("A" + i)).ToArray();
        Assert.Contains("từ 1 đến 10 ảnh", await ThemLoiAsync(Lo(qua)));
    }

    [Fact]
    public async Task Anh_Lo_Phai_La_Duong_Dan_Anh_Khong_Nhan_Google_Drive()
    {
        Seed();
        Assert.Contains("đường dẫn ảnh", await ThemLoiAsync(Lo(Anh("A1", "https://app.vn/giay.pdf"))));
        Assert.Contains("đường dẫn ảnh", await ThemLoiAsync(Lo(Anh("A1", "https://drive.google.com/file/d/abc/a.jpg"))));
    }

    [Fact]
    public async Task Tep_Minh_Chung_Phai_Gan_Dung_Khau_Dung_Dinh_Dang_Toi_Da_10()
    {
        Seed();
        Assert.Contains("không có trong các khâu", await ThemLoiAsync(Lo(Anh("A1"), Tep("F1", "B9"))));
        Assert.Contains("ảnh hoặc PDF", await ThemLoiAsync(Lo(Anh("A1"), Tep("F1", "B1", "https://app.vn/bang.xlsx"))));
        Assert.Contains("ảnh hoặc PDF", await ThemLoiAsync(Lo(Anh("A1"), Tep("F1", "B1", "https://app.vn/hd.docx"))));
        Assert.Contains("ảnh hoặc PDF", await ThemLoiAsync(
            Lo(Anh("A1"), Tep("F1", "B1", "https://drive.google.com/file/d/abc/view"))));
        // v2.7: nới lên 10 tệp/khâu -> 11 tệp mới bị chặn.
        var tep = new[] { Anh("A1") }.Concat(Enumerable.Range(1, 11).Select(i => Tep("F" + i, "B1"))).ToArray();
        Assert.Contains("tối đa 10 tệp", await ThemLoiAsync(Lo(tep)));
    }

    [Fact]
    public async Task Lo_Hop_Le_Duoc_Luu_Voi_Ma_Tu_Sinh()
    {
        Seed();
        var b = Lo(Anh("A1", "https://app.vn/uploads/a.webp?x=1"), Anh("A2"),
                   Tep("F1", "B1", "https://app.vn/kiem-dich.pdf"), Tep("F2", "B1", "https://app.vn/hd.png?v=2"));
        using var db = MoDb();
        var kq = await new LoSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db)).ThemAsync(b);

        Assert.True(kq.ThanhCong, kq.ThongBao);
        Assert.Equal("LO-20260913-001", b.MaLo);   // mã lô theo ngày sản xuất
    }

    [Fact]
    public async Task Ncc_Dau_Vao_Cua_Lo_Phai_Cung_Ung_Danh_Muc_Cua_Thuc_Pham()
    {
        Seed();   // SP01 thuộc danh mục "x"
        using (var db = MoDb())
        {
            // Cả hai NCC đều có hợp đồng còn hạn (v2.7) để test tập trung vào kiểm tra nhóm danh mục.
            db.SubSuppliers.Add(new SubSupplier { MaNccDauVao = "NCC-SAI", Ten = "NCC thịt",
                                                  HopDongSo = "HD-SAI", HopDongNgayHetHan = new DateOnly(2030, 1, 1),
                                                  NhomThucPham = { new SubSupplierFoodGroup { MaNhom = "969" } } });
            db.SubSuppliers.Add(new SubSupplier { MaNccDauVao = "NCC-DUNG", Ten = "NCC bánh",
                                                  HopDongSo = "HD-DUNG", HopDongNgayHetHan = new DateOnly(2030, 1, 1),
                                                  NhomThucPham = { new SubSupplierFoodGroup { MaNhom = "x" } } });
            db.SaveChanges();
        }

        var sai = Lo(Anh("A1"));
        sai.MaNccDauVao = "NCC-SAI";
        Assert.Contains("chưa khai cung ứng danh mục", await ThemLoiAsync(sai));

        var dung = Lo(Anh("A1"));
        dung.MaNccDauVao = "NCC-DUNG";
        using var db2 = MoDb();
        var kq = await new LoSanXuatService(db2, new NoOpOutbox(), new MaTuSinhService(db2)).ThemAsync(dung);
        Assert.True(kq.ThanhCong, kq.ThongBao);
    }

    // ===== v2.7 (A2): ràng buộc bắt buộc khi lô đồng bộ HanoiCheck =====

    [Fact]
    public async Task v27_Lo_Bat_Buoc_Ngay_San_Xuat_Va_Han_Dung()
    {
        Seed();
        var khongNsx = Lo(Anh("A1")); khongNsx.NgaySanXuat = null;
        Assert.Contains("ngày sản xuất", await ThemLoiAsync(khongNsx));

        var khongHsd = Lo(Anh("A1")); khongHsd.HanSuDung = null;
        Assert.Contains("hạn sử dụng", await ThemLoiAsync(khongHsd));

        var hsdTruocNsx = Lo(Anh("A1"));
        hsdTruocNsx.NgaySanXuat = new DateOnly(2026, 9, 20);
        hsdTruocNsx.HanSuDung = new DateOnly(2026, 9, 10);
        Assert.Contains("không được trước ngày sản xuất", await ThemLoiAsync(hsdTruocNsx));
    }

    [Fact]
    public async Task v27_Lo_Phai_Co_Dia_Chi_Thu_Mua_Hoac_Ncc()
    {
        Seed();
        var b = Lo(Anh("A1"));
        b.DiaChiThuMua = null;
        b.MaNccDauVao = null;
        Assert.Contains("địa chỉ thu mua hoặc nhà cung ứng", await ThemLoiAsync(b));
    }

    [Fact]
    public async Task v27_Thuc_Pham_Phai_Co_Quy_Trinh_Co_Khau()
    {
        Seed();
        using (var db = MoDb())   // SP02 không gán quy trình; SP03 gán quy trình rỗng khâu
        {
            db.Products.Add(new Product { MaSanPham = "SP02", TenSanPham = "Bánh 2", MaLoaiSp = "x",
                                          LoaiSanPham = LoaiSanPham.ThanhPham });
            db.Products.Add(new Product { MaSanPham = "SP03", TenSanPham = "Bánh 3", MaLoaiSp = "x",
                                          LoaiSanPham = LoaiSanPham.ThanhPham, MaQuyTrinh = "QT-RONG" });
            db.ProductionProcesses.Add(new ProductionProcess { MaQuyTrinh = "QT-RONG", TenQuyTrinh = "Rỗng" });
            db.SaveChanges();
        }

        var chuaGan = Lo(Anh("A1")); chuaGan.MaSanPham = "SP02";
        Assert.Contains("chưa gán quy trình", await ThemLoiAsync(chuaGan));

        var rongKhau = Lo(Anh("A1")); rongKhau.MaSanPham = "SP03";
        rongKhau.DanhSachKhau.Clear();   // quy trình QT-RONG không có khâu nào
        Assert.Contains("ít nhất 1 khâu", await ThemLoiAsync(rongKhau));
    }

    [Fact]
    public async Task v27_Ncc_Dau_Vao_Phai_Co_Hop_Dong_Con_Han()
    {
        Seed();
        using (var db = MoDb())
        {
            db.SubSuppliers.Add(new SubSupplier { MaNccDauVao = "NCC-KHD", Ten = "NCC không hợp đồng",
                                                  NhomThucPham = { new SubSupplierFoodGroup { MaNhom = "x" } } });
            db.SubSuppliers.Add(new SubSupplier { MaNccDauVao = "NCC-HET", Ten = "NCC hết hạn",
                                                  HopDongSo = "HD-HET", HopDongNgayHetHan = new DateOnly(2020, 1, 1),
                                                  NhomThucPham = { new SubSupplierFoodGroup { MaNhom = "x" } } });
            db.SaveChanges();
        }

        var khongHd = Lo(Anh("A1")); khongHd.DiaChiThuMua = null; khongHd.MaNccDauVao = "NCC-KHD";
        Assert.Contains("chưa có hợp đồng", await ThemLoiAsync(khongHd));

        var hetHan = Lo(Anh("A1")); hetHan.DiaChiThuMua = null; hetHan.MaNccDauVao = "NCC-HET";
        Assert.Contains("hợp đồng đã hết hạn", await ThemLoiAsync(hetHan));
    }
}
