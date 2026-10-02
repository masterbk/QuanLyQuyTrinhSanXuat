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

    [Fact]
    public async Task Dem_Don_Hang_Theo_Trang_Thai_Tu_Moc_Thoi_Gian()
    {
        var moc = new DateOnly(2026, 9, 27);
        using (var db = MoDb(CoSoA))
        {
            // Trước mốc: KHÔNG được tính, dù trạng thái gì.
            db.DonHangBans.Add(Don("DH-TRUOC", moc.AddDays(-1), TrangThaiDonHangBan.ChoXacNhan));

            db.DonHangBans.Add(Don("DH-01", moc, TrangThaiDonHangBan.ChoXacNhan));
            db.DonHangBans.Add(Don("DH-02", moc.AddDays(1), TrangThaiDonHangBan.ChoXacNhan));
            db.DonHangBans.Add(Don("DH-03", moc.AddDays(1), TrangThaiDonHangBan.DaXacNhan));
            db.DonHangBans.Add(Don("DH-04", moc.AddDays(2), TrangThaiDonHangBan.DangGiao));
            // Chờ giao hàng = đã xuất kho, chưa ai nhận -> vẫn tính vào "đang giao".
            db.DonHangBans.Add(Don("DH-05", moc.AddDays(2), TrangThaiDonHangBan.ChoGiaoHang));
            db.DonHangBans.Add(Don("DH-06", moc.AddDays(3), TrangThaiDonHangBan.DaGiao));
            // Đã huỷ: không vào ô nào.
            db.DonHangBans.Add(Don("DH-07", moc.AddDays(3), TrangThaiDonHangBan.DaHuy));

            // Đơn trường đã xác nhận giao thành công trên HanoiCheck.
            var truong = Don("DH-08", moc.AddDays(3), TrangThaiDonHangBan.DaGiao);
            truong.Nguon = NguonDonHang.HanoiCheck;
            truong.MaDonHnC = "HNC-08";
            truong.TrangThaiHnC = "GIAO_HANG_THANH_CONG";
            db.DonHangBans.Add(truong);

            // Đơn HanoiCheck nhưng trường chưa xác nhận -> không tính vào ô đó.
            var chuaXacNhan = Don("DH-09", moc.AddDays(3), TrangThaiDonHangBan.DangGiao);
            chuaXacNhan.Nguon = NguonDonHang.HanoiCheck;
            chuaXacNhan.TrangThaiHnC = "DANG_GIAO";
            db.DonHangBans.Add(chuaXacNhan);

            db.SaveChanges();
        }

        using var db2 = MoDb(CoSoA);
        var dh = (await TaoService(db2).LayAsync(tuNgay: moc)).DonHang;

        Assert.Equal(moc, dh.TuNgay);
        Assert.Equal(2, dh.ChoXacNhan);   // DH-01, DH-02 (DH-TRUOC nằm trước mốc)
        Assert.Equal(1, dh.ChoXuatKho);   // DH-03
        Assert.Equal(3, dh.DangGiao);     // DH-04, DH-05 (chờ nhận), DH-09
        Assert.Equal(2, dh.DaGiao);       // DH-06, DH-08
        Assert.Equal(1, dh.TruongXacNhanGiaoThanhCong);  // DH-08
    }

    [Fact]
    public async Task Chi_Dem_Don_Hang_Cua_Co_So_Minh()
    {
        var moc = new DateOnly(2026, 9, 27);
        using (var db = MoDb(CoSoB))
        {
            db.DonHangBans.Add(Don("DH-B01", moc, TrangThaiDonHangBan.ChoXacNhan));
            db.SaveChanges();
        }

        using var dbA = MoDb(CoSoA);
        var dh = (await TaoService(dbA).LayAsync(tuNgay: moc)).DonHang;

        Assert.Equal(0, dh.ChoXacNhan);
    }

    private static DonHangBan Don(string ma, DateOnly ngayDat, TrangThaiDonHangBan trangThai) => new()
    {
        MaDonHang = ma,
        MaKhachHang = "KH01",
        MaKho = "KHO01",
        NgayDat = ngayDat,
        TrangThai = trangThai
    };

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
