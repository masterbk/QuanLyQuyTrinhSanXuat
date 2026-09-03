using Finbuckle.MultiTenant;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Identity;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using HCP.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Chưa cấu hình ConnectionStrings:DefaultConnection. "
        + "Ở môi trường dev, đặt trong appsettings.Development.json (file này không được commit).");
}

// --- Database ---
// TenantStoreDbContext: context tối giản để Finbuckle tra cứu cơ sở ở đầu mỗi request.
builder.Services.AddDbContext<TenantStoreDbContext>(o => o.UseSqlServer(connectionString));

// AppDbContext: Identity + toàn bộ dữ liệu nghiệp vụ, đã cách ly theo tenant.
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

// --- Multi-tenant ---
// ClaimStrategy đọc claim "tenantId" của người dùng đang đăng nhập để xác định cơ sở.
// Người dùng chưa đăng nhập (trang chủ, đăng ký) không có tenant context - đúng thiết kế.
builder.Services
    .AddMultiTenant<Tenant>()
    .WithClaimStrategy(AppClaimTypes.TenantId)
    .WithEFCoreStore<TenantStoreDbContext, Tenant>();

// --- Identity ---
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Gắn claim tenantId khi đăng nhập - mắt xích nối Identity với Finbuckle.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppUserClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/dang-nhap";
    options.LogoutPath = "/dang-xuat";
    options.AccessDeniedPath = "/tu-choi-truy-cap";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// --- Dịch vụ nghiệp vụ ---
builder.Services.AddDataProtection();
builder.Services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
builder.Services.AddScoped<ICoSoService, CoSoService>();
builder.Services.AddScoped<IKetNoiHnCService, KetNoiHnCService>();

builder.Services.AddHttpClient<IHanoiCheckTokenClient, HanoiCheckTokenClient>(http =>
{
    http.Timeout = TimeSpan.FromSeconds(30);
});

// --- Phân quyền ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("QuanTriNenTang", p => p.RequireRole(AppRoles.PlatformSuperAdmin));
    options.AddPolicy("QuanTriCoSo", p => p.RequireRole(AppRoles.TenantAdmin));
    options.AddPolicy("NguoiDungCoSo", p => p.RequireRole(AppRoles.TenantAdmin, AppRoles.TenantStaff));
});

// --- UI ---
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Tài liệu HnC yêu cầu toàn bộ dữ liệu truyền trên mạng phải mã hoá SSL/TLS.
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();

// PHẢI đặt sau UseAuthentication (ClaimStrategy cần danh tính đã xác thực)
// và trước UseAuthorization / các endpoint truy cập dữ liệu.
app.UseMultiTenant();

app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await DbSeeder.SeedAsync(app.Services);

app.Run();
