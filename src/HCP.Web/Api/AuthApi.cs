using Finbuckle.MultiTenant.Abstractions;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Infrastructure;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HCP.Web.Api;

/// <summary>Nhóm API xác thực cho ứng dụng di động (JWT + refresh token xoay vòng).</summary>
public static class AuthApi
{
    public static void MapAuthApi(this IEndpointRouteBuilder app)
    {
        var nhom = app.MapGroup("/api/v1/auth").WithTags("Xác thực");

        nhom.MapPost("/dang-nhap", async (DangNhapRequest req, IMobileTokenService tokens, AppDbContext db,
                                          CancellationToken ct) =>
        {
            var kq = await tokens.DangNhapAsync(req.Email, req.MatKhau, req.ThietBi, ct);
            return kq.ThanhCong
                ? Results.Ok(await MapAsync(kq.Phien!, db, ct))
                : Results.Json(new LoiDto(kq.ThongBao!), statusCode: StatusCodes.Status401Unauthorized);
        });

        nhom.MapPost("/lam-moi", async (LamMoiRequest req, IMobileTokenService tokens, AppDbContext db,
                                        CancellationToken ct) =>
        {
            var kq = await tokens.LamMoiAsync(req.RefreshToken, req.ThietBi, ct);
            return kq.ThanhCong
                ? Results.Ok(await MapAsync(kq.Phien!, db, ct))
                : Results.Json(new LoiDto(kq.ThongBao!), statusCode: StatusCodes.Status401Unauthorized);
        });

        nhom.MapPost("/dang-xuat", async (DangXuatRequest req, IMobileTokenService tokens, CancellationToken ct) =>
        {
            await tokens.DangXuatAsync(req.RefreshToken, ct);
            return Results.Ok(new KetQuaDto(true, "Đã đăng xuất."));
        });

        // Kiểm tra token còn sống + lấy thông tin người dùng hiện tại. App gọi lại mỗi khi mở màn lệnh để biết
        // công tắc HanoiCheck mới nhất (đổi trên web thì app thấy ngay, không cần đăng nhập lại).
        nhom.MapGet("/toi", async (ClaimsPrincipal user, IMultiTenantContextAccessor tenant, AppDbContext db,
                                   CancellationToken ct) => Results.Ok(new NguoiDungDto(
                user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "",
                user.FindFirstValue("hoTen"),
                user.FindFirstValue(AppClaimTypes.TenantIdentifier),
                user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
                await HanoiCheckBatAsync(db, tenant.MultiTenantContext?.TenantInfo?.Id, ct),
                await MaNhanSuAsync(db, tenant.MultiTenantContext?.TenantInfo?.Id,
                                    user.FindFirstValue(ClaimTypes.NameIdentifier), ct))))
            .RequireAuthorization(ApiAuth.ChinhSach);

        // Đăng ký/bỏ đăng ký token thiết bị (FCM) để nhận thông báo đẩy - gọi lúc đăng nhập/đăng xuất.
        nhom.MapPost("/thiet-bi", async (DangKyThietBiRequest req, ClaimsPrincipal user, AppDbContext db,
                                         CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var token = req.Token.Trim();
            if (token.Length == 0) return Results.BadRequest(new LoiDto("Thiếu token thiết bị."));

            var hienCo = await db.PushDeviceTokens.FirstOrDefaultAsync(t => t.Token == token, ct);
            if (hienCo is null)
            {
                db.PushDeviceTokens.Add(new PushDeviceToken { UserId = userId, Token = token, ThietBi = req.ThietBi });
            }
            else
            {
                hienCo.UserId = userId;
                hienCo.ThietBi = req.ThietBi;
                hienCo.UpdatedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new KetQuaDto(true, "Đã đăng ký nhận thông báo."));
        }).RequireAuthorization(ApiAuth.ChinhSach);

        nhom.MapDelete("/thiet-bi", async (string token, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            await db.PushDeviceTokens.Where(t => t.Token == token.Trim() && t.UserId == userId).ExecuteDeleteAsync(ct);
            return Results.Ok(new KetQuaDto(true, "Đã bỏ đăng ký nhận thông báo."));
        }).RequireAuthorization(ApiAuth.ChinhSach);
    }

    private static async Task<PhienDto> MapAsync(PhienDangNhap p, AppDbContext db, CancellationToken ct) => new(
        p.AccessToken, p.AccessTokenHetHanUtc, p.RefreshToken, p.RefreshTokenHetHanUtc,
        new NguoiDungDto(p.NguoiDung.Id, p.NguoiDung.Email ?? "", p.NguoiDung.HoTen,
                         p.NguoiDung.TenantId, p.VaiTro, await HanoiCheckBatAsync(db, p.NguoiDung.TenantId, ct),
                         await MaNhanSuAsync(db, p.NguoiDung.TenantId, p.NguoiDung.Id, ct)));

    /// <summary>
    /// Mã nhân sự của tài khoản nhân viên (null với quản trị) - app dùng để biết có được quét mã lệnh tham gia khâu.
    /// Đọc thẳng theo TenantId vì lúc đăng nhập request chưa có ngữ cảnh cơ sở.
    /// </summary>
    private static async Task<string?> MaNhanSuAsync(AppDbContext db, string? tenantId, string? userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId)) return null;
        var nhanSuId = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.NhanSuId).FirstOrDefaultAsync(ct);
        return nhanSuId is not { } id
            ? null
            : await db.Staff.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == id && s.TenantId == tenantId).Select(s => s.MaNhanSu).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Công tắc tổng HanoiCheck (chưa có cấu hình kết nối = tắt). Đọc thẳng theo TenantId vì lúc đăng nhập request
    /// chưa có ngữ cảnh cơ sở.
    /// </summary>
    private static Task<bool> HanoiCheckBatAsync(AppDbContext db, string? tenantId, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(tenantId)
            ? Task.FromResult(false)
            : db.TenantHnCCredentials.AsNoTracking().AnyAsync(c => c.TenantId == tenantId && c.BatDongBo, ct);
}

/// <summary>Tên chính sách uỷ quyền dùng chung cho mọi API của ứng dụng di động.</summary>
public static class ApiAuth
{
    public const string ChinhSach = "ApiDiDong";
    public const string Scheme = JwtBearerDefaults.AuthenticationScheme;
}
