using System.Security.Claims;
using System.Text.Json;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Business;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BieuMau;
using Microsoft.AspNetCore.Authorization;
using BieuMauEntity = HCP.Domain.Entities.Business.BieuMau;

namespace HCP.Web.Api;

/// <summary>API Biểu mẫu kiểm soát cho ứng dụng di động: tải mẫu được phép điền và gửi phiếu ghi nhận.</summary>
public static class BieuMauApi
{
    public static void MapBieuMauApi(this IEndpointRouteBuilder app)
    {
        var nhom = app.MapGroup("/api/v1")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.MoiNguoiDungCoSo })
            .WithTags("Biểu mẫu kiểm soát");

        // Danh sách biểu mẫu người đang đăng nhập được phép điền (theo vai trò + đang kích hoạt).
        nhom.MapGet("/bieu-mau", async (IPhieuGhiNhanService svc, ClaimsPrincipal user) =>
        {
            var vaiTro = AppRoles.VaiTroCoSo.Where(user.IsInRole).ToList();
            var ds = await svc.LayBieuMauChoNhapAsync(vaiTro);
            return Results.Ok(ds.Select(Map).ToList());
        });

        nhom.MapGet("/bieu-mau/{id:int}", async (int id, IPhieuGhiNhanService svc) =>
        {
            var mau = await svc.LayBieuMauAsync(id);
            return mau is null ? Results.NotFound(new LoiDto("Không tìm thấy biểu mẫu.")) : Results.Ok(Map(mau));
        });

        // Danh sách phiếu đã ghi; cuaToi=true thì chỉ của người đang đăng nhập.
        nhom.MapGet("/phieu-ghi-nhan", async (IPhieuGhiNhanService svc, ClaimsPrincipal user, AppDbContext db,
                                              DateOnly? ngay, int? bieuMauId, bool cuaToi = false) =>
        {
            string? nguoiLap = cuaToi ? await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db) : null;
            if (cuaToi && nguoiLap is null) return Results.Ok(Array.Empty<PhieuGhiNhanDto>());
            var ds = await svc.LayPhieuAsync(ngay, nguoiLap, bieuMauId);
            return Results.Ok(ds.Select(MapPhieu).ToList());
        });

        nhom.MapGet("/phieu-ghi-nhan/{id:int}", async (int id, IPhieuGhiNhanService svc) =>
        {
            var p = await svc.LayPhieuTheoIdAsync(id);
            return p is null ? Results.NotFound(new LoiDto("Không tìm thấy phiếu.")) : Results.Ok(MapPhieu(p));
        });

        nhom.MapPost("/phieu-ghi-nhan", async (TaoPhieuRequest req, IPhieuGhiNhanService svc,
                                               ClaimsPrincipal user, AppDbContext db) =>
        {
            var phieu = new PhieuGhiNhan
            {
                BieuMauId = req.BieuMauId,
                Ngay = req.Ngay ?? default,
                Ca = req.Ca,
                KhuVuc = req.KhuVuc,
                GhiChu = req.GhiChu,
                NguoiLap = await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db),
                Dong = (req.Dong ?? Array.Empty<DongGhiNhanRequest>()).Select(d => new DongGhiNhan
                {
                    HangMucBieuMauId = d.HangMucBieuMauId,
                    GiaTriJson = JsonSerializer.Serialize(d.GiaTri ?? new Dictionary<string, string?>()),
                    GhiChu = d.GhiChu
                }).ToList()
            };
            var kq = await svc.TaoPhieuAsync(phieu);
            return kq.ThanhCong
                ? Results.Created($"/api/v1/phieu-ghi-nhan/{phieu.Id}", new KetQuaDto(true, kq.ThongBao))
                : Results.BadRequest(new LoiDto(kq.ThongBao));
        });
    }

    private static BieuMauDto Map(BieuMauEntity b) => new(
        b.Id, b.MaHieu, b.Ten, b.BoCuc.ToString(), b.TanSuat, b.GhiChuChan,
        b.Truong.OrderBy(t => t.ThuTu).Select(t => new TruongBieuMauDto(
            t.Ma, t.Ten, t.Kieu.ToString(), t.BatBuoc, t.DonVi, t.GiaTriChuan, t.TuyChonCsv, t.Nhom)).ToList(),
        b.HangMuc.OrderBy(h => h.ThuTu).Select(h => new HangMucBieuMauDto(
            h.Id, h.Ten, h.DienGiai, h.TanSuat)).ToList());

    private static PhieuGhiNhanDto MapPhieu(PhieuGhiNhan p) => new(
        p.Id, p.BieuMauId, p.Ngay, p.Ca, p.KhuVuc, p.NguoiLap, p.TrangThai.ToString(), p.GhiChu, p.ThoiGianUtc,
        p.Dong.OrderBy(d => d.ThuTu).Select(d => new DongGhiNhanDto(
            d.HangMucBieuMauId, d.ThuTu, d.GiaTriJson, d.GhiChu)).ToList());
}
