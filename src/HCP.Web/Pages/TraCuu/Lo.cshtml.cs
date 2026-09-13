using HCP.Infrastructure.Services.TraCuu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HCP.Web.Pages.TraCuu;

/// <summary>Trang công khai (quét QR) xem thông tin lô sản xuất - không cần đăng nhập.</summary>
[AllowAnonymous]
public class LoModel : PageModel
{
    private readonly ITraCuuCongKhaiService _traCuu;

    public LoModel(ITraCuuCongKhaiService traCuu) => _traCuu = traCuu;

    public TraCuuLo? Lo { get; private set; }

    public async Task OnGetAsync(string ma, CancellationToken ct)
    {
        Lo = await _traCuu.TraCuuLoAsync(ma, ct);
        if (Lo is null) Response.StatusCode = StatusCodes.Status404NotFound;
    }
}
