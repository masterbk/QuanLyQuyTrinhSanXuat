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

    /// <summary>
    /// Trích NGUYÊN VĂN 2 đơn từ response thật của GET /api/supplier/orders (Docs/donhang.json,
    /// 11/09/2026) - khoá cách parse theo dữ liệu HanoiCheck thật trả về.
    /// </summary>
    private const string JsonDanhSachThat = """
    {
      "success": true,
      "message": "Danh sách đơn hàng",
      "data": [
        {
          "code": "DH260909Z4IFHJ",
          "traceability_url": "https://tracuu.hanoicheck.com.vn/NCC-2026-000140/truy-xuat/DH260909Z4IFHJ",
          "order_date": "2026-09-10",
          "product_type": "food",
          "product_type_label": "Thực phẩm",
          "school": { "name": "MẦM NON QUANG TRUNG" },
          "products": [ { "code": "KEM-CARAMEL", "name": "KEM CARAMEL" } ],
          "product_summary": "KEM CARAMEL",
          "warehouses": [ { "code": "K-01", "name": "Kho số 1" } ],
          "transporter": {
            "code": "VC-01",
            "name": "Nguyễn Văn A",
            "phone": "0900000001",
            "transport_mean": "Xe máy",
            "license_plate": "30A-000.01"
          },
          "status": "GIAO_HANG_THANH_CONG",
          "status_label": "Giao hàng thành công",
          "school_point": "5",
          "delivery_address": null,
          "note": null,
          "images": [],
          "created_at": "2026-09-09 11:56:16"
        },
        {
          "code": "DH260911M4LVRM",
          "traceability_url": "https://tracuu.hanoicheck.com.vn/NCC-2026-000140/truy-xuat/DH260911M4LVRM",
          "order_date": "2026-09-11",
          "product_type": "food",
          "product_type_label": "Thực phẩm",
          "school": { "name": "Trường Mầm non Hoàn Kiếm" },
          "products": [ { "code": "KEM-CARAMEL", "name": "KEM CARAMEL" } ],
          "product_summary": "KEM CARAMEL",
          "warehouses": [],
          "transporter": null,
          "status": "HUY",
          "status_label": "Hủy",
          "school_point": "1",
          "delivery_address": null,
          "note": "Nhà trường hủy đơn hàng",
          "images": [],
          "created_at": "2026-09-11 13:06:39"
        }
      ],
      "pagination": { "total": 2, "per_page": 20, "current_page": 1, "last_page": 1, "from": 1, "to": 2 }
    }
    """;

    [Fact]
    public async Task Parse_JSON_That_Cua_HanoiCheck_Ra_Du_Thong_Tin_Don()
    {
        // Parse đúng như HanoiCheckOrderQueryClient (options mặc định).
        var parsed = System.Text.Json.JsonSerializer.Deserialize<OrderListResponse>(JsonDanhSachThat)!;
        Assert.Equal(2, parsed.Data!.Count);

        // Chi tiết đơn giả lập giống lỗi đang thấy trên màn hình: có dòng hàng nhưng tên = null,
        // và KHÔNG có phần đầu đơn -> không được xoá thông tin người giao/kho lấy từ danh sách.
        var client = new FakeOrderClient { Result = OrderQueryResult.Ok(parsed.Data) };
        client.ChiTietTheoMa["DH260909Z4IFHJ"] = new OrderDetail
        {
            Code = "DH260909Z4IFHJ",
            Items = new() { new OrderDetailItem { Code = "KEM-CARAMEL", Name = null } }
        };

        using (var db = MoDb()) Assert.True((await Job(db, client).DongBoMotCoSoAsync(CoSo)).ThanhCong);

        using (var db = MoDb())
        {
            var giao = await db.DonHangNhans.Include(d => d.Dong)
                .SingleAsync(d => d.TenantId == CoSo && d.MaDonHang == "DH260909Z4IFHJ");
            Assert.Equal("MẦM NON QUANG TRUNG", giao.TenTruong);
            Assert.Equal("GIAO_HANG_THANH_CONG", giao.TrangThai);
            Assert.Equal(new DateOnly(2026, 9, 10), giao.NgayGiao);
            Assert.Equal("VC-01", giao.MaNguoiGiao);
            Assert.Equal("Nguyễn Văn A", giao.TenNguoiGiao);
            Assert.Equal("0900000001", giao.SdtNguoiGiao);
            Assert.Equal("Xe máy", giao.PhuongTienGiao);
            Assert.Equal("30A-000.01", giao.BienSoXe);
            Assert.Equal("5", giao.DiemTruong);
            Assert.Equal("K-01 - Kho số 1", giao.KhoXuat);
            Assert.Equal("Thực phẩm", giao.LoaiDon);
            Assert.Null(giao.DiaChiGiao);
            Assert.Equal(new DateTime(2026, 9, 9, 11, 56, 16), giao.NgayTaoTrenHnC);
            Assert.StartsWith("https://tracuu.hanoicheck.com.vn/", giao.LinkTruyXuat);
            Assert.True(giao.DaLayChiTiet);
            // Tên dòng hàng bù từ products[] của danh sách khi chi tiết không có tên.
            Assert.Equal("KEM CARAMEL", Assert.Single(giao.Dong).TenSanPham);

            var huy = await db.DonHangNhans.SingleAsync(d => d.TenantId == CoSo && d.MaDonHang == "DH260911M4LVRM");
            Assert.Equal("Nhà trường hủy đơn hàng", huy.GhiChu);
            Assert.Null(huy.TenNguoiGiao);   // transporter: null
            Assert.Null(huy.KhoXuat);        // warehouses: []
        }
    }

    [Fact]
    public async Task Bo_Phan_Cong_Nguoi_Giao_Tren_HnC_Thi_Xoa_Tren_App()
    {
        var parsed = System.Text.Json.JsonSerializer.Deserialize<OrderListResponse>(JsonDanhSachThat)!;
        var client = new FakeOrderClient { Result = OrderQueryResult.Ok(parsed.Data!) };
        using (var db = MoDb()) await Job(db, client).DongBoMotCoSoAsync(CoSo);

        // Lần sau HnC trả transporter = null -> danh sách là nguồn chuẩn, phải xoá theo.
        parsed.Data![0].Transporter = null;
        using (var db = MoDb()) await Job(db, client).DongBoMotCoSoAsync(CoSo);

        using (var db = MoDb())
        {
            var don = await db.DonHangNhans.SingleAsync(d => d.MaDonHang == "DH260909Z4IFHJ");
            Assert.Null(don.MaNguoiGiao);
            Assert.Null(don.TenNguoiGiao);
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
