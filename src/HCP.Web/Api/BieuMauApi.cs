using System.Security.Claims;
using System.Text.Json;
using HCP.Domain.Constants;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using HCP.Infrastructure.Persistence;
using HCP.Infrastructure.Services.BieuMau;
using HCP.Web.Services;
using Microsoft.AspNetCore.Authorization;
using BieuMauEntity = HCP.Domain.Entities.Business.BieuMau;

namespace HCP.Web.Api;

/// <summary>API Biểu mẫu kiểm soát cho ứng dụng di động: tải mẫu được phép điền và gửi phiếu ghi nhận.</summary>
public static class BieuMauApi
{
    private static Task<IReadOnlyList<HCP.Domain.Entities.Business.BieuMau>> MauDuocNhapAsync(
        IPhieuGhiNhanService svc, ClaimsPrincipal user, string? maNhanSu) =>
        svc.LayBieuMauChoNhapAsync(AppRoles.VaiTroCoSo.Where(user.IsInRole).ToList(), maNhanSu);

    private static async Task<bool> DuocXemAsync(int bieuMauId, IPhieuGhiNhanService svc, ClaimsPrincipal user, AppDbContext db)
    {
        var duocXem = await svc.LayMauDuocXemAsync(AppRoles.VaiTroCoSo.Where(user.IsInRole).ToList(),
                                                    await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db));
        return duocXem is null || duocXem.Contains(bieuMauId);
    }

    /// <summary>Họ tên nhân sự (nếu tài khoản gắn nhân sự), không thì họ tên / tên đăng nhập của tài khoản.</summary>
    private static async Task<string?> TenNguoiLapAsync(AppDbContext db, string? maNhanSu, string? userId)
    {
        if (maNhanSu is not null)
        {
            var ten = await db.Staff.AsNoTracking().Where(s => s.MaNhanSu == maNhanSu).Select(s => s.HoTen).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(ten)) return ten;
        }
        if (userId is null) return null;
        var u = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => new { x.HoTen, x.UserName }).FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(u?.HoTen) ? u?.UserName : u.HoTen;
    }

    private static IResult KhongDuocXem() =>
        Results.Json(new LoiDto("Bạn không được giao biểu mẫu này - liên hệ quản trị cơ sở."), statusCode: 403);

    /// <summary>Tóm tắt phiếu cho danh sách: đếm ô Đạt/Không đạt, giá trị đầu phiếu dạng chữ.</summary>
    private static PhieuTomTatDto TomTat(PhieuGhiNhan p, BieuMauEntity? mau)
    {
        var truong = mau?.Truong ?? new List<TruongBieuMau>();
        var datKd = truong.Where(t => !t.LaDauPhieu && t.Kieu == KieuTruongBieuMau.DatKhongDat).Select(t => t.Ma).ToList();
        int dat = 0, khongDat = 0;
        foreach (var d in p.Dong)
        {
            var gt = DocJson(d.GiaTriJson);
            foreach (var ma in datKd)
            {
                var v = gt.GetValueOrDefault(ma);
                if (string.IsNullOrWhiteSpace(v)) continue;
                if (v.Trim().Equals("Đạt", StringComparison.OrdinalIgnoreCase)) dat++; else khongDat++;
            }
        }
        // Đầu phiếu: chỉ các kiểu hiển thị được ngay (chữ/số/lựa chọn), bỏ ô chọn danh mục và ảnh.
        var dau = DocJson(p.GiaTriDauJson);
        var dauPhieu = truong.Where(t => t.LaDauPhieu && t.Kieu is KieuTruongBieuMau.Text or KieuTruongBieuMau.So
                                                   or KieuTruongBieuMau.LuaChon)
            .OrderBy(t => t.ThuTu)
            .Select(t => (t.Ten, V: dau.GetValueOrDefault(t.Ma)))
            .Where(x => !string.IsNullOrWhiteSpace(x.V)).Select(x => $"{x.Ten}: {x.V}").ToList();
        return new PhieuTomTatDto(p.Id, p.BieuMauId, mau?.MaHieu ?? "", mau?.Ten ?? "(biểu mẫu đã xoá)",
            mau?.BoCuc.ToString() ?? "", p.Ngay, p.TrangThai.ToString(), p.NguoiLap, p.TenNguoiLap, p.ThoiGianUtc, p.Dong.Count,
            dat, khongDat, dauPhieu);
    }

    private static Dictionary<string, string?> DocJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static IResult KhongDuocNhap() =>
        Results.Json(new LoiDto("Bạn không được giao nhập biểu mẫu này - liên hệ quản trị cơ sở."), statusCode: 403);

    public static void MapBieuMauApi(this IEndpointRouteBuilder app)
    {
        var nhom = app.MapGroup("/api/v1")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.MoiNguoiDungCoSo })
            .WithTags("Biểu mẫu kiểm soát");

        // Danh sách biểu mẫu người đang đăng nhập được nhập (đang kích hoạt; nhân viên nhập biểu mẫu = mẫu được giao).
        nhom.MapGet("/bieu-mau", async (IPhieuGhiNhanService svc, ClaimsPrincipal user, AppDbContext db) =>
        {
            var ds = await MauDuocNhapAsync(svc, user, await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db));
            return Results.Ok(ds.Select(Map).ToList());
        });

        nhom.MapGet("/bieu-mau/{id:int}", async (int id, IPhieuGhiNhanService svc, ClaimsPrincipal user, AppDbContext db) =>
        {
            if (!await DuocXemAsync(id, svc, user, db)) return KhongDuocXem();
            var mau = await svc.LayBieuMauAsync(id);
            return mau is null ? Results.NotFound(new LoiDto("Không tìm thấy biểu mẫu.")) : Results.Ok(Map(mau));
        });

        // Danh sách thiết bị/dòng đến hoặc quá hạn (hiệu chuẩn, bảo dưỡng) trong soNgay ngày tới (mặc định 30).
        nhom.MapGet("/bieu-mau/nhac-han", async (IBieuMauService bmSvc, int? soNgay) =>
        {
            var ds = await bmSvc.LayNhacHanAsync(soNgay ?? 30);
            return Results.Ok(ds.Select(x => new NhacHanDto(
                x.BieuMauId, x.TenBieuMau, x.PhieuId, x.NgayPhieu, x.Nhan, x.TenTruong, x.Han, x.SoNgayConLai)).ToList());
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

        nhom.MapGet("/phieu-ghi-nhan/{id:int}", async (int id, IPhieuGhiNhanService svc, ClaimsPrincipal user, AppDbContext db) =>
        {
            var p = await svc.LayPhieuTheoIdAsync(id);
            if (p is null) return Results.NotFound(new LoiDto("Không tìm thấy phiếu."));
            return await DuocXemAsync(p.BieuMauId, svc, user, db) ? Results.Ok(MapPhieu(p)) : KhongDuocXem();
        });

        // Lịch sử phiếu cho app (mới nhất trước, phân trang): chỉ phiếu của các mẫu được xem; cuaToi = do tôi lập.
        nhom.MapGet("/phieu-ghi-nhan/lich-su", async (IPhieuGhiNhanService svc, ClaimsPrincipal user, AppDbContext db,
            DateOnly? tuNgay, DateOnly? denNgay, int? bieuMauId, bool cuaToi = false, int trang = 1, int soDong = 20) =>
        {
            var ma = await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db);
            var duocXem = await svc.LayMauDuocXemAsync(AppRoles.VaiTroCoSo.Where(user.IsInRole).ToList(), ma);
            var (t, n) = LenhSanXuatApi.ChuanHoaTrang(trang, soDong);
            var toi = cuaToi ? (ma, user.FindFirstValue(ClaimTypes.NameIdentifier)) : ((string?, string?)?)null;
            var (ds, tong) = await svc.LayLichSuAsync(duocXem, tuNgay, denNgay, bieuMauId, toi, t, n);
            var idMau = ds.Select(p => p.BieuMauId).Distinct().ToList();
            var mau = await db.BieuMaus.AsNoTracking().Include(b => b.Truong)
                .Where(b => idMau.Contains(b.Id)).ToDictionaryAsync(b => b.Id);
            return Results.Ok(new TrangDuLieu<PhieuTomTatDto>(
                ds.Select(p => TomTat(p, mau.GetValueOrDefault(p.BieuMauId))).ToList(), t, n, tong));
        });

        // PDF một phiếu (giống bản in trên web) để app xem / in / chia sẻ.
        nhom.MapGet("/phieu-ghi-nhan/{id:int}/pdf", async (int id, IPhieuGhiNhanService svc, IPhieuPdfService pdf,
                                                            ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var p = await svc.LayPhieuTheoIdAsync(id, ct);
            if (p is null) return Results.NotFound(new LoiDto("Không tìm thấy phiếu."));
            if (!await DuocXemAsync(p.BieuMauId, svc, user, db)) return KhongDuocXem();
            var bytes = await pdf.TaoPdfAsync(id, ct);
            return bytes is null ? Results.NotFound(new LoiDto("Không tìm thấy phiếu.")) : Results.File(bytes, "application/pdf", $"Phieu-{id}.pdf");
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
            var ma = await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db);
            if (!(await MauDuocNhapAsync(svc, user, ma)).Any(b => b.Id == req.BieuMauId)) return KhongDuocNhap();
            var phieu = TuRequest(req, ma);
            // Ghi tài khoản + tên người lập (tài khoản không gắn nhân sự như chủ cơ sở vẫn lọc "Do tôi lập" được).
            phieu.NguoiLapUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            phieu.TenNguoiLap = await TenNguoiLapAsync(db, ma, phieu.NguoiLapUserId);
            var kq = await svc.TaoPhieuAsync(phieu, req.HoanThanh ?? true);
            return kq.ThanhCong
                ? Results.Created($"/api/v1/phieu-ghi-nhan/{phieu.Id}", new KetQuaDto(true, kq.ThongBao))
                : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        nhom.MapPut("/phieu-ghi-nhan/{id:int}", async (int id, TaoPhieuRequest req, IPhieuGhiNhanService svc,
                                                       ClaimsPrincipal user, AppDbContext db) =>
        {
            var ma = await LenhSanXuatApi.MaNhanSuHienTaiAsync(user, db);
            var cu = await svc.LayPhieuTheoIdAsync(id);
            if (cu is null) return Results.NotFound(new LoiDto("Không tìm thấy phiếu."));
            if (!(await MauDuocNhapAsync(svc, user, ma)).Any(b => b.Id == cu.BieuMauId)) return KhongDuocNhap();
            var phieu = TuRequest(req, ma);
            phieu.Id = id;
            var kq = await svc.CapNhatPhieuAsync(phieu, req.HoanThanh ?? true);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        // Tải một ảnh cho trường kiểu Ảnh của phiếu; trả đường dẫn để lưu vào giá trị trường.
        nhom.MapPost("/phieu-ghi-nhan/anh", async (HttpRequest http, ILuuTruAnhService luuAnh, CancellationToken ct) =>
        {
            if (!http.HasFormContentType)
                return Results.BadRequest(new LoiDto("Cần gửi dạng multipart/form-data kèm ảnh."));
            IFormCollection form;
            try { form = await http.ReadFormAsync(ct); }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                return Results.BadRequest(new LoiDto("Dữ liệu ảnh gửi lên không đọc được. Vui lòng thử lại."));
            }
            var f = form.Files.FirstOrDefault(x => x.Length > 0);
            if (f is null) return Results.BadRequest(new LoiDto("Chưa chọn ảnh."));
            try
            {
                await using var luong = f.OpenReadStream();
                var anh = await luuAnh.LuuAsync(luong, f.FileName, f.ContentType, f.Length, ct);
                return Results.Ok(new AnhPhieuDto(anh.TenFile, anh.DuongDan));
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new LoiDto(ex.Message)); }
        }).DisableAntiforgery();
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
            t.Ma, t.Ten, t.Kieu.ToString(), t.LaDauPhieu, t.BatBuoc, t.DonVi, t.GiaTriChuan, t.TuyChonCsv, t.Nhom,
            t.LaHanNhac)).ToList(),
        b.HangMuc.OrderBy(h => h.ThuTu).Select(h => new HangMucBieuMauDto(
            h.Id, h.Ten, h.DienGiai, h.TanSuat)).ToList());

    private static PhieuGhiNhanDto MapPhieu(PhieuGhiNhan p) => new(
        p.Id, p.BieuMauId, p.Ngay, p.GiaTriDauJson, p.NguoiLap, p.TenNguoiLap, p.TrangThai.ToString(), p.GhiChu, p.ThoiGianUtc,
        p.Dong.OrderBy(d => d.ThuTu).Select(d => new DongGhiNhanDto(
            d.HangMucBieuMauId, d.ThuTu, d.GiaTriJson, d.GhiChu)).ToList());
}
