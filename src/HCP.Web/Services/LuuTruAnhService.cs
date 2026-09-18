using HCP.Domain;
using Finbuckle.MultiTenant.Abstractions;
using HCP.Infrastructure.Services.Kho;
using Microsoft.AspNetCore.Components.Forms;

namespace HCP.Web.Services;

/// <summary>
/// Lưu ảnh chứng từ do cơ sở tải lên xuống đĩa của máy chủ (dưới wwwroot/uploads) và trả về
/// đường dẫn tương đối để lưu vào cơ sở dữ liệu.
///
/// Ảnh lô sản xuất còn được gửi kèm sang HanoiCheck qua trường duong_dan, nên phải là đường dẫn
/// HanoiCheck tải được - vì vậy để ở thư mục tĩnh công khai, tên file là GUID ngẫu nhiên (không
/// đoán được) thay vì chặn bằng đăng nhập. Khai khoá cấu hình "Uploads:BaseUrl" (vd
/// https://app.congty.vn) để đường dẫn lưu xuống là URL tuyệt đối; thiếu khoá này thì chỉ ra
/// đường dẫn tương đối - app vẫn hiển thị được nhưng HanoiCheck KHÔNG tải được ảnh.
/// </summary>
public interface ILuuTruAnhService
{
    /// <summary>Ảnh chọn từ giao diện web (Blazor).</summary>
    Task<AnhLoSanXuat> LuuAsync(IBrowserFile file, CancellationToken ct = default);

    /// <summary>Ảnh do ứng dụng di động tải lên qua API (multipart).</summary>
    Task<AnhLoSanXuat> LuuAsync(Stream noiDung, string tenFile, string? kieuNoiDung, long kichThuoc,
                                CancellationToken ct = default);
}

/// <inheritdoc cref="ILuuTruAnhService"/>
public sealed class LuuTruAnhService : ILuuTruAnhService
{
    /// <summary>Giới hạn 5 MB mỗi ảnh - đủ cho ảnh chụp điện thoại đã nén.</summary>
    public const long GioiHanByte = 5 * 1024 * 1024;

    private static readonly HashSet<string> DuoiChoPhep =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private readonly IWebHostEnvironment _moiTruong;
    private readonly IMultiTenantContextAccessor _tenantAccessor;
    private readonly string? _baseUrl;

    public LuuTruAnhService(IWebHostEnvironment moiTruong, IMultiTenantContextAccessor tenantAccessor,
                            IConfiguration cauHinh)
    {
        _moiTruong = moiTruong;
        _tenantAccessor = tenantAccessor;
        _baseUrl = cauHinh["Uploads:BaseUrl"]?.TrimEnd('/');
    }

    public async Task<AnhLoSanXuat> LuuAsync(IBrowserFile file, CancellationToken ct = default)
    {
        await using var luong = file.OpenReadStream(GioiHanByte, ct);
        return await LuuAsync(luong, file.Name, file.ContentType, file.Size, ct);
    }

    public async Task<AnhLoSanXuat> LuuAsync(Stream noiDung, string tenFile, string? kieuNoiDung,
                                             long kichThuoc, CancellationToken ct = default)
    {
        var file = new { Name = tenFile, ContentType = kieuNoiDung ?? "", Size = kichThuoc };

        var tenantId = _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new InvalidOperationException("Không xác định được cơ sở khi tải ảnh lên.");

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"\"{file.Name}\" không phải là ảnh.");

        var duoi = Path.GetExtension(file.Name);
        if (!DuoiChoPhep.Contains(duoi))
            throw new InvalidOperationException(
                $"\"{file.Name}\" có định dạng không hỗ trợ (chỉ nhận {string.Join(", ", DuoiChoPhep)}).");

        if (file.Size > GioiHanByte)
            throw new InvalidOperationException(
                $"\"{file.Name}\" nặng {file.Size / 1024d / 1024d:0.#} MB, vượt giới hạn {GioiHanByte / 1024 / 1024} MB.");

        // wwwroot có thể chưa tồn tại khi chạy từ thư mục lạ - tự tạo cho chắc.
        var goc = _moiTruong.WebRootPath;
        if (string.IsNullOrWhiteSpace(goc)) goc = Path.Combine(_moiTruong.ContentRootPath, "wwwroot");

        var homNay = GioVietNam.Nay;
        var thuMucTuongDoi = Path.Combine("uploads", tenantId, homNay.ToString("yyyy"), homNay.ToString("MM"));
        var thuMuc = Path.Combine(goc, thuMucTuongDoi);
        Directory.CreateDirectory(thuMuc);

        var tenLuu = Guid.NewGuid().ToString("N") + duoi.ToLowerInvariant();
        var duongDanDia = Path.Combine(thuMuc, tenLuu);

        await using (var dich = File.Create(duongDanDia))
        {
            await noiDung.CopyToAsync(dich, ct);
        }

        var duongDan = "/" + Path.Combine(thuMucTuongDoi, tenLuu).Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(_baseUrl)) duongDan = _baseUrl + duongDan;
        return new AnhLoSanXuat(file.Name, duongDan);
    }
}
