using HCP.Domain;
using HCP.Domain.Constants;
using HCP.Infrastructure.Services.SuatAn;
using Microsoft.AspNetCore.Authorization;

namespace HCP.Web.Api;

/// <summary>
/// API suất ăn cho ứng dụng di động: hôm nay (hoặc một ngày bất kỳ) nấu món gì và bao nhiêu suất.
/// Chỉ ĐỌC - khai báo Món ăn và Thực đơn vẫn làm trên web vì là nhập liệu nặng, đẩy thẳng sang HanoiCheck.
/// Mở cho mọi người dùng của cơ sở: người đứng bếp, người giao hàng đều cần biết số suất.
/// </summary>
public static class SuatAnApi
{
    public static void MapSuatAnApi(this IEndpointRouteBuilder app)
    {
        var nhom = app.MapGroup("/api/v1/suat-an")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .RequireAuthorization(ChucNang.ChinhSach.SuatAn)
            .RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.MoiNguoiDungCoSo })
            .WithTags("Suất ăn");

        nhom.MapGet("", async (ISuatAnNgayService svc, DateOnly? ngay, CancellationToken ct) =>
            Results.Ok(await svc.LayTheoNgayAsync(ngay ?? GioVietNam.HomNay, ct)));
    }
}
