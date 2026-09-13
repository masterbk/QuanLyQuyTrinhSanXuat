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
        db.Products.Add(new Product { MaSanPham = "SP01", TenSanPham = "Bánh", MaLoaiSp = "x", LoaiSanPham = LoaiSanPham.ThanhPham });
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
        var b = new Batch
        {
            MaSanPham = "SP01", TenLo = "Lô thử", NgayNhap = new DateOnly(2026, 9, 13),
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
    public async Task Bat_Buoc_1_Den_3_Anh_Chung_Cua_Lo()
    {
        Seed();
        Assert.Contains("từ 1 đến 3 ảnh", await ThemLoiAsync(Lo()));
        Assert.Contains("từ 1 đến 3 ảnh", await ThemLoiAsync(Lo(Tep("F1", "B1"))));   // chỉ có tệp khâu, không có ảnh lô
        Assert.Contains("từ 1 đến 3 ảnh", await ThemLoiAsync(Lo(Anh("A1"), Anh("A2"), Anh("A3"), Anh("A4"))));
    }

    [Fact]
    public async Task Anh_Lo_Phai_La_Duong_Dan_Anh_Khong_Nhan_Google_Drive()
    {
        Seed();
        Assert.Contains("đường dẫn ảnh", await ThemLoiAsync(Lo(Anh("A1", "https://app.vn/giay.pdf"))));
        Assert.Contains("đường dẫn ảnh", await ThemLoiAsync(Lo(Anh("A1", "https://drive.google.com/file/d/abc/a.jpg"))));
    }

    [Fact]
    public async Task Tep_Minh_Chung_Phai_Gan_Dung_Khau_Dung_Dinh_Dang_Toi_Da_3()
    {
        Seed();
        Assert.Contains("không có trong các khâu", await ThemLoiAsync(Lo(Anh("A1"), Tep("F1", "B9"))));
        Assert.Contains("ảnh, PDF, Word", await ThemLoiAsync(Lo(Anh("A1"), Tep("F1", "B1", "https://app.vn/bang.xlsx"))));
        Assert.Contains("tối đa 3 tệp", await ThemLoiAsync(
            Lo(Anh("A1"), Tep("F1", "B1"), Tep("F2", "B1"), Tep("F3", "B1"), Tep("F4", "B1"))));
    }

    [Fact]
    public async Task Lo_Hop_Le_Duoc_Luu_Voi_Ma_Tu_Sinh()
    {
        Seed();
        var b = Lo(Anh("A1", "https://app.vn/uploads/a.webp?x=1"), Anh("A2"),
                   Tep("F1", "B1", "https://drive.google.com/file/d/abc/view"), Tep("F2", "B1", "https://app.vn/hd.docx"));
        using var db = MoDb();
        var kq = await new LoSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db)).ThemAsync(b);

        Assert.True(kq.ThanhCong, kq.ThongBao);
        Assert.Equal("LO-20260913-001", b.MaLo);   // chưa có ngày SX -> theo ngày nhập
    }

    [Fact]
    public async Task Ncc_Dau_Vao_Cua_Lo_Phai_Cung_Ung_Danh_Muc_Cua_Thuc_Pham()
    {
        Seed();   // SP01 thuộc danh mục "x"
        using (var db = MoDb())
        {
            db.SubSuppliers.Add(new SubSupplier { MaNccDauVao = "NCC-SAI", Ten = "NCC thịt",
                                                  NhomThucPham = { new SubSupplierFoodGroup { MaNhom = "969" } } });
            db.SubSuppliers.Add(new SubSupplier { MaNccDauVao = "NCC-DUNG", Ten = "NCC bánh",
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
}
