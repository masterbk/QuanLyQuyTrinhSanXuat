using Finbuckle.MultiTenant;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.HanoiCheck;
using HCP.Infrastructure.Identity;
using HCP.Infrastructure.Logging;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Security;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Services;
using HCP.Infrastructure.Services.Dashboard;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.DonHangNhan;
using HCP.Infrastructure.Services.BanHang;
using HCP.Infrastructure.Services.MaTuSinh;
using HCP.Infrastructure.Services.Kho;
using HCP.Infrastructure.Services.NhatKyDongBo;
using HCP.Infrastructure.Services.ThongBao;
using HCP.Infrastructure.Sync;
using HCP.Web.Api;
using Microsoft.AspNetCore.Authentication;
using HCP.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
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
// ClaimStrategy đọc claim "tenantIdentifier" (KHÔNG phải "tenantId"): kho lưu tenant tra cứu
// bằng TryGetByIdentifierAsync, tức so khớp cột Identifier. Đưa nhầm Id vào đây thì
// TenantInfo là null và mọi truy vấn có bộ lọc tenant sẽ ném NullReferenceException.
// Người dùng chưa đăng nhập (trang chủ, đăng ký) không có tenant context - đúng thiết kế.
builder.Services
    .AddMultiTenant<Tenant>()
    .WithClaimStrategy(AppClaimTypes.TenantIdentifier)
    // Request từ ứng dụng di động mang JWT chứ không mang cookie, nên lúc UseMultiTenant chạy
    // thì HttpContext.User vẫn rỗng (UseAuthentication chỉ giải mã scheme mặc định là cookie).
    // Chiến lược này tự xác thực JWT cho các đường dẫn /api để lấy ra cơ sở - nhờ vậy toàn bộ
    // bộ lọc tenant của EF hoạt động y hệt bản web.
    .WithDelegateStrategy(async context =>
    {
        if (context is not HttpContext http) return null;
        if (!http.Request.Path.StartsWithSegments("/api")) return null;
        var kq = await http.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        return kq.Succeeded ? kq.Principal?.FindFirst(AppClaimTypes.TenantIdentifier)?.Value : null;
    })
    .WithEFCoreStore<TenantStoreDbContext, Tenant>();

// --- Identity ---
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        // Tài khoản nhân viên đăng nhập bằng số điện thoại, email không bắt buộc -> không để Identity bắt email.
        // Email vẫn không được trùng: đăng ký cơ sở và TaiKhoanNhanVienService tự kiểm tra.
        options.User.RequireUniqueEmail = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Gắn claim tenantId khi đăng nhập - mắt xích nối Identity với Finbuckle.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppUserClaimsPrincipalFactory>();

// Đổi quyền / khoá / đặt lại mật khẩu nhân viên thì phiên web cũ phải hết hiệu lực nhanh (mặc định 30 phút).
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/dang-nhap";
    options.LogoutPath = "/dang-xuat";
    options.AccessDeniedPath = "/tu-choi-truy-cap";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// --- Dịch vụ nghiệp vụ ---
// Data Protection dùng để mã hoá client_secret / hmac_secret / CCCD trước khi lưu DB.
// PHẢI lưu khoá vào thư mục CỐ ĐỊNH: nếu để mặc định, mỗi lần Application Pool tái chạy /
// app khởi động lại (nhất là trên IIS) sẽ sinh khoá mới -> không giải mã lại được dữ liệu đã
// lưu, gây lỗi "Không giải mã được client_secret".
//   - SetApplicationName cố định để đổi thư mục deploy cũng không làm lệch khoá.
//   - Đặt DataProtection:KeysPath trong cấu hình để trỏ tới thư mục ổn định, ghi được bởi tài
//     khoản chạy app pool, và KHÔNG bị xoá khi deploy lại. Mặc định dùng ProgramData.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(keysPath))
{
    // Mặc định: ngoài thư mục app (không bị xoá khi deploy lại).
    keysPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "HanoiCheckPlatform", "DataProtectionKeys");
}
else if (!Path.IsPathRooted(keysPath))
{
    // Đường dẫn tương đối -> tính từ thư mục app (cho phép lưu khoá NGAY TRONG thư mục build,
    // vd đặt "DataProtectionKeys"). Lưu ý: deploy kiểu xoá-sạch-rồi-chép sẽ xoá mất thư mục này.
    keysPath = Path.Combine(builder.Environment.ContentRootPath, keysPath);
}
Directory.CreateDirectory(keysPath);

builder.Services.AddDataProtection()
    .SetApplicationName("HanoiCheckPlatform")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
builder.Services.AddScoped<ICoSoService, CoSoService>();
builder.Services.AddScoped<IKetNoiHnCService, KetNoiHnCService>();

// Danh mục lõi của cơ sở. Tất cả đều dựa trên AppDbContext nên đã tự lọc theo tenant.
builder.Services.AddScoped<IDanhMucService<Warehouse>, KhoService>();
builder.Services.AddScoped<IDanhMucService<Facility>, CoSoSanXuatService>();
builder.Services.AddScoped<IDanhMucService<ProductionStep>, KhauSanXuatService>();
builder.Services.AddScoped<IDanhMucService<ProductionProcess>, QuyTrinhSanXuatService>();
builder.Services.AddScoped<IDanhMucService<SubSupplier>, NccDauVaoService>();
builder.Services.AddScoped<IDanhMucService<Staff>, NhanSuService>();
builder.Services.AddScoped<IDanhMucService<Product>, ThucPhamService>();
builder.Services.AddScoped<IDanhMucService<Batch>, LoSanXuatService>();
builder.Services.AddScoped<IDanhMucService<Dish>, MonAnService>();
builder.Services.AddScoped<IDanhMucChuanService, DanhMucChuanService>();
builder.Services.AddScoped<ISyncNhatKyService, SyncNhatKyService>();
builder.Services.AddScoped<IKhoNoiBoService, KhoNoiBoService>();
builder.Services.AddScoped<IDinhMucService, DinhMucService>();
builder.Services.AddScoped<ILenhSanXuatService, LenhSanXuatService>();
builder.Services.AddScoped<IMaTuSinhService, MaTuSinhService>();
builder.Services.AddScoped<ILuuTruAnhService, LuuTruAnhService>();
builder.Services.AddScoped<IKiemTraApiHnCService, KiemTraApiHnCService>();
builder.Services.AddScoped<IKhachHangService, KhachHangService>();
builder.Services.AddScoped<IDonHangBanService, DonHangBanService>();
builder.Services.AddScoped<IDonHangHnCService, DonHangHnCService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<IDonHangHnCTheoCoSo, DonHangHnCTheoCoSo>();
builder.Services.AddScoped<IDashboardCoSoService, DashboardCoSoService>();
builder.Services.AddScoped<IDashboardNenTangService, DashboardNenTangService>();

builder.Services.AddHttpClient<IHanoiCheckTokenClient, HanoiCheckTokenClient>(http =>
{
    http.Timeout = TimeSpan.FromSeconds(30);
});

// --- Engine đồng bộ HanoiCheck (Giai đoạn 3) ---
// HmacSigner không giữ trạng thái -> singleton. Nguồn thời gian tách riêng để kiểm thử được.
builder.Services.AddSingleton<IHmacSigner, HmacSigner>();
builder.Services.AddSingleton(TimeProvider.System);
// Các dịch vụ dưới đây dùng AppDbContext (scoped) nên phải scoped.
builder.Services.AddScoped<ITenantTokenManager, TenantTokenManager>();
builder.Services.AddScoped<IHanoiCheckSyncClient, HanoiCheckSyncClient>();
builder.Services.AddScoped<IHanoiCheckStandardFoodsClient, HanoiCheckStandardFoodsClient>();
builder.Services.AddScoped<ISystemLogWriter, SystemLogWriter>();
builder.Services.AddScoped<ISyncOutboxProcessor, SyncOutboxProcessor>();
builder.Services.AddScoped<ISyncOutboxWriter, SyncOutboxWriter>();
builder.Services.AddScoped<IDongBoHnCTongService, DongBoHnCTongService>();
builder.Services.AddScoped<TrangThaiHnC>();
builder.Services.AddScoped<ITaiKhoanNhanVienService, TaiKhoanNhanVienService>();
builder.Services.AddScoped<HCP.Infrastructure.Services.TraCuu.ITraCuuCongKhaiService, HCP.Infrastructure.Services.TraCuu.TraCuuCongKhaiService>();
builder.Services.AddScoped<IStandardFoodsSyncJob, StandardFoodsSyncJob>();
builder.Services.AddScoped<IHanoiCheckOrderQueryClient, HanoiCheckOrderQueryClient>();
builder.Services.AddScoped<IHanoiCheckOrderCommandClient, HanoiCheckOrderCommandClient>();
builder.Services.AddScoped<IDongBoDonHangJob, DongBoDonHangJob>();
builder.Services.AddScoped<IDonHangNhanService, DonHangNhanService>();

// HttpClient riêng cho việc gửi dữ liệu merge (khác client lấy token).
builder.Services.AddHttpClient(HanoiCheckSyncClient.HttpClientName, http =>
{
    http.Timeout = TimeSpan.FromSeconds(30);
});

// Hangfire: job nền đọc SyncOutbox và đẩy dữ liệu đi. Lưu trạng thái job trong chính SQL Server.
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();

// --- Thông báo đẩy (FCM) cho app mobile: đơn hàng mới/đổi trạng thái ---
// File JSON service account KHÔNG commit (giống appsettings.Development.json). Chưa cấu hình thì
// FirebaseApp.DefaultInstance vẫn null -> PushNotificationService tự bỏ qua gửi, không lỗi.
var firebaseCredPath = builder.Configuration["Firebase:CredentialsPath"];
if (!string.IsNullOrWhiteSpace(firebaseCredPath) && File.Exists(firebaseCredPath))
{
    // GoogleCredential.FromFile bị đánh Obsolete (khuyến nghị CredentialFactory) nhưng vẫn hoạt động
    // đúng - giữ cách gọi đơn giản, quen thuộc này cho tới khi có lý do thật để đổi.
#pragma warning disable CS0618
    var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(firebaseCredPath);
#pragma warning restore CS0618
    FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions { Credential = credential });
}

// --- Xác thực cho ứng dụng di động (chạy SONG SONG với cookie của web, không thay đổi web) ---
var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
builder.Services.AddScoped<IMobileTokenService, MobileTokenService>();
builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "HanoiCheckPlatform",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "HanoiCheckPlatform.Mobile",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey.Length >= 32 ? jwtKey : new string('x', 32))),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

// --- CORS chỉ cho môi trường phát triển ---
// App Flutter chạy thử trên trình duyệt nằm ở cổng khác nên bị chặn bởi CORS.
// Bản Android/iOS thật KHÔNG cần CORS, và bản phát hành cũng không bật chính sách này.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(o => o.AddPolicy("ApiDev", p => p
        .SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()));
}

// --- Phân quyền ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("QuanTriNenTang", p => p.RequireRole(AppRoles.PlatformSuperAdmin));
    options.AddPolicy("QuanTriCoSo", p => p.RequireRole(AppRoles.TenantAdmin));
    options.AddPolicy("NguoiDungCoSo", p => p.RequireRole(AppRoles.VaiTroCoSo));

    // API di động: CHỈ nhận JWT (không nhận cookie) để tránh bị gọi chéo từ trình duyệt đang
    // đăng nhập web - đó cũng là lý do không cần chống CSRF cho nhóm API này.
    options.AddPolicy(ApiAuth.ChinhSach, p => p
        .AddAuthenticationSchemes(ApiAuth.Scheme)
        .RequireAuthenticatedUser()
        .RequireRole(AppRoles.VaiTroCoSo));
});

// --- UI ---
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

var app = builder.Build();

// CỐ Ý KHÔNG đặt culture vi-VN cho toàn ứng dụng.
// Ngày tháng đã nhập/hiển thị đúng nhờ DateFormat="dd/MM/yyyy" đặt trên từng MudDatePicker,
// nên không cần đổi culture. Ngược lại, chuyển sang vi-VN sẽ biến dấu chấm thành phân cách
// hàng nghìn: người dùng gõ diện tích "250.75" bị hiểu thành 25075 - sai 100 lần và không
// có cảnh báo nào. Nếu sau này thực sự cần bản địa hoá, phải xử lý riêng ô nhập số
// và kiểm thử lại toàn bộ các trường thập phân.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Tài liệu HnC yêu cầu toàn bộ dữ liệu truyền trên mạng phải mã hoá SSL/TLS.
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// CORS PHẢI nằm giữa UseRouting và UseAuthorization: nếu đặt sau, phản hồi 401 do phân
// quyền sinh ra sẽ không kèm header CORS và trình duyệt báo lỗi kết nối thay vì 401.
if (app.Environment.IsDevelopment()) app.UseCors("ApiDev");

app.UseAuthentication();

// PHẢI đặt sau UseAuthentication (ClaimStrategy cần danh tính đã xác thực)
// và trước UseAuthorization / các endpoint truy cập dữ liệu.
app.UseMultiTenant();

app.UseAuthorization();

app.MapAuthApi();
app.MapLenhSanXuatApi();
app.MapDonHangApi();
app.MapQrApi();
app.MapKhoNoiBoApi();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// --- Tự động áp migration khi khởi động ---
// Hai context (TenantStoreDbContext + AppDbContext) dùng CHUNG một database nhưng có bộ
// migration riêng, nên phải áp cả hai. Nhờ vậy deploy lên server KHÔNG cần chạy tay
// "dotnet ef database update". Migrate chỉ chạy DDL nên không cần tenant context.
// Lưu ý: nếu chạy nhiều tiến trình/worker song song, chỉ nên để một tiến trình migrate;
// với một site IIS thông thường thì an toàn.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
    try
    {
        await sp.GetRequiredService<TenantStoreDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        logger.LogInformation("Đã áp dụng migration cơ sở dữ liệu (TenantStore + App).");
    }
    catch (Exception ex)
    {
        // Ném lại để lỗi hiện rõ khi khởi động thay vì chạy tiếp với schema thiếu.
        logger.LogCritical(ex, "Áp dụng migration cơ sở dữ liệu thất bại khi khởi động.");
        throw;
    }
}

await DbSeeder.SeedAsync(app.Services);

// Job nền quét hàng đợi đồng bộ mỗi phút. Chạy tự động, không cần thao tác thủ công.
// Dùng IRecurringJobManager (theo DI) thay cho API tĩnh RecurringJob - API tĩnh cần
// JobStorage.Current vốn chưa được set khi cấu hình Hangfire bằng AddHangfire.
// CancellationToken.None sẽ được Hangfire thay bằng token thật khi shutdown.
using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobs.AddOrUpdate<ISyncOutboxProcessor>(
        "dong-bo-hanoicheck",
        p => p.XuLyCacBanGhiDenHanAsync(50, CancellationToken.None),
        Cron.Minutely);

    // Danh mục thực phẩm chuẩn đổi rất chậm - cập nhật mỗi ngày một lần là đủ.
    recurringJobs.AddOrUpdate<IStandardFoodsSyncJob>(
        "cap-nhat-danh-muc-chuan",
        j => j.DongBoAsync(CancellationToken.None),
        Cron.Daily,
        new RecurringJobOptions { TimeZone = HCP.Domain.GioVietNam.MuiGio });

    // Kéo đơn hàng từ trường về (GET /orders) - đơn theo ngày, 15 phút một lần là đủ.
    recurringJobs.AddOrUpdate<IDongBoDonHangJob>(
        "keo-don-hang-hanoicheck",
        j => j.DongBoTatCaAsync(CancellationToken.None),
        "*/15 * * * *");
}

app.Run();
