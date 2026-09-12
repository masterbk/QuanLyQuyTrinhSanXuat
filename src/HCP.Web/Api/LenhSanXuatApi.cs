using HCP.Domain.Entities.Business;
using HCP.Domain.Enums;
using HCP.Infrastructure.Services.DanhMuc;
using HCP.Infrastructure.Services.Kho;
using HCP.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HCP.Web.Api;

/// <summary>API Lệnh sản xuất cho ứng dụng di động - dùng lại nguyên các service của bản web.</summary>
public static class LenhSanXuatApi
{
    /// <summary>Ảnh chụp từ điện thoại: chặn ở mức này để một lần gửi không quá nặng.</summary>
    private const int SoAnhToiDa = 10;

    public static void MapLenhSanXuatApi(this IEndpointRouteBuilder app)
    {
        var nhom = app.MapGroup("/api/v1/lenh-san-xuat")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .WithTags("Lệnh sản xuất");

        nhom.MapGet("", async (ILenhSanXuatService svc, IDanhMucService<Product> sp,
                               string? trangThai, int trang = 1, int soDong = 20) =>
        {
            var tatCa = await svc.LayTatCaAsync();
            if (!string.IsNullOrWhiteSpace(trangThai) && Enum.TryParse<TrangThaiLenhSX>(trangThai, true, out var tt))
                tatCa = tatCa.Where(l => l.TrangThai == tt).ToList();

            var ten = await TenThanhPhamAsync(sp);
            var (t, n) = ChuanHoaTrang(trang, soDong);
            var trangDl = tatCa.Skip((t - 1) * n).Take(n).Select(l => Map(l, ten)).ToList();
            return Results.Ok(new TrangDuLieu<LenhSanXuatDto>(trangDl, t, n, tatCa.Count));
        });

        nhom.MapGet("/{id:int}", async (int id, ILenhSanXuatService svc, IDanhMucService<Product> sp) =>
        {
            var lenh = await svc.LayTheoIdAsync(id);
            return lenh is null
                ? Results.NotFound(new LoiDto("Không tìm thấy lệnh sản xuất."))
                : Results.Ok(Map(lenh, await TenThanhPhamAsync(sp)));
        });

        nhom.MapPost("", async (LenhSanXuatLuuRequest req, ILenhSanXuatService svc) =>
        {
            var lenh = TuRequest(new LenhSanXuat(), req);
            var kq = await svc.TaoAsync(lenh);
            return kq.ThanhCong
                ? Results.Created($"/api/v1/lenh-san-xuat/{lenh.Id}", new KetQuaDto(true, kq.ThongBao))
                : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

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

        // Hoàn thành: multipart/form-data, trường "anh" chứa 1..n ảnh chụp lô thành phẩm.
        nhom.MapPost("/{id:int}/hoan-thanh", async (int id, HttpRequest http,
                                                    ILenhSanXuatService svc, ILuuTruAnhService luuAnh,
                                                    CancellationToken ct) =>
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

            var files = form.Files.Where(f => f.Length > 0).Take(SoAnhToiDa).ToList();
            if (files.Count == 0)
                return Results.BadRequest(new LoiDto("Cần tải lên ít nhất 1 ảnh lô thành phẩm."));

            var anh = new List<AnhLoSanXuat>();
            foreach (var f in files)
            {
                try
                {
                    await using var luong = f.OpenReadStream();
                    anh.Add(await luuAnh.LuuAsync(luong, f.FileName, f.ContentType, f.Length, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new LoiDto(ex.Message));
                }
            }

            var kq = await svc.ThucHienAsync(id, anh, ct);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        }).DisableAntiforgery();

        nhom.MapPost("/{id:int}/huy", async (int id, HuyLenhRequest req, ILenhSanXuatService svc) =>
        {
            var kq = await svc.HuyAsync(id, req.LyDo);
            return kq.ThanhCong ? Results.Ok(new KetQuaDto(true, kq.ThongBao))
                                : Results.BadRequest(new LoiDto(kq.ThongBao));
        });

        nhom.MapGet("/nguyen-lieu-can", async (string maThanhPham, decimal soLuong, string maKho,
                                               ILenhSanXuatService svc) =>
        {
            var ds = await svc.TinhNguyenLieuCanAsync(maThanhPham, soLuong, maKho);
            return Results.Ok(ds.Select(x => new NguyenLieuCanDtoApi(
                x.MaNguyenLieu, x.TenNguyenLieu, x.DonViTinh, x.Can, x.Ton, x.Du)).ToList());
        });

        // Danh mục để app đổ vào ô chọn: chỉ thành phẩm ĐÃ có định mức mới sản xuất được.
        var dm = app.MapGroup("/api/v1/danh-muc")
            .RequireAuthorization(ApiAuth.ChinhSach)
            .WithTags("Danh mục");

        dm.MapGet("/thanh-pham", async (IDanhMucService<Product> sp, IDinhMucService dinhMuc) =>
        {
            var ds = new List<ThanhPhamDto>();
            foreach (var p in (await sp.LayTatCaAsync()).Where(p => p.LoaiSanPham == LoaiSanPham.ThanhPham))
            {
                if ((await dinhMuc.LayTheoThanhPhamAsync(p.Id)).Count > 0)
                    ds.Add(new ThanhPhamDto(p.MaSanPham, p.TenSanPham, p.DonViTinh));
            }
            return Results.Ok(ds);
        });

        dm.MapGet("/kho", async (IDanhMucService<Warehouse> kho) =>
            Results.Ok((await kho.LayTatCaAsync()).Select(k => new KhoDto(k.MaKho, k.TenKho)).ToList()));
    }

    internal static (int Trang, int SoDong) ChuanHoaTrang(int trang, int soDong) =>
        (Math.Max(1, trang), Math.Clamp(soDong, 1, 100));

    private static async Task<Dictionary<string, string>> TenThanhPhamAsync(IDanhMucService<Product> sp) =>
        (await sp.LayTatCaAsync()).GroupBy(p => p.MaSanPham)
            .ToDictionary(g => g.Key, g => g.First().TenSanPham);

    private static LenhSanXuat TuRequest(LenhSanXuat lenh, LenhSanXuatLuuRequest r)
    {
        lenh.MaLenh = r.MaLenh;
        lenh.MaThanhPham = r.MaThanhPham;
        lenh.SoLuong = r.SoLuong;
        lenh.MaKho = r.MaKho;
        lenh.MaLoThanhPham = r.MaLoThanhPham;
        lenh.HanSuDungThanhPham = r.HanSuDungThanhPham;
        lenh.NgaySanXuat = r.NgaySanXuat ?? DateOnly.FromDateTime(DateTime.Today);
        lenh.TaoLoDongBo = r.TaoLoDongBo;
        lenh.GhiChu = r.GhiChu;
        return lenh;
    }

    private static LenhSanXuatDto Map(LenhSanXuat l, IReadOnlyDictionary<string, string> ten) => new(
        l.Id, l.MaLenh, l.MaThanhPham, ten.GetValueOrDefault(l.MaThanhPham), l.SoLuong, l.MaKho,
        l.MaLoThanhPham, l.HanSuDungThanhPham, l.NgaySanXuat, l.TrangThai.ToString(),
        TenTrangThai(l.TrangThai), l.TaoLoDongBo, l.MaLoDaTao, l.ThoiGianHoanThanhUtc,
        l.ThoiGianHuyUtc, l.LyDoHuy, l.GhiChu,
        l.DanhSachAnh.Select(a => new AnhLenhDto(a.MaFile, a.TenFile, a.DuongDan)).ToList());

    private static string TenTrangThai(TrangThaiLenhSX t) => t switch
    {
        TrangThaiLenhSX.MoiTao => "Mới tạo",
        TrangThaiLenhSX.HoanThanh => "Hoàn thành",
        TrangThaiLenhSX.DaHuy => "Đã huỷ",
        _ => t.ToString()
    };
}
