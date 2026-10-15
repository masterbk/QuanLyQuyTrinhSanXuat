using System.Text.Json;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Thực đơn theo đặc tả HanoiCheck v2.6 (dịch vụ 10): món cùng nhóm tuổi, không trùng (bữa, món), trường phải
/// thuộc danh sách được phân công, xoá thì gửi "ngừng áp dụng", và món đang trong thực đơn không xoá/đổi được.
/// </summary>
public class ThucDonServiceTests
{
    private const string CoSo = "coso-a";
    private readonly string _dbName = Guid.NewGuid().ToString();
    private bool _daSeed;

    private AppDbContext MoDb()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    /// <summary>Hàng đợi giả: ghi lại payload được gửi để soi.</summary>
    private sealed class Outbox(bool bat) : ISyncOutboxWriter
    {
        public List<object> DaGui { get; } = new();

        public Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> DangBatAsync(CancellationToken ct = default) => Task.FromResult(bat);

        public Task<string?> GuiAsync(object banGhi, CancellationToken ct = default)
        {
            if (banGhi is Menu m) DaGui.Add(HnCPayloadMapper.ThucDon(m));
            return Task.FromResult<string?>(null);
        }
    }

    private void Seed()
    {
        if (_daSeed) return;
        _daSeed = true;
        using var db = MoDb();
        db.Products.Add(new Product { MaSanPham = "TP-1", TenSanPham = "Thịt", MaLoaiSp = "969" });
        db.Dishes.Add(new Dish { MaMonAn = "MA-001", TenMonAn = "Thịt kho", NhomTuoiId = 1 });
        db.Dishes.Add(new Dish { MaMonAn = "MA-002", TenMonAn = "Canh cải", NhomTuoiId = 1 });
        db.Dishes.Add(new Dish { MaMonAn = "TH-001", TenMonAn = "Cơm gà", NhomTuoiId = 2 });
        db.NhomTuoiHnCs.AddRange(new NhomTuoiHnC { Id = 1, Ten = "Mầm non" }, new NhomTuoiHnC { Id = 2, Ten = "Tiểu học" });
        db.BuaAnHnCs.AddRange(new BuaAnHnC { Id = 1, Ten = "Bữa sáng", ThuTu = 1 }, new BuaAnHnC { Id = 2, Ten = "Bữa trưa", ThuTu = 2 });
        db.TruongHocHnCs.Add(new TruongHocHnC { TruongId = 101, Ten = "MN Hoa Sen" });
        db.SaveChanges();
    }

    private static Menu ThucDon(Action<Menu>? sua = null)
    {
        var m = new Menu
        {
            MaThucDon = "TD-T2-MN", TenThucDon = "Thứ 2 - Mầm non", NhomTuoiId = 1, ThuApDung = 1,
            DanhSachMon =
            {
                new MenuItem { BuoiAnId = 1, MaMonAn = "MA-001" },
                new MenuItem { BuoiAnId = 2, MaMonAn = "MA-002" }
            }
        };
        sua?.Invoke(m);
        return m;
    }

    private async Task<KetQuaThaoTac> ThemAsync(Menu m, Outbox? outbox = null)
    {
        Seed();
        using var db = MoDb();
        return await new ThucDonService(db, outbox ?? new Outbox(true)).ThemAsync(m);
    }

    private async Task<string> ThemLoiAsync(Menu m)
    {
        var kq = await ThemAsync(m);
        Assert.False(kq.ThanhCong, "Lẽ ra phải bị chặn");
        return kq.ThongBao;
    }

    [Fact]
    public async Task Thuc_Don_Hop_Le_Luu_Va_Gui_Dung_Payload()
    {
        var outbox = new Outbox(true);
        var kq = await ThemAsync(ThucDon(m => m.DanhSachTruongCsv = "101"), outbox);
        Assert.True(kq.ThanhCong, kq.ThongBao);

        var e = JsonDocument.Parse(JsonSerializer.Serialize(outbox.DaGui.Single(), HnCPayloadMapper.Json)).RootElement[0];
        Assert.Equal("TD-T2-MN", e.GetProperty("ma_thuc_don").GetString());
        Assert.Equal(1, e.GetProperty("nhom_tuoi_id").GetInt32());
        Assert.Equal(1, e.GetProperty("thu_ap_dung").GetInt32());
        Assert.True(e.GetProperty("trang_thai").GetBoolean());
        var mon = e.GetProperty("danh_sach_mon");
        Assert.Equal(2, mon.GetArrayLength());
        Assert.Equal(1, mon[0].GetProperty("buoi_an_id").GetInt32());
        Assert.Equal("MA-001", mon[0].GetProperty("ma_mon_an").GetString());
        Assert.Equal(101, e.GetProperty("danh_sach_truong")[0].GetInt32());
    }

    [Fact]
    public void Dung_Chung_Thi_Gui_Mang_Truong_Rong()
    {
        // Rỗng = dùng chung; phải gửi [] (không bỏ trường) để HnC gỡ phạm vi riêng đã gửi trước đó.
        var json = JsonSerializer.Serialize(HnCPayloadMapper.ThucDon(ThucDon()), HnCPayloadMapper.Json);
        var e = JsonDocument.Parse(json).RootElement[0];
        Assert.Equal(0, e.GetProperty("danh_sach_truong").GetArrayLength());
    }

    [Fact]
    public async Task Bat_Buoc_Co_It_Nhat_Mot_Mon()
    {
        Assert.Contains("ít nhất một món", await ThemLoiAsync(ThucDon(m => m.DanhSachMon.Clear())));
    }

    [Fact]
    public async Task Mon_Khac_Nhom_Tuoi_Bi_Chan()
    {
        var loi = await ThemLoiAsync(ThucDon(m => m.DanhSachMon.Add(new MenuItem { BuoiAnId = 2, MaMonAn = "TH-001" })));
        Assert.Contains("Cơm gà", loi);
    }

    [Fact]
    public async Task Trung_Bua_Va_Mon_Bi_Chan()
    {
        var loi = await ThemLoiAsync(ThucDon(m => m.DanhSachMon.Add(new MenuItem { BuoiAnId = 1, MaMonAn = "MA-001" })));
        Assert.Contains("bị lặp", loi);
        // Cùng món nhưng khác bữa thì được.
        Assert.True((await ThemAsync(ThucDon(m => m.DanhSachMon.Add(new MenuItem { BuoiAnId = 2, MaMonAn = "MA-001" })))).ThanhCong);
    }

    [Fact]
    public async Task Truong_Khong_Duoc_Phan_Cong_Bi_Chan()
    {
        Assert.Contains("không nằm trong danh sách trường", await ThemLoiAsync(ThucDon(m => m.DanhSachTruongCsv = "101,999")));
    }

    [Fact]
    public async Task Bua_An_Ngoai_Danh_Muc_Bi_Chan()
    {
        Assert.Contains("danh mục bữa ăn", await ThemLoiAsync(ThucDon(m => m.DanhSachMon[0].BuoiAnId = 9)));
    }

    [Fact]
    public async Task Xoa_Thi_Gui_Ngung_Ap_Dung_Truoc()
    {
        Assert.True((await ThemAsync(ThucDon())).ThanhCong);

        var outbox = new Outbox(true);
        using var db = MoDb();
        var id = db.Menus.Single().Id;
        var kq = await new ThucDonService(db, outbox).XoaAsync(id);
        Assert.True(kq.ThanhCong, kq.ThongBao);

        var e = JsonDocument.Parse(JsonSerializer.Serialize(outbox.DaGui.Single(), HnCPayloadMapper.Json)).RootElement[0];
        Assert.False(e.GetProperty("trang_thai").GetBoolean());
        using var db2 = MoDb();
        Assert.Empty(db2.Menus.ToList());
    }

    [Fact]
    public async Task Mon_Dang_Trong_Thuc_Don_Khong_Xoa_Hay_Doi_Nhom_Tuoi_Duoc()
    {
        Assert.True((await ThemAsync(ThucDon())).ThanhCong);

        using var db = MoDb();
        var svc = new MonAnService(db, new Outbox(false));
        var mon = db.Dishes.AsNoTracking().Single(d => d.MaMonAn == "MA-001");

        var xoa = await svc.XoaAsync(mon.Id);
        Assert.False(xoa.ThanhCong);
        Assert.Contains("TD-T2-MN", xoa.ThongBao);

        var sua = await svc.CapNhatAsync(new Dish
        {
            Id = mon.Id, MaMonAn = "MA-001", TenMonAn = "Thịt kho", NhomTuoiId = 2, DongBoHnC = false,
            DanhSachNguyenLieu = { new DishIngredient { MaNguyenLieu = "TP-1", DinhLuong = 1 } }
        });
        Assert.False(sua.ThanhCong);
        Assert.Contains("TD-T2-MN", sua.ThongBao);
    }
}
