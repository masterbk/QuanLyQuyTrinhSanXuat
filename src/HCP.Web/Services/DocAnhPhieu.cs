using HCP.Infrastructure.Services.BieuMau;

namespace HCP.Web.Services;

/// <summary>Đọc ảnh đã lưu dưới wwwroot/uploads để nhúng vào PDF phiếu. Chặn truy cập ngoài thư mục uploads.</summary>
public sealed class DocAnhPhieu : IDocAnhPhieu
{
    private readonly IWebHostEnvironment _env;
    public DocAnhPhieu(IWebHostEnvironment env) => _env = env;

    public byte[]? Doc(string? duongDan)
    {
        if (string.IsNullOrWhiteSpace(duongDan)) return null;

        // Đường dẫn lưu có thể tuyệt đối (https://host/uploads/...) hoặc tương đối (/uploads/...).
        var i = duongDan.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var tuongDoi = duongDan[(i + 1)..]; // "uploads/.../x.jpg"

        var goc = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(goc)) goc = Path.Combine(_env.ContentRootPath, "wwwroot");

        var duongDanDia = Path.GetFullPath(Path.Combine(goc, tuongDoi.Replace('/', Path.DirectorySeparatorChar)));
        var thuMucUploads = Path.GetFullPath(Path.Combine(goc, "uploads")) + Path.DirectorySeparatorChar;
        if (!duongDanDia.StartsWith(thuMucUploads, StringComparison.OrdinalIgnoreCase)) return null; // chặn path traversal

        try { return File.Exists(duongDanDia) ? File.ReadAllBytes(duongDanDia) : null; }
        catch (IOException) { return null; }
    }
}
