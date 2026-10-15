using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm tra Món ăn và loại Quy trình theo đặc tả HanoiCheck v2.6 ngay khi lưu: album ảnh món 1-3, khâu bắt buộc,
/// quy trình phải loại "dish", nguyên liệu phải là thực phẩm gửi HnC, nhóm tuổi đúng danh mục.
/// </summary>
public class MonAnServiceTests
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

    private sealed class Outbox(bool bat) : ISyncOutboxWriter
    {
        public Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> DangBatAsync(CancellationToken ct = default) => Task.FromResult(bat);

        public Task<string?> GuiAsync(object banGhi, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    private bool _daSeed;

    private void Seed()
    {
        if (_daSeed) return;
        _daSeed = true;
        using var db = MoDb();
        db.Products.Add(new Product { MaSanPham = "TP-THIT", TenSanPham = "Thịt", MaLoaiSp = "969", LoaiSanPham = LoaiSanPham.ThanhPham });
        db.Products.Add(new Product { MaSanPham = "NL-MUOI", TenSanPham = "Muối", LoaiSanPham = LoaiSanPham.NguyenLieu });
        db.ProductionSteps.Add(new ProductionStep { MaKhau = "SO_CHE", TenKhau = "Sơ chế" });
        db.ProductionSteps.Add(new ProductionStep { MaKhau = "CHE_BIEN", TenKhau = "Chế biến" });
        db.ProductionProcesses.Add(new ProductionProcess
        {
            MaQuyTrinh = "QT-MON", TenQuyTrinh = "Chế biến món", LoaiApDung = LoaiQuyTrinh.MonAn,
            DanhSachKhau = { new ProcessStepLine { MaKhau = "SO_CHE", ThuTu = 1 } }
        });
        db.ProductionProcesses.Add(new ProductionProcess
        {
            MaQuyTrinh = "QT-TP", TenQuyTrinh = "Sơ chế thịt", LoaiApDung = LoaiQuyTrinh.ThucPham,
            DanhSachKhau = { new ProcessStepLine { MaKhau = "SO_CHE", ThuTu = 1 } }
        });
        db.NhomTuoiHnCs.Add(new NhomTuoiHnC { Id = 1, Ten = "Mầm non" });
        db.SaveChanges();
    }

    private static DishFile Anh(string ma, string? maKhau = null, string duongDan = "https://app.vn/uploads/mon.jpg") =>
        new() { MaFile = ma, TenFile = ma, DuongDan = duongDan, Loai = "HINH_ANH", MaKhau = maKhau };

    /// <summary>Món hợp lệ đầy đủ theo v2.6; từng test làm hỏng đúng một chỗ.</summary>
    private static Dish Mon(Action<Dish>? sua = null)
    {
        var d = new Dish
        {
            MaMonAn = "MON-001", TenMonAn = "Thịt kho", NhomTuoiId = 1, MaQuyTrinh = "QT-MON",
            DanhSachNguyenLieu = { new DishIngredient { MaNguyenLieu = "TP-THIT", DinhLuong = 0.08m } },
            DanhSachKhau = { new DishStep { MaKhau = "SO_CHE", ThuTu = 1 } },
            DanhSachFile = { Anh("F1") }
        };
        sua?.Invoke(d);
        return d;
    }

    private async Task<KetQuaThaoTac> ThemAsync(Dish d, bool batHnC = true)
    {
        Seed();
        using var db = MoDb();
        return await new MonAnService(db, new Outbox(batHnC)).ThemAsync(d);
    }

    private async Task<string> ThemLoiAsync(Dish d)
    {
        var kq = await ThemAsync(d);
        Assert.False(kq.ThanhCong, "Lẽ ra phải bị chặn");
        return kq.ThongBao;
    }

    [Fact]
    public async Task Mon_Day_Du_Theo_V26_Luu_Duoc()
    {
        var kq = await ThemAsync(Mon(d => d.DanhSachFile.Add(Anh("F2", "SO_CHE", "https://app.vn/kd.pdf"))));
        Assert.True(kq.ThanhCong, kq.ThongBao);
    }

    [Fact]
    public async Task Bat_Buoc_Album_Anh_Mon()
    {
        var loi = await ThemLoiAsync(Mon(d => d.DanhSachFile.Clear()));
        Assert.Contains("ảnh món", loi);

        // Tệp gắn khâu không tính là ảnh món.
        var loi2 = await ThemLoiAsync(Mon(d => d.DanhSachFile[0].MaKhau = "SO_CHE"));
        Assert.Contains("ảnh món", loi2);
    }

    [Fact]
    public async Task Toi_Da_3_Anh_Mon()
    {
        var loi = await ThemLoiAsync(Mon(d => d.DanhSachFile.AddRange(new[] { Anh("F2"), Anh("F3"), Anh("F4") })));
        Assert.Contains("từ 1 đến 3 ảnh", loi);
    }

    [Fact]
    public async Task Bat_Buoc_Co_Khau()
    {
        var loi = await ThemLoiAsync(Mon(d => d.DanhSachKhau.Clear()));
        Assert.Contains("ít nhất một khâu", loi);
    }

    [Fact]
    public async Task Bat_Buoc_Quy_Trinh_Loai_Mon_An()
    {
        Assert.Contains("bắt buộc quy trình", await ThemLoiAsync(Mon(d => d.MaQuyTrinh = null)));
        Assert.Contains("dùng cho thực phẩm", await ThemLoiAsync(Mon(d => d.MaQuyTrinh = "QT-TP")));
    }

    [Fact]
    public async Task Nguyen_Lieu_Noi_Bo_Khong_Gui_HnC_Duoc()
    {
        var loi = await ThemLoiAsync(Mon(d =>
            d.DanhSachNguyenLieu.Add(new DishIngredient { MaNguyenLieu = "NL-MUOI", DinhLuong = 0.001m })));
        Assert.Contains("NL-MUOI", loi);
    }

    [Fact]
    public async Task Nhom_Tuoi_Phai_Co_Trong_Danh_Muc()
    {
        var loi = await ThemLoiAsync(Mon(d => d.NhomTuoiId = 99));
        Assert.Contains("Nhóm tuổi 99", loi);
    }

    [Fact]
    public async Task Ma_Mon_Co_Dau_Bi_Chan()
    {
        var loi = await ThemLoiAsync(Mon(d => d.MaMonAn = "Món 1"));
        Assert.Contains("Mã món ăn chỉ được chứa", loi);
    }

    [Fact]
    public async Task Tep_Khau_Phai_Thuoc_Khau_Cua_Mon_Va_La_Anh_Hoac_Pdf()
    {
        Assert.Contains("không có trong các khâu",
            await ThemLoiAsync(Mon(d => d.DanhSachFile.Add(Anh("F2", "CHE_BIEN")))));

        Assert.Contains("ảnh hoặc PDF",
            await ThemLoiAsync(Mon(d => d.DanhSachFile.Add(Anh("F2", "SO_CHE", "https://app.vn/a.docx")))));
    }

    [Fact]
    public async Task Tat_HnC_Thi_Khong_Ap_Quy_Dinh_Rieng_Cua_HnC()
    {
        // Không ảnh, không khâu, không quy trình, nguyên liệu nội bộ: vẫn lưu được khi món không gửi HanoiCheck.
        var kq = await ThemAsync(Mon(d =>
        {
            d.DanhSachFile.Clear();
            d.DanhSachKhau.Clear();
            d.MaQuyTrinh = null;
            d.DanhSachNguyenLieu[0].MaNguyenLieu = "NL-MUOI";
        }), batHnC: false);
        Assert.True(kq.ThanhCong, kq.ThongBao);
    }

    [Fact]
    public async Task Thuc_Pham_Khong_Gan_Duoc_Quy_Trinh_Mon_An()
    {
        Seed();
        using var db = MoDb();
        var kq = await new ThucPhamService(db, new Outbox(false)).ThemAsync(new Product
        {
            MaSanPham = "TP-RAU", TenSanPham = "Rau", MaLoaiSp = "1000", MaQuyTrinh = "QT-MON"
        });
        Assert.False(kq.ThanhCong);
        Assert.Contains("món ăn", kq.ThongBao);
    }

    [Fact]
    public async Task Khong_Doi_Loai_Quy_Trinh_Dang_Duoc_Dung()
    {
        Assert.True((await ThemAsync(Mon())).ThanhCong);

        using var db = MoDb();
        var svc = new QuyTrinhSanXuatService(db, new Outbox(false));
        var qt = (await svc.LayTatCaAsync()).Single(q => q.MaQuyTrinh == "QT-MON");
        var kq = await svc.CapNhatAsync(new ProductionProcess
        {
            Id = qt.Id, MaQuyTrinh = "QT-MON", TenQuyTrinh = qt.TenQuyTrinh, LoaiApDung = LoaiQuyTrinh.ThucPham,
            DanhSachKhau = { new ProcessStepLine { MaKhau = "SO_CHE", ThuTu = 1 } }
        });
        Assert.False(kq.ThanhCong);
        Assert.Contains("MON-001", kq.ThongBao);

        // Không gửi loại (app bản cũ) = giữ nguyên loại "dish".
        var kq2 = await svc.CapNhatAsync(new ProductionProcess
        {
            Id = qt.Id, MaQuyTrinh = "QT-MON", TenQuyTrinh = "Đổi tên", LoaiApDung = "",
            DanhSachKhau = { new ProcessStepLine { MaKhau = "SO_CHE", ThuTu = 1 } }
        });
        Assert.True(kq2.ThanhCong, kq2.ThongBao);
        using var db2 = MoDb();
        Assert.Equal(LoaiQuyTrinh.MonAn, db2.ProductionProcesses.Single(q => q.MaQuyTrinh == "QT-MON").LoaiApDung);
    }
}
