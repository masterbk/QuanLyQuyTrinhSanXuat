using HCP.Domain.Constants;
using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.Kho;
using Microsoft.AspNetCore.Authorization;

namespace HCP.Web.Api;

/// <summary>
/// API Kho nội bộ (tồn theo lô, nhập nguyên liệu, kiểm kê/điều chỉnh) cho ứng dụng di động - nghiệp vụ vận
/// hành nội bộ, KHÔNG đồng bộ sang HanoiCheck. Xem tồn/lịch sử: <see cref="AppRoles.QuyenSanXuat"/>;
/// nhập/điều chỉnh: <see cref="AppRoles.QuyenNhapLieu"/> (chồng thêm lên policy của cả nhóm).
/// </summary>
public static class KhoNoiBoApi
{
    public static void MapKhoNoiBoApi(this IEndpointRouteBuilder app)
    {
        var nhom = app.MapGroup("/api/v1/kho-noi-bo")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.QuyenSanXuat })
            .WithTags("Kho nội bộ");

        nhom.MapGet("/ton", async (IKhoNoiBoService svc) =>
            Results.Ok((await svc.LayTonAsync()).Select(t => new TonKhoDto(
                t.MaSanPham, t.TenSanPham, t.LoaiSanPham.ToString(), t.DonViTinh,
                t.MaKho, t.TenKho, t.MaLo, t.HanSuDung, t.SoLuongTon)).ToList()));

        nhom.MapGet("/lich-su", async (IKhoNoiBoService svc, IDanhMucService<Product> sp, int gioiHan = 100) =>
        {
            var ds = await svc.LayLichSuAsync(gioiHan);
            var ten = (await sp.LayTatCaAsync()).GroupBy(p => p.MaSanPham)
                .ToDictionary(g => g.Key, g => g.First().TenSanPham);
            return Results.Ok(ds.Select(g => new LichSuKhoDto(
                g.ThoiGianUtc, g.Loai.ToString(), TenLoai(g.Loai), g.MaSanPham,
                ten.GetValueOrDefault(g.MaSanPham), g.MaKho, g.MaLo, g.SoLuong, g.ChungTu, g.GhiChu)).ToList());
        });

        nhom.MapPost("/nhap", async (NhapKhoApiRequest req, IKhoNoiBoService svc) =>
        {
            var kq = await svc.NhapNguyenLieuAsync(new NhapKhoRequest
            {
                MaSanPham = req.MaSanPham, MaKho = req.MaKho, MaLo = req.MaLo, SoLuong = req.SoLuong,
                HanSuDung = req.HanSuDung, MaNccDauVao = req.MaNccDauVao, GhiChu = req.GhiChu
            });
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        }).RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.QuyenNhapLieu });

        nhom.MapPost("/dieu-chinh", async (DieuChinhTonApiRequest req, IKhoNoiBoService svc) =>
        {
            var kq = await svc.DieuChinhTonAsync(new DieuChinhTonRequest
            {
                MaSanPham = req.MaSanPham, MaKho = req.MaKho, MaLo = req.MaLo,
                SoLuongThucTe = req.SoLuongThucTe, LyDo = req.LyDo
            });
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao)) : Results.BadRequest(new LoiDto(kq.ThongBao));
        }).RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.QuyenNhapLieu });

        // Danh mục phục vụ màn nhập kho: nguyên liệu (khác /danh-muc/thanh-pham vốn chỉ trả thành phẩm) + NCC đầu vào.
        app.MapGet("/api/v1/danh-muc/nguyen-lieu", async (IDanhMucService<Product> sp) =>
            Results.Ok((await sp.LayTatCaAsync()).Where(p => p.LoaiSanPham == LoaiSanPham.NguyenLieu)
                .Select(p => new ThanhPhamDto(p.MaSanPham, p.TenSanPham, p.DonViTinh, p.MaQuyTrinh)).ToList()))
            .RequireAuthorization(ApiAuth.ChinhSach).WithTags("Danh mục");

        app.MapGet("/api/v1/danh-muc/ncc-dau-vao", async (IDanhMucService<SubSupplier> ncc) =>
            Results.Ok((await ncc.LayTatCaAsync())
                .Select(n => new NccDauVaoDto(n.MaNccDauVao, n.Ten)).ToList()))
            .RequireAuthorization(ApiAuth.ChinhSach).WithTags("Danh mục");
    }

    private static string TenLoai(LoaiGiaoDichKho l) => l switch
    {
        LoaiGiaoDichKho.NhapNguyenLieu => "Nhập nguyên liệu",
        LoaiGiaoDichKho.XuatSanXuat => "Xuất sản xuất",
        LoaiGiaoDichKho.NhapThanhPham => "Nhập thành phẩm",
        LoaiGiaoDichKho.XuatBan => "Xuất bán",
        LoaiGiaoDichKho.DieuChinh => "Điều chỉnh",
        LoaiGiaoDichKho.HoanTacSanXuat => "Huỷ sản xuất",
        LoaiGiaoDichKho.HoanTacXuatBan => "Huỷ xuất bán",
        _ => l.ToString()
    };
}
