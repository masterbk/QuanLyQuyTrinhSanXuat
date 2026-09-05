using System.Text.Json;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.HanoiCheck.Mapping;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng nghiệp vụ đơn hàng, trọng tâm là quy tắc food vs dish: đơn món ăn KHÔNG được
/// gửi xuất kho (nếu không HnC trả 422), và dòng đặt mang đúng khoá theo loại đơn.
/// </summary>
public class DonHangServiceTests
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

    private void Seed()
    {
        using var db = MoDb();
        db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho", DiaChi = "HN" });
        db.Products.Add(new Product { MaSanPham = "SP001", TenSanPham = "Thịt", MaLoaiSp = "THIT" });
        db.Batches.Add(new Batch { MaSanPham = "SP001", MaLo = "LO01", TenLo = "Lô", NgayNhap = new DateOnly(2026, 7, 1) });
        db.Dishes.Add(new Dish { MaMonAn = "MON001", TenMonAn = "Cơm", NhomTuoiId = 1 });
        db.SaveChanges();
    }

    [Fact]
    public async Task Don_Food_Hop_Le_Thi_Luu_Va_Enqueue()
    {
        Seed();
        var outbox = new CapturingOutbox();
        using var db = MoDb();
        var svc = new DonHangService(db, outbox);

        var kq = await svc.ThemAsync(new Order
        {
            MaDonHang = "DH001", LoaiDonHang = "food", MaTruong = "TH_A", TrangThai = "DANG_GIAO",
            ChiTiet = { new OrderLine { MaLoaiSp = "THIT", SoLuong = 50 } },
            XuatKho = { new OrderExport { MaSanPham = "SP001", MaKho = "KHO01", MaLo = "LO01", SoLuong = 50 } }
        });

        Assert.True(kq.ThanhCong, kq.ThongBao);
        Assert.Equal("Order", outbox.EntityTypeCuoi);
        Assert.Contains("\"xuat_kho\"", outbox.PayloadCuoi);
        Assert.Contains("\"ma_loai_sp\":\"THIT\"", outbox.PayloadCuoi);
    }

    [Fact]
    public async Task Don_Dish_Thi_Tu_Dong_Bo_Xuat_Kho()
    {
        Seed();
        var outbox = new CapturingOutbox();
        using var db = MoDb();
        var svc = new DonHangService(db, outbox);

        // Cố tình đính kèm xuat_kho vào đơn món ăn - service phải tự loại bỏ để HnC không trả 422.
        var kq = await svc.ThemAsync(new Order
        {
            MaDonHang = "DH002", LoaiDonHang = "dish", MaTruong = "TH_A", TrangThai = "DANG_CHUAN_BI",
            ChiTiet = { new OrderLine { MaMonAn = "MON001", SoLuong = 320 } },
            XuatKho = { new OrderExport { MaSanPham = "SP001", MaKho = "KHO01", MaLo = "LO01", SoLuong = 5 } }
        });

        Assert.True(kq.ThanhCong, kq.ThongBao);
        Assert.DoesNotContain("xuat_kho", outbox.PayloadCuoi);
        Assert.Contains("\"ma_mon_an\":\"MON001\"", outbox.PayloadCuoi);

        using var db2 = MoDb();
        Assert.Equal(0, await db2.OrderExports.CountAsync());
    }

    [Fact]
    public async Task Don_Food_Dong_Thieu_Ma_Loai_Sp_Thi_Bao_Loi()
    {
        Seed();
        using var db = MoDb();
        var svc = new DonHangService(db, new CapturingOutbox());

        var kq = await svc.ThemAsync(new Order
        {
            MaDonHang = "DH003", LoaiDonHang = "food", MaTruong = "TH_A", TrangThai = "CHO_XAC_NHAN",
            ChiTiet = { new OrderLine { SoLuong = 10 } } // thiếu ma_loai_sp
        });

        Assert.False(kq.ThanhCong);
        Assert.Equal(0, await db.Orders.CountAsync());
    }

    [Fact]
    public async Task Xuat_Kho_Lo_Khong_Ton_Tai_Thi_Bao_Loi()
    {
        Seed();
        using var db = MoDb();
        var svc = new DonHangService(db, new CapturingOutbox());

        var kq = await svc.ThemAsync(new Order
        {
            MaDonHang = "DH004", LoaiDonHang = "food", MaTruong = "TH_A", TrangThai = "DANG_GIAO",
            ChiTiet = { new OrderLine { MaLoaiSp = "THIT", SoLuong = 50 } },
            XuatKho = { new OrderExport { MaSanPham = "SP001", MaKho = "KHO01", MaLo = "LO_KHONG_CO", SoLuong = 50 } }
        });

        Assert.False(kq.ThanhCong);
        Assert.Contains("LO_KHONG_CO", kq.ThongBao);
    }

    private sealed class CapturingOutbox : ISyncOutboxWriter
    {
        public string? EntityTypeCuoi { get; private set; }
        public string PayloadCuoi { get; private set; } = "";

        public Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
        {
            EntityTypeCuoi = entityType;
            PayloadCuoi = JsonSerializer.Serialize(payload, HnCPayloadMapper.Json);
            return Task.CompletedTask;
        }
    }
}
