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

        // Phiếu NHÁP của người đang đăng nhập cho một biểu mẫu + ngày (để mở nhập tiếp). 204 nếu chưa có.
        nhom.MapGet("/phieu-ghi-nhan/nhap", async (IPhieuGhiNhanService svc, ClaimsPrincipal user, AppDbContext db,
                                                   int bieuMauId, DateOnly ngay) =>
        {
            var nguoiLap = await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db);
            var p = await svc.LayPhieuNhapAsync(bieuMauId, ngay, nguoiLap);
            return p is null ? Results.NoContent() : Results.Ok(MapPhieu(p));
        });

        // Phiếu của một biểu mẫu trong một NGÀY (mọi trạng thái) - cho biểu mẫu "1 phiếu/ngày":
        // còn nháp thì mở sửa, đã hoàn thành thì xem. 204 nếu ngày đó chưa có phiếu.
        nhom.MapGet("/phieu-ghi-nhan/theo-ngay", async (IPhieuGhiNhanService svc, int bieuMauId, DateOnly ngay) =>
        {
            var p = await svc.LayPhieuTheoNgayAsync(bieuMauId, ngay);
            return p is null ? Results.NoContent() : Results.Ok(MapPhieu(p));
        });

        nhom.MapPost("/phieu-ghi-nhan", async (TaoPhieuRequest req, IPhieuGhiNhanService svc,
                                               ClaimsPrincipal user, AppDbContext db) =>
        {
            var phieu = TuRequest(req, await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db));
            var kq = await svc.TaoPhieuAsync(phieu, req.HoanThanh ?? true);
            return kq.ThanhCong
                ? Results.Created($"/api/v1/phieu-ghi-nhan/{phieu.Id}", new KetQuaDto(true, kq.ThongBao))
                : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        nhom.MapPut("/phieu-ghi-nhan/{id:int}", async (int id, TaoPhieuRequest req, IPhieuGhiNhanService svc,
                                                       ClaimsPrincipal user, AppDbContext db) =>
        {
            var phieu = TuRequest(req, await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db));
            phieu.Id = id;
            var kq = await svc.CapNhatPhieuAsync(phieu, req.HoanThanh ?? true);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        });
    }

    private static PhieuGhiNhan TuRequest(TaoPhieuRequest req, string? nguoiLap) => new()
    {
        BieuMauId = req.BieuMauId,
        Ngay = req.Ngay ?? default,
        GiaTriDauJson = JsonSerializer.Serialize(req.GiaTriDau ?? new Dictionary<string, string?>()),
        GhiChu = req.GhiChu,
        NguoiLap = nguoiLap,
        Dong = (req.Dong ?? Array.Empty<DongGhiNhanRequest>()).Select(d => new DongGhiNhan
        {
            HangMucBieuMauId = d.HangMucBieuMauId,
            GiaTriJson = JsonSerializer.Serialize(d.GiaTri ?? new Dictionary<string, string?>()),
            GhiChu = d.GhiChu
        }).ToList()
    };

    private static BieuMauDto Map(BieuMauEntity b) => new(
        b.Id, b.MaHieu, b.Ten, b.BoCuc.ToString(), b.TanSuat, b.GhiChuChan, b.MotPhieuMoiNgay,
        b.Truong.OrderBy(t => t.ThuTu).Select(t => new TruongBieuMauDto(
            t.Ma, t.Ten, t.Kieu.ToString(), t.LaDauPhieu, t.BatBuoc, t.DonVi, t.GiaTriChuan, t.TuyChonCsv, t.Nhom)).ToList(),
        b.HangMuc.OrderBy(h => h.ThuTu).Select(h => new HangMucBieuMauDto(
            h.Id, h.Ten, h.DienGiai, h.TanSuat)).ToList());

    private static PhieuGhiNhanDto MapPhieu(PhieuGhiNhan p) => new(
        p.Id, p.BieuMauId, p.Ngay, p.GiaTriDauJson, p.NguoiLap, p.TrangThai.ToString(), p.GhiChu, p.ThoiGianUtc,
        p.Dong.OrderBy(d => d.ThuTu).Select(d => new DongGhiNhanDto(
            d.HangMucBieuMauId, d.ThuTu, d.GiaTriJson, d.GhiChu)).ToList());
}
