using Finbuckle.MultiTenant;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.Identity;
using HCP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:DefaultConnection.");

// --- Database ---
// TenantStoreDbContext: context tối giản để Finbuckle tra cứu cơ sở ở đầu mỗi request.
builder.Services.AddDbContext<TenantStoreDbContext>(o => o.UseSqlServer(connectionString));

// AppDbContext: Identity + toàn bộ dữ liệu nghiệp vụ, đã cách ly theo tenant.
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

// --- Multi-tenant ---
// ClaimStrategy đọc claim "tenantId" của người dùng đang đăng nhập để xác định cơ sở.
// Người dùng chưa đăng nhập (trang chủ, đăng ký) sẽ không có tenant context - đúng như thiết kế.
builder.Services
    .AddMultiTenant<Tenant>()
    .WithClaimStrategy(AppClaimTypes.TenantId)
    .WithEFCoreStore<TenantStoreDbContext, Tenant>();

// --- Identity ---
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

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

// PHẢI đặt sau UseAuthentication (ClaimStrategy cần danh tính đã được xác thực)
// và trước UseAuthorization / các endpoint truy cập dữ liệu.
app.UseMultiTenant();

app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
