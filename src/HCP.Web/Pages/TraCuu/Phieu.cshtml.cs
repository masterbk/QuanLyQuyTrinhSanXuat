using HCP.Infrastructure.Services.TraCuu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HCP.Web.Pages.TraCuu;

/// <summary>
/// Trang công khai (quét QR trên PDF phiếu biểu mẫu) để bên kiểm tra đối chiếu bản gốc: nội dung phiếu, người ký,
/// giờ ký, chữ ký và kết quả so mã băm (nội dung có bị sửa sau khi ký không). Không cần đăng nhập.
/// </summary>
[AllowAnonymous]
public class PhieuModel : PageModel
{
    private readonly ITraCuuCongKhaiService _traCuu;

    public PhieuModel(ITraCuuCongKhaiService traCuu) => _traCuu = traCuu;

    public TraCuuPhieu? Phieu { get; private set; }

    public async Task OnGetAsync(string ma, CancellationToken ct)
    {
        Phieu = await _traCuu.TraCuuPhieuAsync(ma, ct);
        if (Phieu is null) Response.StatusCode = StatusCodes.Status404NotFound;
    }
}
