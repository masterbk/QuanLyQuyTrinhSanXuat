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
        public Task<OrderQueryResult> LayDanhSachAsync(string tenantId, OrderQueryFilter filter, CancellationToken ct = default)
            => Task.FromResult(Result);
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

    [Fact]
    public async Task Dong_Bo_Lai_Thi_Upsert_Khong_Nhan_Ban()
    {
        var client = new FakeOrderClient
        {
            Result = OrderQueryResult.Ok(new[] { Don("DH001", "Trường A", "CHO_XAC_NHAN", "2026-09-10", ("TP-01", "Bánh mì")) })
        };
        using (var db = MoDb()) await Job(db, client).DongBoMotCoSoAsync(CoSo);

        // Lần 2: cùng mã đơn nhưng đổi trạng thái + dòng sản phẩm.
        client.Result = OrderQueryResult.Ok(new[]
        {
            Don("DH001", "Trường A", "DA_GIAO", "2026-09-10", ("TP-01", "Bánh mì"), ("TP-09", "Bánh kem"))
        });
        using (var db = MoDb()) await Job(db, client).DongBoMotCoSoAsync(CoSo);

        using (var db = MoDb())
        {
            var dons = await db.DonHangNhans.Include(d => d.Dong).Where(d => d.TenantId == CoSo).ToListAsync();
            Assert.Single(dons);                              // không nhân bản
            Assert.Equal("DA_GIAO", dons[0].TrangThai);       // cập nhật tại chỗ
            Assert.Equal(2, dons[0].Dong.Count);              // dòng thay mới
            // Không còn dòng mồ côi.
            Assert.Equal(2, await db.DonHangNhanDongs.CountAsync());
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
