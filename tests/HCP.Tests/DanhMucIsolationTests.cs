using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Cách ly dữ liệu cho các danh mục lõi (Giai đoạn 2).
///
/// Trọng tâm là BẢNG CON (ProcessStepLines, SubSupplierFoodGroups). Bảng cha bị lọc theo
/// tenant, nhưng nếu bảng con không bị lọc thì một truy vấn trực tiếp vào bảng con vẫn
/// chạm được dữ liệu cơ sở khác. Nguy hiểm ở chỗ mã khâu là mã chuẩn (SO_CHE, GIET_MO...)
/// nên trùng nhau giữa các cơ sở là chuyện bình thường.
/// </summary>
public class DanhMucIsolationTests
{
    private const string CoSoA = "coso-a";
    private const string CoSoB = "coso-b";

    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext OpenAs(string tenantId)
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(tenantId);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        return new AppDbContext(accessor, options);
    }

    /// <summary>Ghi hàng đợi đồng bộ không cần thiết cho test cách ly - dùng bản rỗng.</summary>
    private sealed class NoOpOutbox : ISyncOutboxWriter
    {
        public Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> DangBatAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task<string?> GuiAsync(object banGhi, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    /// <summary>Tạo sẵn cho mỗi cơ sở một khâu cùng mã và một quy trình dùng khâu đó.</summary>
    private void TaoDuLieuHaiCoSoCungMaKhau()
    {
        foreach (var tenant in new[] { CoSoA, CoSoB })
        {
            using var db = OpenAs(tenant);

            db.ProductionSteps.Add(new ProductionStep
            {
                MaKhau = "SO_CHE",
                TenKhau = $"Sơ chế ({tenant})"
            });

            db.ProductionProcesses.Add(new ProductionProcess
            {
                MaQuyTrinh = "QT001",
                TenQuyTrinh = $"Quy trình ({tenant})",
                DanhSachKhau = new List<ProcessStepLine>
                {
                    new() { MaKhau = "SO_CHE", ThuTu = 1 }
                }
            });

            db.SaveChanges();
        }
    }

    [Fact]
    public void Bang_Con_ProcessStepLine_Bi_Loc_Theo_Co_So()
    {
        TaoDuLieuHaiCoSoCungMaKhau();

        using var db = OpenAs(CoSoA);

        // Truy vấn TRỰC TIẾP bảng con, chỉ lọc theo mã khâu - không hề nhắc tới TenantId.
        var dongKhau = db.ProcessStepLines.Where(l => l.MaKhau == "SO_CHE").ToList();

        Assert.Single(dongKhau);
        Assert.Equal(CoSoA, dongKhau[0].TenantId);
    }

    [Fact]
    public async Task Sua_Khau_Giu_Nguyen_Ma_Va_Khong_Dung_Toi_Co_So_Khac()
    {
        TaoDuLieuHaiCoSoCungMaKhau();

        using (var db = OpenAs(CoSoA))
        {
            var service = new KhauSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db));
            var khau = await db.ProductionSteps.FirstAsync();

            // Gửi kèm mã mới: mã khâu bị khoá nên chỉ tên được cập nhật.
            var ketQua = await service.CapNhatAsync(new ProductionStep
            {
                Id = khau.Id,
                MaKhau = "SO_CHE_MOI",
                TenKhau = "Sơ chế (đã sửa)"
            });

            Assert.True(ketQua.ThanhCong, ketQua.ThongBao);
        }

        using (var db = OpenAs(CoSoA))
        {
            var khau = await db.ProductionSteps.SingleAsync();
            Assert.Equal("SO_CHE", khau.MaKhau);
            Assert.Equal("Sơ chế (đã sửa)", khau.TenKhau);
            Assert.Equal("SO_CHE", (await db.ProcessStepLines.SingleAsync()).MaKhau);
        }

        using (var db = OpenAs(CoSoB))
            Assert.Equal($"Sơ chế ({CoSoB})", (await db.ProductionSteps.SingleAsync()).TenKhau);
    }

    [Fact]
    public async Task Khong_Bi_Chan_Xoa_Khau_Chi_Vi_Co_So_Khac_Dang_Dung_Ma_Do()
    {
        // Cơ sở B có quy trình dùng SO_CHE; cơ sở A có khâu SO_CHE nhưng không quy trình nào dùng.
        using (var db = OpenAs(CoSoB))
        {
            db.ProductionSteps.Add(new ProductionStep { MaKhau = "SO_CHE", TenKhau = "Sơ chế" });
            db.ProductionProcesses.Add(new ProductionProcess
            {
                MaQuyTrinh = "QT-B",
                TenQuyTrinh = "Quy trình B",
                DanhSachKhau = new List<ProcessStepLine> { new() { MaKhau = "SO_CHE", ThuTu = 1 } }
            });
            db.SaveChanges();
        }

        using (var db = OpenAs(CoSoA))
        {
            db.ProductionSteps.Add(new ProductionStep { MaKhau = "SO_CHE", TenKhau = "Sơ chế" });
            db.SaveChanges();
        }

        using (var db = OpenAs(CoSoA))
        {
            var service = new KhauSanXuatService(db, new NoOpOutbox(), new MaTuSinhService(db));
            var khau = await db.ProductionSteps.FirstAsync();

            var ketQua = await service.XoaAsync(khau.Id);

            Assert.True(ketQua.ThanhCong,
                "Cơ sở A phải xoá được khâu của mình dù cơ sở B đang dùng cùng mã khâu. "
                + "Thông báo nhận được: " + ketQua.ThongBao);
        }
    }

    [Fact]
    public async Task Nhom_Thuc_Pham_Cua_Ncc_Bi_Loc_Theo_Co_So()
    {
        foreach (var tenant in new[] { CoSoA, CoSoB })
        {
            using var db = OpenAs(tenant);
            db.SubSuppliers.Add(new SubSupplier
            {
                MaNccDauVao = "NCC001",
                Ten = $"Nhà cung ứng ({tenant})",
                NhomThucPham = new List<SubSupplierFoodGroup> { new() { MaNhom = "THIT" } }
            });
            db.SaveChanges();
        }

        using var dbA = OpenAs(CoSoA);

        var nhom = await dbA.SubSupplierFoodGroups.Where(g => g.MaNhom == "THIT").ToListAsync();

        Assert.Single(nhom);
        Assert.Equal(CoSoA, nhom[0].TenantId);
    }

    [Fact]
    public void Nhan_Su_Bi_Loc_Theo_Co_So()
    {
        // Mã nhân sự trùng nhau giữa 2 cơ sở là hợp lệ (chỉ duy nhất trong từng cơ sở).
        foreach (var tenant in new[] { CoSoA, CoSoB })
        {
            using var db = OpenAs(tenant);
            db.Staff.Add(new Staff { MaNhanSu = "NV001", HoTen = $"Nhân viên ({tenant})" });
            db.SaveChanges();
        }

        using var dbA = OpenAs(CoSoA);
        var ds = dbA.Staff.ToList();

        Assert.Single(ds);
        Assert.Equal(CoSoA, ds[0].TenantId);
        Assert.Equal("Nhân viên (coso-a)", ds[0].HoTen);
    }

    [Fact]
    public void Lo_San_Xuat_Va_Bang_Con_Bi_Loc_Theo_Co_So()
    {
        foreach (var tenant in new[] { CoSoA, CoSoB })
        {
            using var db = OpenAs(tenant);
            db.Batches.Add(new Batch
            {
                MaSanPham = "SP001", MaLo = "LO01", TenLo = $"Lô ({tenant})",
                NgayNhap = new DateOnly(2026, 7, 20),
                DanhSachKho = { new BatchWarehouse { MaKho = "KHO01" } },
                DanhSachKhau = { new BatchStep { MaBuocSx = "BSX01", MaKhau = "SO_CHE", ThuTu = 1 } }
            });
            db.SaveChanges();
        }

        using var dbA = OpenAs(CoSoA);
        Assert.Single(dbA.Batches.ToList());
        Assert.Equal(CoSoA, dbA.Batches.Single().TenantId);

        // Truy vấn TRỰC TIẾP bảng con (mã bước trùng nhau giữa các cơ sở) vẫn phải bị lọc.
        var buoc = dbA.BatchSteps.Where(s => s.MaBuocSx == "BSX01").ToList();
        Assert.Single(buoc);
        Assert.Equal(CoSoA, buoc[0].TenantId);
    }

    [Fact]
    public void Don_Hang_Ban_Va_Bang_Con_Bi_Loc_Theo_Co_So()
    {
        foreach (var tenant in new[] { CoSoA, CoSoB })
        {
            using var db = OpenAs(tenant);
            db.DonHangBans.Add(new DonHangBan
            {
                MaDonHang = "DH001", MaKhachHang = "KH01", MaKho = "KHO01", NgayDat = new DateOnly(2026, 9, 13),
                Dong =
                {
                    new DonHangBanDong
                    {
                        MaThanhPham = "SP001", SoLuong = 50, DonGia = 1000,
                        XuatLo = { new DonHangBanXuatLo { MaLo = "LO01", SoLuong = 50 } }
                    }
                }
            });
            db.SaveChanges();
        }

        using var dbA = OpenAs(CoSoA);
        Assert.Single(dbA.DonHangBans.ToList());
        Assert.Equal(CoSoA, dbA.DonHangBans.Single().TenantId);

        // Bảng con phân bổ lô (mã lô trùng nhau giữa cơ sở) phải bị lọc.
        var lo = dbA.DonHangBanXuatLos.Where(x => x.MaLo == "LO01").ToList();
        Assert.Single(lo);
        Assert.Equal(CoSoA, lo[0].TenantId);
    }

    [Fact]
    public void Mon_An_Va_Bang_Con_Bi_Loc_Theo_Co_So()
    {
        foreach (var tenant in new[] { CoSoA, CoSoB })
        {
            using var db = OpenAs(tenant);
            db.Dishes.Add(new Dish
            {
                MaMonAn = "MON001", TenMonAn = $"Món ({tenant})", NhomTuoiId = 1,
                DanhSachNguyenLieu = { new DishIngredient { MaNguyenLieu = "SP001", DinhLuong = 0.1m } },
                DanhSachKhau = { new DishStep { MaKhau = "SO_CHE", ThuTu = 1 } }
            });
            db.SaveChanges();
        }

        using var dbA = OpenAs(CoSoA);
        Assert.Single(dbA.Dishes.ToList());
        Assert.Equal(CoSoA, dbA.Dishes.Single().TenantId);

        // Bảng con nguyên liệu: mã nguyên liệu trùng nhau giữa cơ sở nhưng phải bị lọc.
        var nl = dbA.DishIngredients.Where(i => i.MaNguyenLieu == "SP001").ToList();
        Assert.Single(nl);
        Assert.Equal(CoSoA, nl[0].TenantId);
    }

    [Fact]
    public void Thuc_Pham_Bi_Loc_Theo_Co_So()
    {
        foreach (var tenant in new[] { CoSoA, CoSoB })
        {
            using var db = OpenAs(tenant);
            db.Products.Add(new Product { MaSanPham = "SP001", TenSanPham = $"Thịt ({tenant})", MaLoaiSp = "THIT" });
            db.SaveChanges();
        }

        using var dbA = OpenAs(CoSoA);
        var ds = dbA.Products.ToList();

        Assert.Single(ds);
        Assert.Equal(CoSoA, ds[0].TenantId);
        Assert.Equal("Thịt (coso-a)", ds[0].TenSanPham);
    }

    [Fact]
    public async Task Cap_Nhat_Danh_Muc_Chuan_Thay_The_Toan_Bo()
    {
        // Có sẵn một mã cũ (slug); cập nhật danh mục mới (theo id) phải XOÁ hết bản cũ.
        using (var db = OpenAs(CoSoA))
        {
            db.StandardFoodCategories.Add(new StandardFoodCategory { Code = "ba_chi_que", Name = "Thịt ba chỉ (cũ)" });
            db.SaveChanges();
        }

        using (var db = OpenAs(CoSoA))
        {
            var svc = new DanhMucChuanService(db);
            await svc.CapNhatTuHnCAsync(new[]
            {
                new StandardFoodCategory { Code = "48533", Name = "Thịt ba chỉ" },
                new StandardFoodCategory { Code = "52795", Name = "AJINOMOTO Giấm gạo lên men" }
            });
        }

        using (var db = OpenAs(CoSoA))
        {
            var ds = await new DanhMucChuanService(db).LayTatCaAsync();
            Assert.Equal(2, ds.Count);
            Assert.DoesNotContain(ds, c => c.Code == "ba_chi_que"); // bản slug cũ đã bị thay thế
            Assert.Contains(ds, c => c.Code == "48533");
        }
    }

    [Fact]
    public async Task Danh_Muc_Chuan_Dung_Chung_Cho_Moi_Co_So()
    {
        // Danh mục do HanoiCheck ban hành - cố ý KHÔNG lọc theo tenant.
        using (var db = OpenAs(CoSoA))
        {
            var service = new DanhMucChuanService(db);
            await service.CapNhatTuHnCAsync(new[]
            {
                new StandardFoodCategory { Code = "THIT", Name = "Thịt và sản phẩm từ thịt" },
                new StandardFoodCategory { Code = "RAU_CU", Name = "Rau củ quả" }
            });
        }

        using (var db = OpenAs(CoSoB))
        {
            var service = new DanhMucChuanService(db);
            var danhMuc = await service.LayTatCaAsync();

            Assert.Equal(2, danhMuc.Count);
        }
    }
}
