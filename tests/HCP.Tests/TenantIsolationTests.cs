using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng hàng rào cách ly dữ liệu giữa các cơ sở sản xuất.
///
/// Đây là rủi ro nghiêm trọng nhất của kiến trúc multi-tenant dùng chung database:
/// chỉ cần một truy vấn quên lọc TenantId là dữ liệu cơ sở này lộ sang cơ sở khác.
/// Mọi entity nghiệp vụ mới thêm sau này PHẢI được bổ sung test tương tự.
///
/// Mỗi context được tạo sau khi tenant đã được resolve - giống hệt production,
/// nơi middleware của Finbuckle xác định tenant trước khi DI tạo scoped DbContext.
/// </summary>
public class TenantIsolationTests
{
    private const string CoSoA = "coso-a";
    private const string CoSoB = "coso-b";

    private readonly string _dbName = Guid.NewGuid().ToString();

    /// <summary>Mở một "phiên làm việc" của một cơ sở trên cùng một database.</summary>
    private AppDbContext OpenAs(string tenantId)
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(tenantId);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        return new AppDbContext(accessor, options);
    }

    [Fact]
    public void CoSo_Chi_Doc_Duoc_Du_Lieu_Cua_Chinh_Minh()
    {
        using (var db = OpenAs(CoSoA))
        {
            db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho cơ sở A", DiaChi = "Hà Nội" });
            db.SaveChanges();
        }

        using (var db = OpenAs(CoSoB))
        {
            // Mã kho trùng với cơ sở A - vẫn hợp lệ vì mã chỉ cần duy nhất trong từng cơ sở.
            db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho cơ sở B", DiaChi = "Hà Nội" });
            db.SaveChanges();

            var khoCuaB = db.Warehouses.ToList();
            Assert.Single(khoCuaB);
            Assert.Equal("Kho cơ sở B", khoCuaB[0].TenKho);
        }

        using (var db = OpenAs(CoSoA))
        {
            var khoCuaA = db.Warehouses.ToList();
            Assert.Single(khoCuaA);
            Assert.Equal("Kho cơ sở A", khoCuaA[0].TenKho);
        }
    }

    [Fact]
    public void TenantId_Duoc_Tu_Dong_Gan_Khi_Ghi()
    {
        using var db = OpenAs(CoSoA);

        // Cố tình KHÔNG gán TenantId - Finbuckle phải tự điền theo tenant hiện hành.
        var kho = new Warehouse { MaKho = "KHO09", TenKho = "Kho test", DiaChi = "Hà Nội" };
        db.Warehouses.Add(kho);
        db.SaveChanges();

        Assert.Equal(CoSoA, kho.TenantId);
    }

    [Fact]
    public void Khong_The_Ghi_De_Du_Lieu_Cua_Co_So_Khac()
    {
        int idCuaA;
        using (var db = OpenAs(CoSoA))
        {
            var khoCuaA = new Warehouse { MaKho = "KHO01", TenKho = "Kho cơ sở A", DiaChi = "Hà Nội" };
            db.Warehouses.Add(khoCuaA);
            db.SaveChanges();
            idCuaA = khoCuaA.Id;
        }

        using (var db = OpenAs(CoSoB))
        {
            // Cơ sở B cố tình sửa bản ghi của cơ sở A (mô phỏng request bị giả mạo Id).
            db.Warehouses.Update(new Warehouse
            {
                Id = idCuaA,
                TenantId = CoSoA,
                MaKho = "KHO01",
                TenKho = "ĐÃ BỊ SỬA TRÁI PHÉP",
                DiaChi = "Hà Nội"
            });

            // Finbuckle chặn ngay tại SaveChanges vì TenantId không khớp tenant hiện hành.
            Assert.ThrowsAny<Exception>(() => db.SaveChanges());
        }

        using (var db = OpenAs(CoSoA))
        {
            Assert.Equal("Kho cơ sở A", db.Warehouses.Single().TenKho);
        }
    }

    [Fact]
    public void Khong_Co_Tenant_Thi_Khong_Doc_Duoc_Du_Lieu_Nghiep_Vu()
    {
        using (var db = OpenAs(CoSoA))
        {
            db.Warehouses.Add(new Warehouse { MaKho = "KHO01", TenKho = "Kho cơ sở A", DiaChi = "Hà Nội" });
            db.SaveChanges();
        }

        // Tài khoản platform admin (không thuộc cơ sở nào) KHÔNG đọc được dữ liệu nghiệp vụ
        // qua context thường: truy vấn thất bại (fail-closed) thay vì trả về dữ liệu của
        // mọi cơ sở. Muốn giám sát xuyên tenant phải dùng đường truy vấn riêng, có kiểm soát.
        var accessor = new TestMultiTenantContextAccessor();
        accessor.ClearTenant();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        using var dbKhongTenant = new AppDbContext(accessor, options);

        Assert.ThrowsAny<Exception>(() => dbKhongTenant.Warehouses.ToList());
    }
}
