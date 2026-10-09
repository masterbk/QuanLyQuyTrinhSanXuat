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
                                          IMultiTenantStore<Tenant> coSo, CancellationToken ct) =>
        {
            var kq = await tokens.DangNhapAsync(req.Email, req.MatKhau, req.ThietBi, ct);
            return kq.ThanhCong
                ? Results.Ok(await MapAsync(kq.Phien!, db, coSo, ct))
                : Results.Json(new LoiDto(kq.ThongBao!), statusCode: StatusCodes.Status401Unauthorized);
        });

        // Tự đổi mật khẩu: thành công thì mọi phiên app cũ bị thu hồi và trả PHIÊN MỚI cho thiết bị đang dùng
        // (giữ đăng nhập ở máy này, máy khác phải đăng nhập lại bằng mật khẩu mới).
        nhom.MapPost("/doi-mat-khau", async (DoiMatKhauRequest req, ClaimsPrincipal user, IDoiMatKhauService svc,
                                             IMobileTokenService tokens, AppDbContext db,
                                             IMultiTenantStore<Tenant> coSo, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var kq = await svc.DoiMatKhauAsync(userId, req.MatKhauHienTai, req.MatKhauMoi, ct);
            if (!kq.ThanhCong) return Results.BadRequest(new LoiDto(kq.ThongBao));

            var tenDangNhap = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.UserName).FirstAsync(ct);
            var phien = await tokens.DangNhapAsync(tenDangNhap!, req.MatKhauMoi, req.ThietBi, ct);
            return phien.ThanhCong
                ? Results.Ok(await MapAsync(phien.Phien!, db, coSo, ct))
                : Results.Ok(new KetQuaDto(true, kq.ThongBao + " Vui lòng đăng nhập lại."));
        }).RequireAuthorization(ApiAuth.ChinhSach);

        nhom.MapPost("/lam-moi", async (LamMoiRequest req, IMobileTokenService tokens, AppDbContext db,
                                        IMultiTenantStore<Tenant> coSo, CancellationToken ct) =>
        {
            var kq = await tokens.LamMoiAsync(req.RefreshToken, req.ThietBi, ct);
            return kq.ThanhCong
                ? Results.Ok(await MapAsync(kq.Phien!, db, coSo, ct))
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
                await LenhSanXuatApi.MaNhanSuTaiKhoanAsync(db, tenant.MultiTenantContext?.TenantInfo?.Id,
                    user.FindFirstValue(ClaimTypes.NameIdentifier), user.IsInRole(AppRoles.TenantAdmin), ct),
                tenant.MultiTenantContext?.TenantInfo?.Name)))
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

    private static async Task<PhienDto> MapAsync(PhienDangNhap p, AppDbContext db, IMultiTenantStore<Tenant> coSo,
                                                 CancellationToken ct) => new(
        p.AccessToken, p.AccessTokenHetHanUtc, p.RefreshToken, p.RefreshTokenHetHanUtc,
        new NguoiDungDto(p.NguoiDung.Id, p.NguoiDung.Email ?? "", p.NguoiDung.HoTen,
                         p.NguoiDung.TenantId, p.VaiTro, await HanoiCheckBatAsync(db, p.NguoiDung.TenantId, ct),
                         await LenhSanXuatApi.MaNhanSuTaiKhoanAsync(db, p.NguoiDung.TenantId, p.NguoiDung.Id,
                                                                    p.VaiTro.Contains(AppRoles.TenantAdmin), ct),
                         string.IsNullOrWhiteSpace(p.NguoiDung.TenantId)
                             ? null : (await coSo.TryGetAsync(p.NguoiDung.TenantId))?.Name));

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
