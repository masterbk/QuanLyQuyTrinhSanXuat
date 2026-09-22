using HCP.Domain.Constants;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.Kho;
using Microsoft.AspNetCore.Authorization;

namespace HCP.Web.Api;

/// <summary>
/// API quản lý danh mục lõi cho ứng dụng di động: Thực phẩm/SKU (+ Định mức nguyên liệu), Khâu sản xuất,
/// Quy trình sản xuất. Dùng lại nguyên các service của bản web (QuanLyThucPham/QuanLyDinhMuc/
/// QuanLyKhauSanXuat/QuanLyQuyTrinh) nên cùng luật kiểm tra và cùng đồng bộ HanoiCheck.
/// Chỉ quản trị/nhân viên nhập liệu được sửa - xem/sản xuất dùng nhóm /api/v1/danh-muc (chỉ đọc).
/// </summary>
public static class QuanLyDanhMucApi
{
    public static void MapQuanLyDanhMucApi(this IEndpointRouteBuilder app)
    {
        var nhom = app.MapGroup("/api/v1/quan-ly")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.QuyenNhapLieu })
            .WithTags("Quản lý danh mục");

        MapThucPham(nhom);
        MapKhauSanXuat(nhom);
        MapQuyTrinh(nhom);
    }

    // ---------- Thực phẩm / SKU + Định mức nguyên liệu ----------

    private static void MapThucPham(RouteGroupBuilder nhom)
    {
        nhom.MapGet("/thuc-pham", async (IDanhMucService<Product> svc) =>
            Results.Ok((await svc.LayTatCaAsync()).OrderBy(p => p.MaSanPham).Select(Map).ToList()));

        nhom.MapGet("/thuc-pham/{id:int}", async (int id, IDanhMucService<Product> svc) =>
        {
            var sp = await svc.LayTheoIdAsync(id);
            return sp is null ? Results.NotFound(new LoiDto("Không tìm thấy thực phẩm.")) : Results.Ok(Map(sp));
        });

        nhom.MapPost("/thuc-pham", async (SanPhamLuuRequest req, IDanhMucService<Product> svc) =>
        {
            var kq = await svc.ThemAsync(TuRequest(new Product(), req));
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        nhom.MapPut("/thuc-pham/{id:int}", async (int id, SanPhamLuuRequest req, IDanhMucService<Product> svc) =>
        {
            var kq = await svc.CapNhatAsync(TuRequest(new Product { Id = id }, req));
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        nhom.MapDelete("/thuc-pham/{id:int}", async (int id, IDanhMucService<Product> svc) =>
        {
            var kq = await svc.XoaAsync(id);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        // Định mức: chỉ có ý nghĩa với thành phẩm - mỗi lần lưu thay thế toàn bộ danh sách.
        nhom.MapGet("/thuc-pham/{id:int}/dinh-muc", async (int id, IDinhMucService svc, IDanhMucService<Product> sp) =>
        {
            var ten = (await sp.LayTatCaAsync()).GroupBy(p => p.MaSanPham).ToDictionary(g => g.Key, g => g.First().TenSanPham);
            var ds = await svc.LayTheoThanhPhamAsync(id);
            return Results.Ok(ds.Select(d => new DinhMucDongDto(
                d.MaNguyenLieu, ten.GetValueOrDefault(d.MaNguyenLieu), d.SoLuong, d.HaoHutPhanTram)).ToList());
        });

        nhom.MapPut("/thuc-pham/{id:int}/dinh-muc", async (int id, DinhMucLuuRequest req, IDinhMucService svc) =>
        {
            var dong = (req.Dong ?? Array.Empty<DinhMucDongRequest>())
                .Select(d => new DinhMucNguyenLieu
                {
                    MaNguyenLieu = d.MaNguyenLieu, SoLuong = d.SoLuong, HaoHutPhanTram = d.HaoHutPhanTram
                });
            var kq = await svc.LuuAsync(id, dong);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        });
    }

    private static SanPhamQuanLyDto Map(Product p) => new(
        p.Id, p.DongBoHnC, p.MaSanPham, p.TenSanPham, p.LoaiSanPham.ToString(),
        p.DonViTinh, p.TonToiThieu, p.MaLoaiSp, p.MaThucPhamChuan, p.Gtin, p.QuocGia, p.MoTa, p.MaQuyTrinh);

    private static Product TuRequest(Product p, SanPhamLuuRequest r)
    {
        p.DongBoHnC = r.DongBoHnC;
        p.MaSanPham = r.MaSanPham;
        p.TenSanPham = r.TenSanPham;
        p.LoaiSanPham = Enum.TryParse<LoaiSanPham>(r.LoaiSanPham, true, out var l) ? l : LoaiSanPham.ThanhPham;
        p.DonViTinh = r.DonViTinh;
        p.TonToiThieu = r.TonToiThieu;
        p.MaLoaiSp = r.MaLoaiSp ?? "";
        p.MaThucPhamChuan = r.MaThucPhamChuan;
        p.Gtin = r.Gtin;
        p.QuocGia = r.QuocGia;
        p.MoTa = r.MoTa;
        p.MaQuyTrinh = r.MaQuyTrinh;
        return p;
    }

    // ---------- Khâu sản xuất ----------

    private static void MapKhauSanXuat(RouteGroupBuilder nhom)
    {
        nhom.MapGet("/khau-san-xuat", async (IDanhMucService<ProductionStep> svc) =>
            Results.Ok((await svc.LayTatCaAsync()).OrderBy(s => s.MaKhau).Select(Map).ToList()));

        nhom.MapPost("/khau-san-xuat", async (KhauLuuRequest req, IDanhMucService<ProductionStep> svc) =>
        {
            var kq = await svc.ThemAsync(new ProductionStep { DongBoHnC = req.DongBoHnC, TenKhau = req.TenKhau, GhiChu = req.GhiChu });
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        nhom.MapPut("/khau-san-xuat/{id:int}", async (int id, KhauLuuRequest req, IDanhMucService<ProductionStep> svc) =>
        {
            var kq = await svc.CapNhatAsync(new ProductionStep
            {
                Id = id, DongBoHnC = req.DongBoHnC, TenKhau = req.TenKhau, GhiChu = req.GhiChu
            });
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        nhom.MapDelete("/khau-san-xuat/{id:int}", async (int id, IDanhMucService<ProductionStep> svc) =>
        {
            var kq = await svc.XoaAsync(id);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        });
    }

    private static KhauQuanLyDto Map(ProductionStep s) => new(s.Id, s.DongBoHnC, s.MaKhau, s.TenKhau, s.GhiChu);

    // ---------- Quy trình sản xuất ----------

    private static void MapQuyTrinh(RouteGroupBuilder nhom)
    {
        nhom.MapGet("/quy-trinh", async (IDanhMucService<ProductionProcess> svc, IDanhMucService<ProductionStep> khau) =>
        {
            var tenKhau = await LayTenKhauAsync(khau);
            return Results.Ok((await svc.LayTatCaAsync()).OrderBy(p => p.MaQuyTrinh).Select(p => Map(p, tenKhau)).ToList());
        });

        nhom.MapGet("/quy-trinh/{id:int}", async (int id, IDanhMucService<ProductionProcess> svc,
                                                  IDanhMucService<ProductionStep> khau) =>
        {
            var qt = await svc.LayTheoIdAsync(id);
            return qt is null
                ? Results.NotFound(new LoiDto("Không tìm thấy quy trình."))
                : Results.Ok(Map(qt, await LayTenKhauAsync(khau)));
        });

        nhom.MapPost("/quy-trinh", async (QuyTrinhLuuRequest req, IDanhMucService<ProductionProcess> svc) =>
        {
            var kq = await svc.ThemAsync(TuRequest(new ProductionProcess(), req));
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        nhom.MapPut("/quy-trinh/{id:int}", async (int id, QuyTrinhLuuRequest req, IDanhMucService<ProductionProcess> svc) =>
        {
            var kq = await svc.CapNhatAsync(TuRequest(new ProductionProcess { Id = id }, req));
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        nhom.MapDelete("/quy-trinh/{id:int}", async (int id, IDanhMucService<ProductionProcess> svc) =>
        {
            var kq = await svc.XoaAsync(id);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        });
    }

    private static async Task<IReadOnlyDictionary<string, string>> LayTenKhauAsync(IDanhMucService<ProductionStep> khau) =>
        (await khau.LayTatCaAsync()).GroupBy(k => k.MaKhau).ToDictionary(g => g.Key, g => g.First().TenKhau);

    private static QuyTrinhQuanLyDto Map(ProductionProcess p, IReadOnlyDictionary<string, string> tenKhau) => new(
        p.Id, p.DongBoHnC, p.MaQuyTrinh, p.TenQuyTrinh, p.MaDanhMucThucPham,
        p.DanhSachKhau.OrderBy(k => k.ThuTu)
            .Select(k => new KhauDto(k.MaKhau, tenKhau.GetValueOrDefault(k.MaKhau), k.ThuTu)).ToList());

    private static ProductionProcess TuRequest(ProductionProcess p, QuyTrinhLuuRequest r)
    {
        p.DongBoHnC = r.DongBoHnC;
        p.MaQuyTrinh = r.MaQuyTrinh;
        p.TenQuyTrinh = r.TenQuyTrinh;
        p.MaDanhMucThucPham = r.MaDanhMucThucPham;
        p.DanhSachKhau = (r.DanhSachKhau ?? Array.Empty<QuyTrinhKhauRequest>())
            .Select(k => new ProcessStepLine { MaKhau = k.MaKhau, ThuTu = k.ThuTu }).ToList();
        return p;
    }
}
