using HCP.Infrastructure.Services.TraCuu;
using QRCoder;

namespace HCP.Web.Api;

/// <summary>
/// Ảnh mã QR (PNG) của đơn hàng bán và lô sản xuất - dùng cookie đăng nhập web của cơ sở.
///   GET /app/qr/don-hang/{id}[?tai=true]   (tai=true: tải file về)
///   GET /app/qr/lo/{id}[?tai=true]
/// </summary>
public static class QrApi
{
    public static void MapQrApi(this IEndpointRouteBuilder app)
    {
        var qr = app.MapGroup("/app/qr").RequireAuthorization("NguoiDungCoSo");

        qr.MapGet("/don-hang/{id:int}", async (int id, bool? tai, ITraCuuCongKhaiService svc, IConfiguration cauHinh,
                                               HttpRequest req, CancellationToken ct) =>
            TraAnh(await svc.LayQrDonHangAsync(id, ct), LoaiTraCuu.DonHang, tai, cauHinh, req));

        qr.MapGet("/lo/{id:int}", async (int id, bool? tai, ITraCuuCongKhaiService svc, IConfiguration cauHinh,
                                         HttpRequest req, CancellationToken ct) =>
            TraAnh(await svc.LayQrLoAsync(id, ct), LoaiTraCuu.Lo, tai, cauHinh, req));
    }

    private static IResult TraAnh(QrTraCuu? qr, LoaiTraCuu loai, bool? tai, IConfiguration cauHinh, HttpRequest req)
    {
        if (qr is null) return Results.NotFound();

        var noiDung = DuongDanTraCuu.NoiDungQr(qr, loai, DuongDanTraCuu.Goc(cauHinh, $"{req.Scheme}://{req.Host}{req.PathBase}"));
        var png = TaoPng(noiDung);
        if (tai != true) return Results.File(png, "image/png");

        var tenFile = string.Concat(qr.Ma.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Results.File(png, "image/png", $"QR-{tenFile}.png");
    }

    /// <summary>PNG đen trắng, mức sửa lỗi M (chịu được tem hơi bẩn/nhàu), 20 px mỗi ô - đủ nét để in tem.</summary>
    public static byte[] TaoPng(string noiDung)
    {
        using var boTao = new QRCodeGenerator();
        using var duLieu = boTao.CreateQrCode(noiDung, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(duLieu).GetGraphic(20);
    }
}

/// <summary>Đường dẫn trang tra cứu công khai (nội dung mã QR).</summary>
public static class DuongDanTraCuu
{
    /// <summary>
    /// Gốc website công khai: khoá "Uploads:BaseUrl" (tên miền thật, vd https://quanly.tuannghiabakery.vn) - không lấy
    /// theo request vì sau reverse proxy request có thể là http://localhost; chưa khai thì mới dùng <paramref name="duPhong"/>.
    /// </summary>
    public static string Goc(IConfiguration cauHinh, string duPhong)
    {
        var goc = cauHinh["Uploads:BaseUrl"]?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(goc) ? duPhong.TrimEnd('/') : goc;
    }

    public static string NoiDungQr(QrTraCuu qr, LoaiTraCuu loai, string goc) =>
        qr.LinkNgoai ?? $"{goc}/tra-cuu/{(loai == LoaiTraCuu.DonHang ? "don-hang" : "lo")}/{qr.MaTraCuu}";
}
