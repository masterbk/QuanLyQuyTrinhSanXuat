using System.Security.Claims;
using HCP.Domain.Constants;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BieuMau;

namespace HCP.Web.Api;

/// <summary>
/// PDF của phiếu ghi nhận biểu mẫu - dùng cookie đăng nhập web của cơ sở.
///   GET /app/phieu/{id}/pdf[?tai=true]   (tai=true: tải file về, mặc định mở xem trong trình duyệt)
/// </summary>
public static class PhieuPdfApi
{
    public static void MapPhieuPdfApi(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/app/phieu").RequireAuthorization("NguoiDungCoSo");

        g.MapGet("/{id:int}/pdf", async (int id, IPhieuPdfService svc, IPhieuGhiNhanService phieuSvc, ClaimsPrincipal user,
                                         AppDbContext db, bool? tai, CancellationToken ct) =>
        {
            var phieu = await phieuSvc.LayPhieuTheoIdAsync(id, ct);
            if (phieu is null) return Results.NotFound();
            if (!await DuocXemAsync(phieu.BieuMauId, phieuSvc, user, db)) return Results.Forbid();
            var pdf = await svc.TaoPdfAsync(id, ct);
            if (pdf is null) return Results.NotFound();
            return tai == true
                ? Results.File(pdf, "application/pdf", $"Phieu-{id}.pdf")
                : Results.File(pdf, "application/pdf");
        });

        // Báo cáo tháng (mọi biểu mẫu; checklist in dạng ma trận): /app/phieu/bao-cao-thang?bieuMauId=&nam=&thang=[&tai=true]
        g.MapGet("/bao-cao-thang", async (int bieuMauId, int nam, int thang, bool? tai,
                                          IPhieuPdfService svc, IPhieuGhiNhanService phieuSvc, ClaimsPrincipal user,
                                          AppDbContext db, CancellationToken ct) =>
        {
            if (!await DuocXemAsync(bieuMauId, phieuSvc, user, db)) return Results.Forbid();
            var pdf = await svc.TaoBaoCaoThangAsync(bieuMauId, nam, thang, ct);
            if (pdf is null) return Results.NotFound();
            return tai == true
                ? Results.File(pdf, "application/pdf", $"BaoCao-{bieuMauId}-{thang:00}{nam}.pdf")
                : Results.File(pdf, "application/pdf");
        });
    }

    /// <summary>Chỉ xem PDF của biểu mẫu được giao (quản trị/nhập liệu: mọi mẫu).</summary>
    private static async Task<bool> DuocXemAsync(int bieuMauId, IPhieuGhiNhanService svc, ClaimsPrincipal user, AppDbContext db)
    {
        var duocXem = await svc.LayMauDuocXemAsync(AppRoles.VaiTroCoSo.Where(user.IsInRole).ToList(),
                                                    await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db));
        return duocXem is null || duocXem.Contains(bieuMauId);
    }
}
