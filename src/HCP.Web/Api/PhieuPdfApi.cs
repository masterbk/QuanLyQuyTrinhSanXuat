using System.IO.Compression;
using System.Security.Claims;
using HCP.Domain.Constants;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BieuMau;
using Microsoft.EntityFrameworkCore;

namespace HCP.Web.Api;

/// <summary>
/// PDF của phiếu ghi nhận biểu mẫu - dùng cookie đăng nhập web của cơ sở.
///   GET /app/phieu/{id}/pdf[?tai=true]   (tai=true: tải file về, mặc định mở xem trong trình duyệt)
/// </summary>
public static class PhieuPdfApi
{
    /// <summary>Số phiếu tối đa mỗi lần xuất zip (tránh treo máy chủ khi lọc quá rộng).</summary>
    public const int ToiDaXuatZip = 500;

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

        // Xuất nhiều phiếu theo bộ lọc: mỗi phiếu một PDF, nén chung 1 file zip.
        //   /app/phieu/xuat-zip?bieuMauId=&tuNgay=yyyy-MM-dd&denNgay=yyyy-MM-dd   (chỉ phiếu của mẫu được xem)
        g.MapGet("/xuat-zip", async (int? bieuMauId, DateOnly? tuNgay, DateOnly? denNgay, IPhieuPdfService pdf,
                                     IPhieuGhiNhanService phieuSvc, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var duocXem = await phieuSvc.LayMauDuocXemAsync(AppRoles.VaiTroCoSo.Where(user.IsInRole).ToList(),
                                                            await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db));
            var (ds, tong) = await phieuSvc.LayLichSuAsync(duocXem, tuNgay, denNgay, bieuMauId, null, 1, ToiDaXuatZip, ct);
            if (tong == 0) return Results.Text("Không có phiếu nào khớp bộ lọc.", statusCode: 404);
            if (tong > ToiDaXuatZip)
                return Results.Text($"Có {tong} phiếu, vượt giới hạn {ToiDaXuatZip} phiếu mỗi lần xuất - hãy thu hẹp khoảng ngày.", statusCode: 400);

            var maHieu = await db.BieuMaus.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.MaHieu, ct);
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var p in ds.OrderBy(x => x.Ngay).ThenBy(x => x.Id))
                {
                    var bytes = await pdf.TaoPdfAsync(p.Id, ct);
                    if (bytes is null) continue;
                    var ten = TenFile($"{p.Ngay:yyyy-MM-dd}_{maHieu.GetValueOrDefault(p.BieuMauId, "BM")}_{p.Id}.pdf");
                    var entry = zip.CreateEntry(ten, CompressionLevel.Fastest);
                    await using var s = entry.Open();
                    await s.WriteAsync(bytes, ct);
                }
            }
            var tenZip = $"Phieu_{(tuNgay?.ToString("yyyyMMdd") ?? "dau")}-{(denNgay?.ToString("yyyyMMdd") ?? "nay")}.zip";
            return Results.File(ms.ToArray(), "application/zip", tenZip);
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

    /// <summary>Tên file an toàn trong zip (bỏ ký tự cấm như / \ : * ? " &lt; &gt; |).</summary>
    private static string TenFile(string ten) =>
        string.Concat(ten.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <summary>Chỉ xem PDF của biểu mẫu được giao (quản trị/nhập liệu: mọi mẫu).</summary>
    private static async Task<bool> DuocXemAsync(int bieuMauId, IPhieuGhiNhanService svc, ClaimsPrincipal user, AppDbContext db)
    {
        var duocXem = await svc.LayMauDuocXemAsync(AppRoles.VaiTroCoSo.Where(user.IsInRole).ToList(),
                                                    await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db));
        return duocXem is null || duocXem.Contains(bieuMauId);
    }
}
