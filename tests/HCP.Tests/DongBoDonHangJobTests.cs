using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng job kéo đơn hàng từ HanoiCheck: upsert theo mã đơn (idempotent), lưu đúng
/// TenantId/dòng sản phẩm, và cập nhật tại chỗ khi đơn đã tồn tại.
/// </summary>
public class DongBoDonHangJobTests
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

    private sealed class FakeOrderClient : IHanoiCheckOrderQueryClient
    {
        public OrderQueryResult Result = OrderQueryResult.Ok(Array.Empty<OrderListItem>());
        public Dictionary<string, OrderDetail> ChiTietTheoMa = new();

        public Task<OrderQueryResult> LayDanhSachAsync(string tenantId, OrderQueryFilter filter, CancellationToken ct = default)
            => Task.FromResult(Result);

        public Task<OrderDetailResult> LayChiTietAsync(string tenantId, string maDon, CancellationToken ct = default)
            => Task.FromResult(ChiTietTheoMa.TryGetValue(maDon, out var d)
                ? OrderDetailResult.Ok(d)
                : OrderDetailResult.Loi("Không có chi tiết giả lập."));
    }

    private static OrderListItem Don(string code, string truong, string status, string? ngay,
                                     params (string ma, string ten)[] sp) => new()
    {
        Code = code,
        School = new SchoolInfo { Name = truong },
        Status = status,
        OrderDate = ngay,
        Products = sp.Select(x => new ProductInfo { Code = x.ma, Name = x.ten }).ToList()
    };

    private DongBoDonHangJob Job(AppDbContext db, FakeOrderClient client) =>
        new(db, client, TimeProvider.System, NullLogger<DongBoDonHangJob>.Instance);

    [Fact]
    public async Task Keo_Va_Luu_Don_Voi_Dong_San_Pham()
    {
        var client = new FakeOrderClient
        {
            Result = OrderQueryResult.Ok(new[]
            {
                Don("DH001", "Trường Mầm non Hoa Sen", "DANG_GIAO", "2026-09-10",
                    ("TP-01", "Bánh mì"), ("TP-02", "Bánh ngọt")),
                Don("DH002", "Tiểu học Quang Trung", "CHO_XAC_NHAN", "2026-09-11", ("TP-01", "Bánh mì")),
            })
        };

        using (var db = MoDb())
        {
            var kq = await Job(db, client).DongBoMotCoSoAsync(CoSo);
            Assert.True(kq.ThanhCong, kq.ThongBao);
            Assert.Equal(2, kq.SoDon);
        }

        using (var db = MoDb())
        {
            var dons = await db.DonHangNhans.Include(d => d.Dong)
                .Where(d => d.TenantId == CoSo).OrderBy(d => d.MaDonHang).ToListAsync();
            Assert.Equal(2, dons.Count);

            var d1 = dons[0];
            Assert.Equal("DH001", d1.MaDonHang);
            Assert.Equal("Trường Mầm non Hoa Sen", d1.TenTruong);
            Assert.Equal("DANG_GIAO", d1.TrangThai);
            Assert.Equal(new DateOnly(2026, 9, 10), d1.NgayGiao);
            Assert.Equal(CoSo, d1.TenantId);
            Assert.Equal(2, d1.Dong.Count);
            Assert.All(d1.Dong, x => Assert.Equal(CoSo, x.TenantId));
            Assert.Contains(d1.Dong, x => x.MaSanPham == "TP-02" && x.TenSanPham == "Bánh ngọt");
        }
    }

    private static OrderDetail ChiTiet(string code, string status, string ngay,
        params (string ma, string ten, decimal sl, string trace, (string sfc, string lo, string kho, decimal sl)[] alloc)[] items) => new()
    {
        Code = code, Status = status, OrderDate = ngay,
        School = new SchoolInfo { Name = "Trường A" },
        Items = items.Select(i => new OrderDetailItem
        {
            Code = i.ma, Name = i.ten, SoLuong = i.sl, TraceCode = i.trace,
            Allocations = i.alloc.Select(a => new OrderAllocation
            { SupplierFoodCode = a.sfc, MaLo = a.lo, MaKho = a.kho, SoLuong = a.sl }).ToList()
        }).ToList()
    };

    [Fact]
    public async Task Keo_Chi_Tiet_Do_So_Luong_Va_Phan_Bo()
    {
        var client = new FakeOrderClient
        {
            Result = OrderQueryResult.Ok(new[] { Don("DH001", "Trường A", "DANG_GIAO", "2026-09-10", ("TP-01", "Bánh mì")) }),
        };
        client.ChiTietTheoMa["DH001"] = ChiTiet("DH001", "DANG_GIAO", "2026-09-10",
            ("TP-01", "Bánh mì", 30m, "TR-1", new[] { ("SF-01", "LO_A", "KHO01", 20m), ("SF-01", "LO_B", "KHO01", 10m) }));

        using (var db = MoDb()) Assert.True((await Job(db, client).DongBoMotCoSoAsync(CoSo)).ThanhCong);

        using (var db = MoDb())
        {
            var don = await db.DonHangNhans
                .Include(d => d.Dong).ThenInclude(l => l.PhanBo)
                .SingleAsync(d => d.TenantId == CoSo && d.MaDonHang == "DH001");
            Assert.True(don.DaLayChiTiet);
            var dong = Assert.Single(don.Dong);
            Assert.Equal(30m, dong.SoLuong);
            Assert.Equal("TR-1", dong.MaTruyVet);
            Assert.Equal(2, dong.PhanBo.Count);
            Assert.Contains(dong.PhanBo, p => p.MaLo == "LO_A" && p.MaKho == "KHO01" && p.SoLuong == 20m);
            Assert.All(dong.PhanBo, p => Assert.Equal(CoSo, p.TenantId));
        }
    }

    [Fact]
    public async Task Dong_Bo_Lai_Thi_Upsert_Khong_Nhan_Ban()
    {
        var client = new FakeOrderClient
        {
            Result = OrderQueryResult.Ok(new[] { Don("DH001", "Trường A", "CHO_XAC_NHAN", "2026-09-10", ("TP-01", "Bánh mì")) })
        };
        client.ChiTietTheoMa["DH001"] = ChiTiet("DH001", "CHO_XAC_NHAN", "2026-09-10",
            ("TP-01", "Bánh mì", 10m, "TR-1", System.Array.Empty<(string, string, string, decimal)>()));
        using (var db = MoDb()) await Job(db, client).DongBoMotCoSoAsync(CoSo);

        // Lần 2: cùng mã đơn nhưng đổi trạng thái + dòng hàng.
        client.Result = OrderQueryResult.Ok(new[] { Don("DH001", "Trường A", "DA_GIAO", "2026-09-10") });
        client.ChiTietTheoMa["DH001"] = ChiTiet("DH001", "DA_GIAO", "2026-09-10",
            ("TP-01", "Bánh mì", 10m, "TR-1", System.Array.Empty<(string, string, string, decimal)>()),
            ("TP-09", "Bánh kem", 5m, "TR-2", System.Array.Empty<(string, string, string, decimal)>()));
        using (var db = MoDb()) await Job(db, client).DongBoMotCoSoAsync(CoSo);

        using (var db = MoDb())
        {
            var dons = await db.DonHangNhans.Include(d => d.Dong).Where(d => d.TenantId == CoSo).ToListAsync();
            Assert.Single(dons);                              // không nhân bản
            Assert.Equal("DA_GIAO", dons[0].TrangThai);       // cập nhật tại chỗ
            Assert.Equal(2, dons[0].Dong.Count);              // dòng thay mới từ chi tiết
            Assert.Equal(2, await db.DonHangNhanDongs.CountAsync()); // không còn mồ côi
        }
    }

    [Fact]
    public async Task Chua_Cau_Hinh_Thi_Bao_Chua_Ket_Noi()
    {
        var client = new FakeOrderClient { Result = OrderQueryResult.ChuaCauHinhKq("Chưa cấu hình.") };
        using var db = MoDb();
        var kq = await Job(db, client).DongBoMotCoSoAsync(CoSo);
        Assert.False(kq.ThanhCong);
        Assert.True(kq.ChuaCauHinh);
        Assert.Equal(0, await db.DonHangNhans.CountAsync());
    }
}
