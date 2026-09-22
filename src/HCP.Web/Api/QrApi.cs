using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Net;
using HCP.Infrastructure.Services.TraCuu;
using QRCoder;

namespace HCP.Web.Api;

/// <summary>
/// Ảnh mã QR (PNG) của đơn hàng bán và lô sản xuất - dùng cookie đăng nhập web của cơ sở.
///   GET /app/qr/don-hang/{id}[?tai=true]   (tai=true: tải file về)
///   GET /app/qr/don-hang/{id}[?in=true]    (in=true: trang HTML tự bật hộp thoại in của trình duyệt)
///   GET /app/qr/lo/{id}[?tai=true|in=true]
/// </summary>
public static class QrApi
{
    public static void MapQrApi(this IEndpointRouteBuilder app)
    {
        var qr = app.MapGroup("/app/qr").RequireAuthorization("NguoiDungCoSo");

        qr.MapGet("/don-hang/{id:int}", async (int id, bool? tai, bool? @in, ITraCuuCongKhaiService svc, IConfiguration cauHinh,
                                               HttpRequest req, CancellationToken ct) =>
            TraAnh(await svc.LayQrDonHangAsync(id, ct), LoaiTraCuu.DonHang, tai, @in, cauHinh, req));

        qr.MapGet("/lo/{id:int}", async (int id, bool? tai, bool? @in, ITraCuuCongKhaiService svc, IConfiguration cauHinh,
                                         HttpRequest req, CancellationToken ct) =>
            TraAnh(await svc.LayQrLoAsync(id, ct), LoaiTraCuu.Lo, tai, @in, cauHinh, req));

        qr.MapGet("/lenh-san-xuat/{id:int}", async (int id, bool? tai, bool? @in, ITraCuuCongKhaiService svc, IConfiguration cauHinh,
                                                    HttpRequest req, CancellationToken ct) =>
            TraAnh(await svc.LayQrLenhSanXuatAsync(id, ct), LoaiTraCuu.LenhSanXuat, tai, @in, cauHinh, req));
    }

    private static IResult TraAnh(QrTraCuu? qr, LoaiTraCuu loai, bool? tai, bool? @in, IConfiguration cauHinh, HttpRequest req)
    {
        if (qr is null) return Results.NotFound();

        var noiDung = DuongDanTraCuu.NoiDungQr(qr, loai, DuongDanTraCuu.Goc(cauHinh, $"{req.Scheme}://{req.Host}{req.PathBase}"));
        var png = TaoPng(noiDung, CacDongChu(qr, loai));
        if (@in == true) return Results.Content(TrangIn(qr, png), "text/html");
        if (tai != true) return Results.File(png, "image/png");

        var tenFile = string.Concat(qr.Ma.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Results.File(png, "image/png", $"QR-{tenFile}.png");
    }

    /// <summary>Trang in: ảnh QR nhúng thẳng base64 (khỏi gọi lại API, khỏi lo cookie/CORS trong tab mới),
    /// tự gọi window.print() khi ảnh vẽ xong. Người dùng huỷ/đóng hộp thoại in thì tự đóng lại tab đó.</summary>
    private static string TrangIn(QrTraCuu qr, byte[] png)
    {
        var base64 = Convert.ToBase64String(png);
        var tieuDe = WebUtility.HtmlEncode($"In mã QR {qr.Ma}");
        return $$"""
            <!doctype html>
            <html lang="vi"><head><meta charset="utf-8" /><title>{{tieuDe}}</title>
            <style>
                body { margin: 0; display: flex; justify-content: center; align-items: center; min-height: 100vh; background: #fff; }
                img { max-width: 92vw; max-height: 92vh; }
            </style></head>
            <body>
                <img src="data:image/png;base64,{{base64}}" alt="{{tieuDe}}"
                     onload="window.print()" onafterprint="window.close()" />
            </body></html>
            """;
    }

    /// <summary>Dòng chữ in dưới ảnh QR - đơn hàng từ HanoiCheck thì qr.Ma đã là mã trên HanoiCheck
    /// (xem TraCuuCongKhaiService.LayQrDonHangAsync), đơn nội bộ thì là mã nội bộ. Dùng lại ở API mobile.</summary>
    internal static string ChuThich(QrTraCuu qr, LoaiTraCuu loai) => loai switch
    {
        LoaiTraCuu.DonHang => $"Đơn hàng {qr.Ma}",
        LoaiTraCuu.Lo => $"Lô {qr.Ma}",
        _ => $"Lệnh sản xuất {qr.Ma}"
    };

    /// <summary>Dòng mã (in đậm) rồi tới các dòng thêm của <see cref="QrTraCuu.DongChuThem"/> nếu có
    /// (đơn hàng: tên sản phẩm/số lượng từng dòng + địa chỉ giao).</summary>
    internal static IReadOnlyList<string> CacDongChu(QrTraCuu qr, LoaiTraCuu loai)
    {
        var dong = new List<string> { ChuThich(qr, loai) };
        if (qr.DongChuThem is { Count: > 0 }) dong.AddRange(qr.DongChuThem);
        return dong;
    }

    /// <summary>PNG đen trắng, mức sửa lỗi M (chịu được tem hơi bẩn/nhàu), 20 px mỗi ô - đủ nét để in tem.
    /// Có <paramref name="cacDongChu"/> thì in thêm các dòng đó dưới mã QR, trong viền trắng - dòng đầu in đậm
    /// cỡ lớn (mã đơn/lô/lệnh), các dòng sau cỡ nhỏ hơn và tự ngắt dòng nếu dài hơn bề rộng mã QR.
    /// Dùng System.Drawing.Common (MIT, có sẵn với .NET) - chỉ chạy trên Windows, khớp môi trường IIS
    /// đang triển khai (project đã khai TargetFramework net8.0-windows); KHÔNG dùng
    /// SixLabors.ImageSharp.Drawing vì gói đó đòi mua bản quyền thương mại (Six Labors Split License)
    /// tuỳ doanh thu công ty.</summary>
    public static byte[] TaoPng(string noiDung, IReadOnlyList<string>? cacDongChu = null)
    {
        using var boTao = new QRCodeGenerator();
        using var duLieu = boTao.CreateQrCode(noiDung, QRCodeGenerator.ECCLevel.M);
        var qrPng = new PngByteQRCode(duLieu).GetGraphic(20);
        var dongGoc = (cacDongChu ?? Array.Empty<string>()).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        if (dongGoc.Count == 0) return qrPng;

        using var msQr = new MemoryStream(qrPng);
        using var qrAnh = Image.FromStream(msQr);
        const int le = 24;           // viền trắng quanh mã QR
        const int caoDongDau = 54;   // chiều cao dòng đầu (mã, in đậm)
        const int caoDongSau = 48;   // chiều cao mỗi dòng còn lại (có thể ngắt dòng)
        var rong = qrAnh.Width + le * 2;

        // Cỡ chữ đã tăng thêm ~1.5 lần (33/30, trước là 22/20) theo yêu cầu - tem dán xa vẫn đọc được.
        using var fontDam = new Font("Arial", 33, FontStyle.Bold, GraphicsUnit.Pixel);
        using var fontThuong = new Font("Arial", 30, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(0x1a, 0x2b, 0x4c));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        using var doDoTam = new Bitmap(1, 1);
        using var gTam = Graphics.FromImage(doDoTam);
        var cacDongVe = new List<(string Chu, Font Font, int Cao)> { (dongGoc[0], fontDam, caoDongDau) };
        foreach (var dong in dongGoc.Skip(1))
            foreach (var phan in NgatDong(gTam, dong, fontThuong, qrAnh.Width - 8))
                cacDongVe.Add((phan, fontThuong, caoDongSau));

        var cao = qrAnh.Height + le * 2 + cacDongVe.Sum(d => d.Cao);

        using var ghep = new Bitmap(rong, cao);
        using (var g = Graphics.FromImage(ghep))
        {
            g.Clear(Color.White);
            g.DrawImage(qrAnh, le, le, qrAnh.Width, qrAnh.Height);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var y = qrAnh.Height + le;
            foreach (var (chu, font, caoDong) in cacDongVe)
            {
                g.DrawString(chu, font, brush, new RectangleF(0, y, rong, caoDong), format);
                y += caoDong;
            }
        }

        using var msRa = new MemoryStream();
        ghep.Save(msRa, ImageFormat.Png);
        return msRa.ToArray();
    }

    /// <summary>Ngắt <paramref name="text"/> thành nhiều dòng theo từ để mỗi dòng vừa <paramref name="rongToiDa"/>
    /// khi vẽ bằng <paramref name="font"/> (đo bằng <paramref name="g"/>).</summary>
    private static List<string> NgatDong(Graphics g, string text, Font font, float rongToiDa)
    {
        var ketQua = new List<string>();
        var dong = "";
        foreach (var tu in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var thu = dong.Length == 0 ? tu : $"{dong} {tu}";
            if (dong.Length == 0 || g.MeasureString(thu, font).Width <= rongToiDa)
                dong = thu;
            else
            {
                ketQua.Add(dong);
                dong = tu;
            }
        }
        if (dong.Length > 0) ketQua.Add(dong);
        return ketQua.Count == 0 ? new List<string> { text } : ketQua;
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
