using HCP.Domain.Entities.Business;
using HCP.Domain.Entities.Infrastructure;
using HCP.Domain.Enums;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services;
using HCP.Infrastructure.Services.Dashboard;
using HCP.Infrastructure.Services.NhatKyDongBo;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Kiểm chứng cảnh báo giấy tờ sắp/đã hết hạn trên dashboard cơ sở: chỉ cảnh báo giấy tờ
/// trong ngưỡng, đánh dấu đúng "đã hết hạn", và chỉ lấy dữ liệu của cơ sở đang đăng nhập.
/// </summary>
public class DashboardCoSoServiceTests
{
    private const string CoSoA = "coso-a";
    private const string CoSoB = "coso-b";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private AppDbContext MoDb(string tenantId)
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(accessor, options);
    }

    private DashboardCoSoService TaoService(AppDbContext db) =>
        new(db, new FakeNhatKy());

    [Fact]
    public async Task Chi_Canh_Bao_Giay_To_Trong_Nguong_Va_Danh_Dau_Da_Het_Han()
    {
        var homNay = DateOnly.FromDateTime(DateTime.Today);
        using (var db = MoDb(CoSoA))
        {
            db.SubSuppliers.Add(new SubSupplier
            {
                MaNccDauVao = "NCC01", Ten = "Trại A",
                AttpSoGiay = "ATTP01", AttpNgayCap = homNay.AddYears(-1),
                AttpNgayHetHan = homNay.AddDays(-5), // đã hết hạn
                NhomThucPham = { new SubSupplierFoodGroup { MaNhom = "THIT" } }
            });
            db.SubSuppliers.Add(new SubSupplier
            {
                MaNccDauVao = "NCC02", Ten = "Trại B",
                AttpSoGiay = "ATTP02", AttpNgayCap = homNay,
                AttpNgayHetHan = homNay.AddDays(400), // còn xa -> không cảnh báo
                NhomThucPham = { new SubSupplierFoodGroup { MaNhom = "RAU_CU" } }
            });
            db.Staff.Add(new Staff
            {
                MaNhanSu = "NV01", HoTen = "Nguyễn A",
                KskSoGiay = "KSK01", KskNgayKham = homNay.AddMonths(-6),
                KskNgayHetHan = homNay.AddDays(10) // sắp hết hạn
            });
            db.SaveChanges();
        }

        using var db2 = MoDb(CoSoA);
        var data = await TaoService(db2).LayAsync(soNgayCanhBao: 30);

        Assert.Equal(2, data.CanhBaoGiayTo.Count); // ATTP hết hạn + KSK sắp hết hạn
        var attp = data.CanhBaoGiayTo.Single(c => c.LoaiGiayTo == "Giấy chứng nhận ATTP");
        Assert.True(attp.DaHetHan);
        var ksk = data.CanhBaoGiayTo.Single(c => c.LoaiGiayTo == "Giấy khám sức khoẻ");
        Assert.False(ksk.DaHetHan);
    }

    [Fact]
    public async Task Chi_Thay_Giay_To_Cua_Co_So_Minh()
    {
        var homNay = DateOnly.FromDateTime(DateTime.Today);
        using (var db = MoDb(CoSoB))
        {
            db.Staff.Add(new Staff
            {
                MaNhanSu = "NV01", HoTen = "Của B",
                KskSoGiay = "KSK-B", KskNgayKham = homNay, KskNgayHetHan = homNay.AddDays(3)
            });
            db.SaveChanges();
        }

        using var dbA = MoDb(CoSoA);
        var data = await TaoService(dbA).LayAsync();

        Assert.Empty(data.CanhBaoGiayTo); // cơ sở A không thấy giấy tờ của cơ sở B
    }

    private sealed class FakeNhatKy : ISyncNhatKyService
    {
        public Task<IReadOnlyList<SyncOutboxItem>> LayDanhSachAsync(SyncOutboxStatus? loc = null, int gioiHan = 200, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SyncOutboxItem>>(Array.Empty<SyncOutboxItem>());
        public Task<IReadOnlyDictionary<SyncOutboxStatus, int>> DemTheoTrangThaiAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<SyncOutboxStatus, int>>(new Dictionary<SyncOutboxStatus, int>());
        public Task<KetQuaThaoTac> GuiLaiAsync(long id, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
