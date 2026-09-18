using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Business;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.Identity;
using HCP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HCP.Tests;

/// <summary>
/// Tài khoản đăng nhập cho nhân sự: tên đăng nhập là SĐT, nhiều vai trò, đổi quyền/khoá/đặt lại mật khẩu thu hồi phiên,
/// tắt tài khoản hoặc xoá nhân sự thì xoá tài khoản.
/// </summary>
public class TaiKhoanNhanVienServiceTests
{
    private const string CoSo = "coso-a";
    private const string MatKhau = "NhanVien@2026";
    private readonly string _dbName = Guid.NewGuid().ToString();

    private ServiceProvider DichVu()
    {
        var accessor = new TestMultiTenantContextAccessor();
        accessor.SetTenant(CoSo);

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSingleton<IMultiTenantContextAccessor>(accessor);
        sc.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        sc.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        sc.AddScoped<ITaiKhoanNhanVienService, TaiKhoanNhanVienService>();

        var sp = sc.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in AppRoles.All)
            if (!roles.RoleExistsAsync(r).GetAwaiter().GetResult())
                roles.CreateAsync(new IdentityRole(r)).GetAwaiter().GetResult();
        return sp;
    }

    private static Staff NhanSu(int id = 5, string? sdt = "0912 345 678", bool dangLam = true) =>
        new() { Id = id, MaNhanSu = $"NV{id}", HoTen = "Thợ bánh A", DienThoai = sdt, TrangThai = dangLam };

    private static YeuCauTaiKhoan YeuCau(params string[] vaiTro) =>
        new() { CoTaiKhoan = true, MatKhau = MatKhau, VaiTro = vaiTro.ToList() };

    [Fact]
    public async Task Tao_Tai_Khoan_Dang_Nhap_Bang_So_Dien_Thoai_Voi_Nhieu_Vai_Tro()
    {
        using var sp = DichVu();
        using (var scope = sp.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ITaiKhoanNhanVienService>();
            var kq = await svc.LuuAsync(NhanSu(), YeuCau(AppRoles.TenantSanXuat, AppRoles.TenantGiaoHang));
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        using (var scope = sp.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            // Nhập kiểu +84 có dấu cách vẫn tìm ra đúng tài khoản.
            var user = await TenDangNhap.TimAsync(um, "+84 912 345 678");
            Assert.NotNull(user);
            Assert.Equal("0912345678", user!.UserName);
            Assert.Equal(CoSo, user.TenantId);
            Assert.Equal(5, user.NhanSuId);
            Assert.True(await um.CheckPasswordAsync(user, MatKhau));
            Assert.Equal(new[] { AppRoles.TenantSanXuat, AppRoles.TenantGiaoHang }.OrderBy(x => x),
                         (await um.GetRolesAsync(user)).OrderBy(x => x));

            var ds = await scope.ServiceProvider.GetRequiredService<ITaiKhoanNhanVienService>().LayTheoCoSoAsync();
            Assert.Equal(new[] { AppRoles.TenantSanXuat, AppRoles.TenantGiaoHang }, ds[5].VaiTro);
        }
    }

    [Fact]
    public async Task Kiem_Tra_Chan_Du_Lieu_Tai_Khoan_Khong_Hop_Le()
    {
        using var sp = DichVu();
        using var scope = sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ITaiKhoanNhanVienService>();

        Assert.Contains("số điện thoại", await svc.KiemTraAsync(NhanSu(sdt: null), YeuCau(AppRoles.TenantStaff)));
        Assert.Contains("ít nhất một vai trò", await svc.KiemTraAsync(NhanSu(), YeuCau()));
        Assert.Contains("Nhập mật khẩu", await svc.KiemTraAsync(NhanSu(),
            new YeuCauTaiKhoan { CoTaiKhoan = true, VaiTro = { AppRoles.TenantStaff } }));
        Assert.Contains("Mật khẩu", await svc.KiemTraAsync(NhanSu(),
            new YeuCauTaiKhoan { CoTaiKhoan = true, MatKhau = "12345678", VaiTro = { AppRoles.TenantStaff } }));
        Assert.Contains("Email không hợp lệ", await svc.KiemTraAsync(NhanSu(),
            new YeuCauTaiKhoan { CoTaiKhoan = true, MatKhau = MatKhau, Email = "abc", VaiTro = { AppRoles.TenantStaff } }));
        Assert.Contains("không hợp lệ", await svc.KiemTraAsync(NhanSu(), YeuCau(AppRoles.TenantAdmin)));   // không tự cấp quyền quản trị

        // SĐT đã có tài khoản của nhân sự khác.
        Assert.True((await svc.LuuAsync(NhanSu(5), YeuCau(AppRoles.TenantStaff))).ThanhCong);
        Assert.Contains("đã được dùng", await svc.KiemTraAsync(NhanSu(6), YeuCau(AppRoles.TenantStaff)));
        Assert.Null(await svc.KiemTraAsync(NhanSu(5), new YeuCauTaiKhoan { CoTaiKhoan = true, VaiTro = { AppRoles.TenantStaff } }));
    }

    [Fact]
    public async Task Doi_Vai_Tro_Khoa_Dat_Lai_Mat_Khau_Thi_Thu_Hoi_Phien()
    {
        using var sp = DichVu();
        string userId, stampCu;
        using (var scope = sp.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ITaiKhoanNhanVienService>();
            Assert.True((await svc.LuuAsync(NhanSu(), YeuCau(AppRoles.TenantSanXuat, AppRoles.TenantGiaoHang))).ThanhCong);
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await um.FindByNameAsync("0912345678");
            userId = user!.Id;
            stampCu = user.SecurityStamp!;

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MobileRefreshTokens.Add(new MobileRefreshToken
            {
                UserId = userId, TokenHash = "hash", ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
            });
            await db.SaveChangesAsync();
        }

        using (var scope = sp.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ITaiKhoanNhanVienService>();
            var kq = await svc.LuuAsync(NhanSu(), new YeuCauTaiKhoan
            {
                CoTaiKhoan = true, MatKhau = "MatKhauMoi#2026", VaiTro = { AppRoles.TenantStaff }, DangHoatDong = false
            });
            Assert.True(kq.ThanhCong, kq.ThongBao);
        }

        using (var scope = sp.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await um.FindByIdAsync(userId);
            Assert.Equal(new[] { AppRoles.TenantStaff }, await um.GetRolesAsync(user!));
            Assert.False(user!.DangHoatDong);
            Assert.True(await um.CheckPasswordAsync(user, "MatKhauMoi#2026"));
            Assert.NotEqual(stampCu, user.SecurityStamp);                     // phiên web cũ hết hiệu lực
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull((await db.MobileRefreshTokens.SingleAsync()).RevokedAtUtc);   // phiên app bị thu hồi

            // Nhân sự nghỉ việc: tài khoản bị khoá dù ô "cho phép đăng nhập" bật.
            var svc = scope.ServiceProvider.GetRequiredService<ITaiKhoanNhanVienService>();
            await svc.LuuAsync(NhanSu(dangLam: false), new YeuCauTaiKhoan { CoTaiKhoan = true, VaiTro = { AppRoles.TenantStaff } });
        }

        using (var scope = sp.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.False((await um.FindByIdAsync(userId))!.DangHoatDong);
        }
    }

    [Fact]
    public async Task Tat_Tai_Khoan_Hoac_Xoa_Nhan_Su_Thi_Xoa_Tai_Khoan()
    {
        using var sp = DichVu();
        using (var scope = sp.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ITaiKhoanNhanVienService>();
            Assert.True((await svc.LuuAsync(NhanSu(5), YeuCau(AppRoles.TenantStaff))).ThanhCong);
            Assert.True((await svc.LuuAsync(NhanSu(6, "0987654321"), YeuCau(AppRoles.TenantGiaoHang))).ThanhCong);

            Assert.Contains("Đã xoá", (await svc.LuuAsync(NhanSu(5), new YeuCauTaiKhoan { CoTaiKhoan = false })).ThongBao);
            Assert.Contains("Đã xoá", (await svc.XoaTheoNhanSuAsync(6)).ThongBao);
            Assert.True((await svc.XoaTheoNhanSuAsync(99)).ThanhCong);   // không có tài khoản thì thôi
        }

        using (var scope = sp.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Empty(um.Users);
        }
    }
}
