using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
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

        qr.MapGet("/lenh-san-xuat/{id:int}", async (int id, bool? tai, ITraCuuCongKhaiService svc, IConfiguration cauHinh,
                                                    HttpRequest req, CancellationToken ct) =>
            TraAnh(await svc.LayQrLenhSanXuatAsync(id, ct), LoaiTraCuu.LenhSanXuat, tai, cauHinh, req));
    }

    private static IResult TraAnh(QrTraCuu? qr, LoaiTraCuu loai, bool? tai, IConfiguration cauHinh, HttpRequest req)
    {
        if (qr is null) return Results.NotFound();

        var noiDung = DuongDanTraCuu.NoiDungQr(qr, loai, DuongDanTraCuu.Goc(cauHinh, $"{req.Scheme}://{req.Host}{req.PathBase}"));
        var png = TaoPng(noiDung, ChuThich(qr, loai));
        if (tai != true) return Results.File(png, "image/png");

        var tenFile = string.Concat(qr.Ma.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Results.File(png, "image/png", $"QR-{tenFile}.png");
    }

    /// <summary>Dòng chữ in dưới ảnh QR - đơn hàng từ HanoiCheck thì qr.Ma đã là mã trên HanoiCheck
    /// (xem TraCuuCongKhaiService.LayQrDonHangAsync), đơn nội bộ thì là mã nội bộ. Dùng lại ở API mobile.</summary>
    internal static string ChuThich(QrTraCuu qr, LoaiTraCuu loai) => loai switch
    {
        LoaiTraCuu.DonHang => $"Đơn hàng {qr.Ma}",
        LoaiTraCuu.Lo => $"Lô {qr.Ma}",
        _ => $"Lệnh sản xuất {qr.Ma}"
    };

    /// <summary>PNG đen trắng, mức sửa lỗi M (chịu được tem hơi bẩn/nhàu), 20 px mỗi ô - đủ nét để in tem.
    /// Có <paramref name="chuThich"/> thì in thêm dòng chữ đó ngay dưới mã QR, trong viền trắng.
    /// Dùng System.Drawing.Common (MIT, có sẵn với .NET) - chỉ chạy trên Windows, khớp môi trường IIS
    /// đang triển khai (project đã khai TargetFramework net8.0-windows); KHÔNG dùng
    /// SixLabors.ImageSharp.Drawing vì gói đó đòi mua bản quyền thương mại (Six Labors Split License)
    /// tuỳ doanh thu công ty.</summary>
    public static byte[] TaoPng(string noiDung, string? chuThich = null)
    {
        using var boTao = new QRCodeGenerator();
        using var duLieu = boTao.CreateQrCode(noiDung, QRCodeGenerator.ECCLevel.M);
        var qrPng = new PngByteQRCode(duLieu).GetGraphic(20);
        if (string.IsNullOrWhiteSpace(chuThich)) return qrPng;

        using var msQr = new MemoryStream(qrPng);
        using var qrAnh = Image.FromStream(msQr);
        const int le = 24;      // viền trắng quanh mã QR
        const int caoChu = 56;  // chỗ cho dòng chữ dưới mã QR
        var rong = qrAnh.Width + le * 2;
        var cao = qrAnh.Height + le * 2 + caoChu;

        using var ghep = new Bitmap(rong, cao);
        using (var g = Graphics.FromImage(ghep))
        {
            g.Clear(Color.White);
            g.DrawImage(qrAnh, le, le, qrAnh.Width, qrAnh.Height);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var font = new Font("Arial", 20, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.FromArgb(0x1a, 0x2b, 0x4c));
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(chuThich, font, brush, new RectangleF(0, qrAnh.Height + le, rong, caoChu), format);
        }

        using var msRa = new MemoryStream();
        ghep.Save(msRa, ImageFormat.Png);
        return msRa.ToArray();
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
