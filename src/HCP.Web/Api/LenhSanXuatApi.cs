using Microsoft.EntityFrameworkCore;
using HCP.Infrastructure.Persistence;
using System.Security.Claims;
using HCP.Domain.Constants;
using HCP.Domain;
using System.Text.Json;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.Kho;
using HCP.Infrastructure.Services.TraCuu;
using HCP.Web.Services;

namespace HCP.Web.Api;

/// <summary>API Lệnh sản xuất cho ứng dụng di động - dùng lại nguyên các service của bản web.</summary>
public static class LenhSanXuatApi
{
    /// <summary>Ảnh chụp từ điện thoại: chặn ở mức này để một lần gửi không quá nặng.</summary>
    private const int SoAnhToiDa = 30;

    /// <summary>Tiền tố tên trường file trong multipart để biết ảnh thuộc dòng sản phẩm nào: anh_{id}.</summary>
    private const string TienToTruongAnh = "anh_";

    private static readonly JsonSerializerOptions JsonForm = new() { PropertyNameCaseInsensitive = true };

    public static void MapLenhSanXuatApi(this IEndpointRouteBuilder app)
    {
        var nhom = app.MapGroup("/api/v1/lenh-san-xuat")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = AppRoles.QuyenSanXuat })
            .WithTags("Lệnh sản xuất");

        nhom.MapGet("", async (ILenhSanXuatService svc, IDanhMucService<Product> sp,
                               IDanhMucService<ProductionProcess> qt, IDanhMucService<ProductionStep> khau,
                               IDanhMucService<Facility> coSo, ClaimsPrincipal user, AppDbContext db,
                               string? trangThai, int trang = 1, int soDong = 20, bool cuaToi = false) =>
        {
            var tatCa = await svc.LayTatCaAsync();
            if (!string.IsNullOrWhiteSpace(trangThai) && Enum.TryParse<TrangThaiLenhSX>(trangThai, true, out var tt))
                tatCa = tatCa.Where(l => l.TrangThai == tt).ToList();
            if (cuaToi)
            {
                // "Của tôi" = lệnh có khâu mà người đang đăng nhập là người thực hiện.
                var ma = await MaNhanSuHienTaiAsync(user, db);
                tatCa = ma is null
                    ? new List<LenhSanXuat>()
                    : tatCa.Where(l => l.SanPham.Any(s => s.Khau.Any(k =>
                          k.NguoiThucHien.Contains(ma, StringComparer.OrdinalIgnoreCase)))).ToList();
            }

            var ten = await LayTenAsync(sp, qt, khau, coSo);
            var (t, n) = ChuanHoaTrang(trang, soDong);
            var trangDl = tatCa.Skip((t - 1) * n).Take(n).Select(l => Map(l, ten)).ToList();
            return Results.Ok(new TrangDuLieu<LenhSanXuatDto>(trangDl, t, n, tatCa.Count));
        });

        nhom.MapGet("/{id:int}", async (int id, ILenhSanXuatService svc, IDanhMucService<Product> sp,
                                        IDanhMucService<ProductionProcess> qt,
                                        IDanhMucService<ProductionStep> khau,
                                        IDanhMucService<Facility> coSo) =>
        {
            var lenh = await svc.LayTheoIdAsync(id);
            return lenh is null
                ? Results.NotFound(new LoiDto("Không tìm thấy lệnh sản xuất."))
                : Results.Ok(Map(lenh, await LayTenAsync(sp, qt, khau, coSo)));
        });

        // Ảnh QR (PNG, kèm dòng chữ mã lệnh dưới mã QR) để xem/tải trên app - cùng nội dung QR với web
        // (nội dung "LSX:<mã lệnh>" cho nhân viên sản xuất quét tham gia khâu, xem mục /theo-ma dưới đây).
        nhom.MapGet("/{id:int}/qr", async (int id, ITraCuuCongKhaiService traCuu, IConfiguration cauHinh,
                                          HttpRequest req, CancellationToken ct) =>
        {
            var qr = await traCuu.LayQrLenhSanXuatAsync(id, ct);
            if (qr is null) return Results.NotFound(new LoiDto("Không tìm thấy lệnh sản xuất."));

            var goc = DuongDanTraCuu.Goc(cauHinh, $"{req.Scheme}://{req.Host}{req.PathBase}");
            var noiDung = DuongDanTraCuu.NoiDungQr(qr, LoaiTraCuu.LenhSanXuat, goc);
            var png = QrApi.TaoPng(noiDung, QrApi.CacDongChu(qr, LoaiTraCuu.LenhSanXuat));
            return Results.Bytes(png, "image/png");
        });

        // App quét mã QR lệnh (nội dung "LSX:<mã lệnh>") rồi tra lệnh theo mã.
        nhom.MapGet("/theo-ma/{maLenh}", async (string maLenh, ILenhSanXuatService svc, IDanhMucService<Product> sp,
                                                IDanhMucService<ProductionProcess> qt,
                                                IDanhMucService<ProductionStep> khau,
                                                IDanhMucService<Facility> coSo) =>
        {
            var lenh = await svc.LayTheoMaAsync(maLenh);
            return lenh is null
                ? Results.NotFound(new LoiDto($"Không tìm thấy lệnh sản xuất \"{maLenh}\"."))
                : Results.Ok(Map(lenh, await LayTenAsync(sp, qt, khau, coSo)));
        });

        // Nhân viên sản xuất tham gia các khâu đã chọn (chọn rỗng = rời lệnh).
        nhom.MapPost("/{id:int}/tham-gia", async (int id, ThamGiaLenhRequest req, ClaimsPrincipal user, AppDbContext db,
                                                  ILenhSanXuatService svc) =>
        {
            var maNhanSu = await MaNhanSuHienTaiAsync(user, db);
            if (maNhanSu is null)
                return Results.BadRequest(new LoiDto(
                    "Tài khoản chưa gắn với hồ sơ nhân sự đang làm việc - liên hệ quản trị cơ sở."));
            var kq = await svc.ThamGiaAsync(id, maNhanSu, req.KhauIds ?? Array.Empty<int>());
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        }).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = AppRoles.TenantSanXuat });

        nhom.MapPost("", async (LenhSanXuatLuuRequest req, ILenhSanXuatService svc) =>
        {
            var lenh = TuRequest(new LenhSanXuat(), req);
            var kq = await svc.TaoAsync(lenh);
            return kq.ThanhCong
                ? Results.Created($"/api/v1/lenh-san-xuat/{lenh.Id}", new KetQuaDto(true, kq.ThongBao))
                : Results.BadRequest(new LoiDto(kq.ThongBao));
        }).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = AppRoles.QuyenNhapLieu });

        nhom.MapPut("/{id:int}", async (int id, LenhSanXuatLuuRequest req, ILenhSanXuatService svc) =>
        {
            var lenh = TuRequest(new LenhSanXuat { Id = id }, req);
            var kq = await svc.CapNhatAsync(lenh);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        nhom.MapDelete("/{id:int}", async (int id, ILenhSanXuatService svc) =>
        {
            var kq = await svc.XoaAsync(id);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        // Hoàn thành: multipart/form-data.
        //   - ảnh: mỗi trường tên "anh_{idSanPham}" chứa 1..n ảnh của ĐÚNG lô đó (lệnh 1 sản phẩm
        //     thì chấp nhận tên trường "anh" cho gọn);
        //   - trường "khau" (tuỳ chọn): JSON mảng KhauSuaLaiRequest nếu người làm thực tế khác kế hoạch.
        nhom.MapPost("/{id:int}/hoan-thanh", async (int id, HttpRequest http,
                                                    ILenhSanXuatService svc, ILuuTruAnhService luuAnh,
                                                    ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            if (!http.HasFormContentType)
                return Results.BadRequest(new LoiDto("Cần gửi dạng multipart/form-data kèm ảnh lô thành phẩm."));

            IFormCollection form;
            try
            {
                form = await http.ReadFormAsync(ct);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                // Mạng di động hay đứt giữa chừng -> phần multipart hỏng. Trả lỗi rõ ràng để app
                // biết mà gửi lại, thay vì để lọt thành 500 khó hiểu.
                return Results.BadRequest(new LoiDto("Dữ liệu ảnh gửi lên không đọc được. Vui lòng thử lại."));
            }

            var lenh = await svc.LayTheoIdAsync(id, ct);
            if (lenh is null) return Results.NotFound(new LoiDto("Không tìm thấy lệnh sản xuất."));

            var files = form.Files.Where(f => f.Length > 0).Take(SoAnhToiDa).ToList();
            if (files.Count == 0)
                return Results.BadRequest(new LoiDto("Cần tải lên ít nhất 1 ảnh lô thành phẩm."));

            var idHopLe = lenh.SanPham.Select(s => s.Id).ToHashSet();
            var theoDong = new Dictionary<int, List<AnhLoSanXuat>>();
            foreach (var f in files)
            {
                if (!ThuocDongNao(f.Name, lenh, idHopLe, out var sanPhamId))
                    return Results.BadRequest(new LoiDto(
                        $"Trường \"{f.Name}\" không gắn được với sản phẩm nào trong lệnh. "
                        + "Hãy đặt tên trường là anh_{id sản phẩm}."));

                try
                {
                    await using var luong = f.OpenReadStream();
                    var anh = await luuAnh.LuuAsync(luong, f.FileName, f.ContentType, f.Length, ct);
                    if (!theoDong.TryGetValue(sanPhamId, out var ds)) theoDong[sanPhamId] = ds = new();
                    ds.Add(anh);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new LoiDto(ex.Message));
                }
            }

            IReadOnlyList<LenhSanXuatKhau>? khauSuaLai = null;
            var khauJson = form["khau"].ToString();
            if (!string.IsNullOrWhiteSpace(khauJson))
            {
                try
                {
                    khauSuaLai = (JsonSerializer.Deserialize<List<KhauSuaLaiRequest>>(khauJson, JsonForm) ?? new())
                        .Select(k => new LenhSanXuatKhau
                        {
                            Id = k.Id, MaCoSo = k.MaCoSo, GhiChu = k.GhiChu,
                            NguoiThucHienCsv = string.Join(",", k.NguoiThucHien ?? Array.Empty<string>())
                        }).ToList();
                }
                catch (JsonException)
                {
                    return Results.BadRequest(new LoiDto("Trường \"khau\" không phải JSON hợp lệ."));
                }
            }

            // Quản trị/nhân viên nhập liệu luôn hoàn thành được; nhân viên sản xuất thuần chỉ hoàn thành
            // được lệnh mình có tham gia (kiểm tra ở service, cần đúng mã nhân sự đang đăng nhập).
            var coQuyenNhapLieu = user.IsInRole(AppRoles.TenantAdmin) || user.IsInRole(AppRoles.TenantStaff);
            var maNguoiThucHien = coQuyenNhapLieu ? null : await MaNhanSuHienTaiAsync(user, db);

            var anhTheoSanPham = theoDong.Select(x => new AnhTheoSanPham(x.Key, x.Value)).ToList();
            var kq = await svc.ThucHienAsync(id, anhTheoSanPham, khauSuaLai, coQuyenNhapLieu, maNguoiThucHien, ct);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        }).DisableAntiforgery();

        nhom.MapPost("/{id:int}/huy", async (int id, HuyLenhRequest req, ILenhSanXuatService svc) =>
        {
            var kq = await svc.HuyAsync(id, req.LyDo);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        }).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = AppRoles.QuyenNhapLieu });

        // POST (không phải GET) vì nhu cầu tính trên NHIỀU dòng sản phẩm một lúc.
        nhom.MapPost("/nguyen-lieu-can", async (NguyenLieuCanRequest req, ILenhSanXuatService svc) =>
        {
            var dong = (req.Dong ?? Array.Empty<DongSanPhamRequest>())
                .Select(d => (d.MaThanhPham, d.SoLuong)).ToList();
            var ds = await svc.TinhNguyenLieuCanAsync(dong, req.MaKho);
            return Results.Ok(ds.Select(x => new NguyenLieuCanDtoApi(
                x.MaNguyenLieu, x.TenNguyenLieu, x.DonViTinh, x.Can, x.Ton, x.Du)).ToList());
        });

        MapDanhMuc(app);
    }

    /// <summary>Danh mục để app đổ vào các ô chọn khi lập lệnh.</summary>
    private static void MapDanhMuc(IEndpointRouteBuilder app)
    {
        var dm = app.MapGroup("/api/v1/danh-muc")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .WithTags("Danh mục");

        // Chỉ thành phẩm ĐÃ có định mức mới sản xuất được.
        dm.MapGet("/thanh-pham", async (IDanhMucService<Product> sp, IDinhMucService dinhMuc) =>
        {
            var ds = new List<ThanhPhamDto>();
            foreach (var p in (await sp.LayTatCaAsync()).Where(p => p.LoaiSanPham == LoaiSanPham.ThanhPham))
            {
                if ((await dinhMuc.LayTheoThanhPhamAsync(p.Id)).Count > 0)
                    ds.Add(new ThanhPhamDto(p.MaSanPham, p.TenSanPham, p.DonViTinh, p.MaQuyTrinh));
            }
            return Results.Ok(ds);
        });

        dm.MapGet("/kho", async (IDanhMucService<Warehouse> kho) =>
            Results.Ok((await kho.LayTatCaAsync()).Select(k => new KhoDto(k.MaKho, k.TenKho)).ToList()));

        // Mọi thành phẩm (không lọc theo có định mức) - dùng cho dòng hàng của Đơn hàng bán,
        // khác /thanh-pham ở trên vốn chỉ phục vụ lập lệnh sản xuất.
        dm.MapGet("/thanh-pham-ban", async (IDanhMucService<Product> sp) =>
            Results.Ok((await sp.LayTatCaAsync()).Where(p => p.LoaiSanPham == LoaiSanPham.ThanhPham)
                .Select(p => new ThanhPhamDto(p.MaSanPham, p.TenSanPham, p.DonViTinh, p.MaQuyTrinh)).ToList()));

        dm.MapGet("/khach-hang", async (IKhachHangService kh) =>
            Results.Ok((await kh.LayTatCaAsync())
                .Select(k => new KhachHangDto(k.MaKhachHang, k.TenKhachHang, k.DiaChi)).ToList()));

        dm.MapGet("/quy-trinh", async (IDanhMucService<ProductionProcess> qt,
                                       IDanhMucService<ProductionStep> khau) =>
        {
            var tenKhau = (await khau.LayTatCaAsync()).GroupBy(k => k.MaKhau)
                .ToDictionary(g => g.Key, g => g.First().TenKhau);
            return Results.Ok((await qt.LayTatCaAsync()).Select(q => new QuyTrinhDto(
                q.MaQuyTrinh, q.TenQuyTrinh,
                q.DanhSachKhau.OrderBy(k => k.ThuTu)
                    .Select(k => new KhauDto(k.MaKhau, tenKhau.GetValueOrDefault(k.MaKhau), k.ThuTu))
                    .ToList())).ToList());
        });

        dm.MapGet("/co-so", async (IDanhMucService<Facility> coSo) =>
            Results.Ok((await coSo.LayTatCaAsync())
                .Select(c => new CoSoDto(c.MaCoSo, c.TenCoSo, c.DiaChi)).ToList()));

        dm.MapGet("/nhan-su", async (IDanhMucService<Staff> ns) =>
            Results.Ok((await ns.LayTatCaAsync()).Where(n => n.TrangThai)
                .Select(n => new NhanSuDto(n.MaNhanSu, n.HoTen, n.ViTri)).ToList()));
    }

    internal static (int Trang, int SoDong) ChuanHoaTrang(int trang, int soDong) =>
        (Math.Max(1, trang), Math.Clamp(soDong, 1, 100));

    /// <summary>
    /// Xác định ảnh thuộc dòng sản phẩm nào qua tên trường "anh_{id}". Lệnh chỉ có một sản phẩm thì
    /// chấp nhận tên trường bất kỳ (app cũ gửi "anh") để không phải ép client đổi ngay.
    /// </summary>
    private static bool ThuocDongNao(string tenTruong, LenhSanXuat lenh, HashSet<int> idHopLe, out int sanPhamId)
    {
        if (tenTruong.StartsWith(TienToTruongAnh, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(tenTruong[TienToTruongAnh.Length..], out sanPhamId)
            && idHopLe.Contains(sanPhamId))
            return true;

        if (lenh.SanPham.Count == 1)
        {
            sanPhamId = lenh.SanPham[0].Id;
            return true;
        }

        sanPhamId = 0;
        return false;
    }

    /// <summary>Mã nhân sự (đang làm việc) gắn với tài khoản đang gọi API; null nếu tài khoản không gắn hồ sơ nhân sự.</summary>
    internal static async Task<string?> MaNhanSuHienTaiAsync(ClaimsPrincipal user, AppDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return null;
        var nhanSuId = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.NhanSuId).FirstOrDefaultAsync();
        return nhanSuId is not { } id
            ? null
            : await db.Staff.AsNoTracking().Where(s => s.Id == id && s.TrangThai).Select(s => s.MaNhanSu).FirstOrDefaultAsync();
    }

    private sealed record BangTen(
        IReadOnlyDictionary<string, string> ThanhPham,
        IReadOnlyDictionary<string, string> QuyTrinh,
        IReadOnlyDictionary<string, string> Khau,
        IReadOnlyDictionary<string, string> CoSo);

    private static async Task<BangTen> LayTenAsync(
        IDanhMucService<Product> sp, IDanhMucService<ProductionProcess> qt,
        IDanhMucService<ProductionStep> khau, IDanhMucService<Facility> coSo) => new(
        (await sp.LayTatCaAsync()).GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First().TenSanPham),
        (await qt.LayTatCaAsync()).GroupBy(p => p.MaQuyTrinh).ToDictionary(g => g.Key, g => g.First().TenQuyTrinh),
        (await khau.LayTatCaAsync()).GroupBy(p => p.MaKhau).ToDictionary(g => g.Key, g => g.First().TenKhau),
        (await coSo.LayTatCaAsync()).GroupBy(p => p.MaCoSo).ToDictionary(g => g.Key, g => g.First().TenCoSo));

    private static LenhSanXuat TuRequest(LenhSanXuat lenh, LenhSanXuatLuuRequest r)
    {
        lenh.MaLenh = r.MaLenh ?? "";
        lenh.MaKho = r.MaKho;
        lenh.NgaySanXuat = r.NgaySanXuat ?? GioVietNam.HomNay;
        lenh.TaoLoDongBo = r.TaoLoDongBo;
        lenh.GhiChu = r.GhiChu;
        lenh.SanPham = (r.SanPham ?? Array.Empty<LenhSanXuatSanPhamRequest>()).Select(s => new LenhSanXuatSanPham
        {
            MaThanhPham = s.MaThanhPham,
            SoLuong = s.SoLuong,
            MaLoThanhPham = s.MaLoThanhPham ?? "",
            HanSuDung = s.HanSuDung,
            MaQuyTrinh = s.MaQuyTrinh,
            Khau = (s.Khau ?? Array.Empty<LenhSanXuatKhauRequest>()).Select(k => new LenhSanXuatKhau
            {
                MaKhau = k.MaKhau,
                ThuTu = k.ThuTu,
                MaCoSo = k.MaCoSo,
                NguoiThucHienCsv = string.Join(",", k.NguoiThucHien ?? Array.Empty<string>()),
                GhiChu = k.GhiChu
            }).ToList()
        }).ToList();
        return lenh;
    }

    private static LenhSanXuatDto Map(LenhSanXuat l, BangTen ten) => new(
        l.Id, l.MaLenh, l.MaKho, l.NgaySanXuat, l.TrangThai.ToString(), TenTrangThai(l.TrangThai),
        l.TaoLoDongBo, l.ThoiGianHoanThanhUtc, l.ThoiGianHuyUtc, l.LyDoHuy, l.GhiChu,
        l.SanPham.Select(s => new LenhSanXuatSanPhamDto(
            s.Id, s.MaThanhPham, ten.ThanhPham.GetValueOrDefault(s.MaThanhPham), s.SoLuong,
            s.MaLoThanhPham, s.HanSuDung, s.MaQuyTrinh, ten.QuyTrinh.GetValueOrDefault(s.MaQuyTrinh),
            s.MaLoDaTao,
            s.Khau.OrderBy(k => k.ThuTu).Select(k => new LenhSanXuatKhauDto(
                k.Id, k.MaKhau, ten.Khau.GetValueOrDefault(k.MaKhau), k.ThuTu,
                k.MaCoSo, ten.CoSo.GetValueOrDefault(k.MaCoSo), k.NguoiThucHien, k.GhiChu)).ToList(),
            s.Anh.Select(a => new AnhLenhDto(a.MaFile, a.TenFile, a.DuongDan)).ToList())).ToList(),
        l.ThamGia.OrderBy(t => t.ThoiGianUtc)
            .Select(t => new LenhSanXuatThamGiaDto(t.MaNhanSu, t.HoTen, t.ThoiGianUtc)).ToList());

    private static string TenTrangThai(TrangThaiLenhSX t) => t switch
    {
        TrangThaiLenhSX.MoiTao => "Mới tạo",
        TrangThaiLenhSX.HoanThanh => "Hoàn thành",
        TrangThaiLenhSX.DaHuy => "Đã huỷ",
        _ => t.ToString()
    };
}
