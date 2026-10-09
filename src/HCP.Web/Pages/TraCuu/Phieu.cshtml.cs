using HCP.Infrastructure.Services.TraCuu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HCP.Web.Pages.TraCuu;

/// <summary>
/// Trang công khai (quét QR trên PDF phiếu biểu mẫu) để bên kiểm tra đối chiếu bản gốc: nội dung phiếu, người ký,
/// giờ ký, chữ ký, kết quả so mã băm + chữ ký số máy chủ (dữ liệu có bị sửa sau khi ký không) và mã băm trong QR
/// (bản PDF đang quét có phải bản hiện hành). Không cần đăng nhập.
/// </summary>
[AllowAnonymous]
public class PhieuModel : PageModel
{
    private readonly ITraCuuCongKhaiService _traCuu;

    public PhieuModel(ITraCuuCongKhaiService traCuu) => _traCuu = traCuu;

    public TraCuuPhieu? Phieu { get; private set; }

    /// <param name="h">Mã băm lúc ký mang trong QR của bản PDF đang quét (PDF in trước đây không có).</param>
    public async Task OnGetAsync(string ma, string? h, CancellationToken ct)
    {
        Phieu = await _traCuu.TraCuuPhieuAsync(ma, h is { Length: <= 64 } ? h : null, ct);
        if (Phieu is null) Response.StatusCode = StatusCodes.Status404NotFound;
    }
}
