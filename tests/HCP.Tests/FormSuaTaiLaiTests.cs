using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.Kho;
using HCP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace HCP.Tests;

/// <summary>
/// Form Sửa phải đổ dữ liệu MỚI NHẤT dưới CSDL, kể cả khi mở lần thứ hai trong cùng phiên làm việc.
///
/// Vì sao cần bài kiểm thử riêng: Blazor Server giữ một AppDbContext suốt phiên của người dùng. Truy vấn
/// CÓ theo dõi thay đổi (LayTheoIdAsync) mà gặp bản ghi đã nạp trước đó thì EF trả lại chính đối tượng cũ
/// đang giữ trong bộ nhớ, KHÔNG cập nhật giá trị mới dưới CSDL - đã tái hiện thật trên trình duyệt: mở form
/// sửa lần hai vẫn ra tên cũ dù CSDL đã đổi. LayDeSuaAsync đọc không theo dõi nên luôn ra bản mới.
/// </summary>
public class FormSuaTaiLaiTests
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

    private sealed class OutboxGia : ISyncOutboxWriter
    {
        public Task ThemAsync(string entityType, string entityKey, object payload, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<bool> DangBatAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<string?> GuiAsync(object banGhi, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    [Fact]
    public async Task Quy_Trinh_Mo_Form_Lan_Hai_Van_Ra_Ban_Moi_Nhat()
    {
        int id;
        using (var db = MoDb())
        {
            var qt = new ProductionProcess
            {
                MaQuyTrinh = "QT01", TenQuyTrinh = "Tên ban đầu",
                DanhSachKhau = { new ProcessStepLine { MaKhau = "K1", ThuTu = 1 } }
            };
            db.ProductionProcesses.Add(qt);
            await db.SaveChangesAsync();
            id = qt.Id;
        }

        // "Phiên làm việc" của người dùng: MỘT DbContext dùng lại cho mọi thao tác, như Blazor Server.
        using var phien = MoDb();
        var svc = new QuyTrinhSanXuatService(phien, new OutboxGia());

        // Mở form lần đầu bằng đường CÓ theo dõi -> bản ghi lọt vào bộ nhớ của phiên.
        var lanDau = await svc.LayTheoIdAsync(id);
        Assert.Equal("Tên ban đầu", lanDau!.TenQuyTrinh);

        // Người khác sửa bản ghi (phiên khác, như người dùng thứ hai hoặc job nền).
        using (var db2 = MoDb())
        {
            var qt = await db2.ProductionProcesses.FirstAsync(p => p.Id == id);
            qt.TenQuyTrinh = "Tên người khác vừa sửa";
            await db2.SaveChangesAsync();
        }

        // Chính là cái bẫy: đọc lại bằng đường CÓ theo dõi vẫn ra bản cũ trong bộ nhớ.
        var theoDoi = await svc.LayTheoIdAsync(id);
        Assert.Equal("Tên ban đầu", theoDoi!.TenQuyTrinh);

        // Còn đường dành cho form sửa thì ra đúng bản mới nhất.
        var lanHai = await svc.LayDeSuaAsync(id);
        Assert.Equal("Tên người khác vừa sửa", lanHai!.TenQuyTrinh);
        Assert.Single(lanHai.DanhSachKhau);   // bảng con vẫn nạp đủ khi đọc không theo dõi
    }

    [Fact]
    public async Task Khach_Hang_Mo_Form_Lan_Hai_Van_Ra_Ban_Moi_Nhat()
    {
        int id;
        using (var db = MoDb())
        {
            var kh = new KhachHang { MaKhachHang = "KH01", TenKhachHang = "Tên ban đầu" };
            db.KhachHangs.Add(kh);
            await db.SaveChangesAsync();
            id = kh.Id;
        }

        using var phien = MoDb();
        var svc = new KhachHangService(phien);
        Assert.Equal("Tên ban đầu", (await svc.LayDeSuaAsync(id))!.TenKhachHang);

        using (var db2 = MoDb())
        {
            var kh = await db2.KhachHangs.FirstAsync(k => k.Id == id);
            kh.TenKhachHang = "Tên người khác vừa sửa";
            await db2.SaveChangesAsync();
        }

        Assert.Equal("Tên người khác vừa sửa", (await svc.LayDeSuaAsync(id))!.TenKhachHang);
    }

    [Fact]
    public async Task Khong_Tim_Thay_Thi_Tra_Null_De_Man_Hinh_Bao_Loi()
    {
        using var db = MoDb();
        Assert.Null(await new KhachHangService(db).LayDeSuaAsync(123456));
        Assert.Null(await new QuyTrinhSanXuatService(db, new OutboxGia()).LayDeSuaAsync(123456));
    }
}
