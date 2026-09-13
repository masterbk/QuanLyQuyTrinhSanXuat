using HCP.Infrastructure.Services.TraCuu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HCP.Web.Pages.TraCuu;

/// <summary>Trang công khai (quét QR) xem thông tin đơn hàng bán - không cần đăng nhập.</summary>
[AllowAnonymous]
public class DonHangModel : PageModel
{
    private readonly ITraCuuCongKhaiService _traCuu;

    public DonHangModel(ITraCuuCongKhaiService traCuu) => _traCuu = traCuu;

    public TraCuuDonHang? Don { get; private set; }

    public async Task OnGetAsync(string ma, CancellationToken ct)
    {
        Don = await _traCuu.TraCuuDonHangAsync(ma, ct);
        if (Don is null) Response.StatusCode = StatusCodes.Status404NotFound;
    }
}
