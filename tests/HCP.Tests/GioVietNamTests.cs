using HCP.Domain;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Giờ Việt Nam: DB lưu UTC, mọi chỗ hiển thị/gửi đi quy đổi UTC+7 không phụ thuộc múi giờ máy chủ.
/// </summary>
public class GioVietNamTests
{
    [Fact]
    public void Doi_Utc_Sang_Gio_Viet_Nam_Qua_Ngay_Hom_Sau()
    {
        var vn = GioVietNam.TuUtc(new DateTime(2026, 9, 13, 17, 30, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 9, 14, 0, 30, 0), vn);
        Assert.Equal(DateTimeKind.Unspecified, vn.Kind);

        // Giá trị EF đọc ra (Kind=Unspecified) cũng coi là UTC.
        Assert.Equal(new DateTime(2026, 9, 13, 9, 57, 56),
                     GioVietNam.TuUtc(new DateTime(2026, 9, 13, 2, 57, 56, DateTimeKind.Unspecified)));
        Assert.Null(GioVietNam.TuUtc((DateTime?)null));
    }

    [Fact]
    public void Hom_Nay_Theo_Gio_Viet_Nam()
    {
        var truoc = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var homNay = GioVietNam.HomNay;
        var sau = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        Assert.InRange(homNay, truoc, sau);   // đúng cả khi chạy test sát nửa đêm
    }

    [Fact]
    public async Task Cot_Utc_Doc_Ra_Mang_Kind_Utc_De_Api_Tra_Z()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant("coso-a");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        using (var db = new AppDbContext(accessor, options))
        {
            db.KhoGiaoDichs.Add(new KhoGiaoDich
            {
                MaSanPham = "SP01", MaKho = "K-01", MaLo = "LO-1", SoLuong = 1, Loai = LoaiGiaoDichKho.NhapThanhPham,
                ChungTu = "NK-1", ThoiGianUtc = new DateTime(2026, 9, 13, 2, 0, 0, DateTimeKind.Unspecified)
            });
            await db.SaveChangesAsync();
        }

        using (var db = new AppDbContext(accessor, options))
        {
            var gd = await db.KhoGiaoDichs.SingleAsync();
            Assert.Equal(DateTimeKind.Utc, gd.ThoiGianUtc.Kind);
            Assert.Equal(new DateTime(2026, 9, 13, 9, 0, 0), GioVietNam.TuUtc(gd.ThoiGianUtc));
        }
    }
}
