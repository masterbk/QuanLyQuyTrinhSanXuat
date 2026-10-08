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

        g.MapGet("/{id:int}/pdf", async (int id, IPhieuPdfService svc, bool? tai, CancellationToken ct) =>
        {
            var pdf = await svc.TaoPdfAsync(id, ct);
            if (pdf is null) return Results.NotFound();
            return tai == true
                ? Results.File(pdf, "application/pdf", $"Phieu-{id}.pdf")
                : Results.File(pdf, "application/pdf");
        });

        // Báo cáo tháng (mọi biểu mẫu; checklist in dạng ma trận): /app/phieu/bao-cao-thang?bieuMauId=&nam=&thang=[&tai=true]
        g.MapGet("/bao-cao-thang", async (int bieuMauId, int nam, int thang, bool? tai,
                                          IPhieuPdfService svc, CancellationToken ct) =>
        {
            var pdf = await svc.TaoBaoCaoThangAsync(bieuMauId, nam, thang, ct);
            if (pdf is null) return Results.NotFound();
            return tai == true
                ? Results.File(pdf, "application/pdf", $"BaoCao-{bieuMauId}-{thang:00}{nam}.pdf")
                : Results.File(pdf, "application/pdf");
        });
    }
}
