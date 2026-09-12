using HCP.Domain.Constants;
using HCP.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HCP.Web.Api;

/// <summary>Nhóm API xác thực cho ứng dụng di động (JWT + refresh token xoay vòng).</summary>
public static class AuthApi
{
    public static void MapAuthApi(this IEndpointRouteBuilder app)
    {
        var nhom = app.MapGroup("/api/v1/auth").WithTags("Xác thực");

        nhom.MapPost("/dang-nhap", async (DangNhapRequest req, IMobileTokenService tokens,
                                          CancellationToken ct) =>
        {
            var kq = await tokens.DangNhapAsync(req.Email, req.MatKhau, req.ThietBi, ct);
            return kq.ThanhCong
                ? Results.Ok(Map(kq.Phien!))
                : Results.Json(new LoiDto(kq.ThongBao!), statusCode: StatusCodes.Status401Unauthorized);
        });

        nhom.MapPost("/lam-moi", async (LamMoiRequest req, IMobileTokenService tokens, CancellationToken ct) =>
        {
            var kq = await tokens.LamMoiAsync(req.RefreshToken, req.ThietBi, ct);
            return kq.ThanhCong
                ? Results.Ok(Map(kq.Phien!))
                : Results.Json(new LoiDto(kq.ThongBao!), statusCode: StatusCodes.Status401Unauthorized);
        });

        nhom.MapPost("/dang-xuat", async (DangXuatRequest req, IMobileTokenService tokens, CancellationToken ct) =>
        {
            await tokens.DangXuatAsync(req.RefreshToken, ct);
            return Results.Ok(new KetQuaDto(true, "Đã đăng xuất."));
        });

        // Kiểm tra token còn sống + lấy thông tin người dùng hiện tại.
        nhom.MapGet("/toi", (ClaimsPrincipal user) => Results.Ok(new NguoiDungDto(
                user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "",
                user.FindFirstValue("hoTen"),
                user.FindFirstValue(AppClaimTypes.TenantIdentifier),
                user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList())))
            .RequireAuthorization(ApiAuth.ChinhSach);
    }

    private static PhienDto Map(PhienDangNhap p) => new(
        p.AccessToken, p.AccessTokenHetHanUtc, p.RefreshToken, p.RefreshTokenHetHanUtc,
        new NguoiDungDto(p.NguoiDung.Id, p.NguoiDung.Email ?? "", p.NguoiDung.HoTen,
                         p.NguoiDung.TenantId, p.VaiTro));
}

/// <summary>Tên chính sách uỷ quyền dùng chung cho mọi API của ứng dụng di động.</summary>
public static class ApiAuth
{
    public const string ChinhSach = "ApiDiDong";
    public const string Scheme = JwtBearerDefaults.AuthenticationScheme;
}
