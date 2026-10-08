using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HCP.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HCP.Web.Pages.Account;

/// <summary>
/// Người dùng tự đổi mật khẩu. Razor Page (không phải Blazor) vì sau khi đổi phải cấp lại cookie đăng nhập
/// (RefreshSignIn) - nếu không, chính phiên đang dùng cũng bị đăng xuất khi security stamp đổi.
/// </summary>
[Authorize]
public class DoiMatKhauModel : PageModel
{
    private readonly IDoiMatKhauService _doiMatKhau;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public DoiMatKhauModel(IDoiMatKhauService doiMatKhau, UserManager<ApplicationUser> userManager,
                           SignInManager<ApplicationUser> signInManager)
    {
        _doiMatKhau = doiMatKhau;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Thông báo thành công (hiện sau khi đổi xong).</summary>
    public string? ThongBao { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        public string MatKhauHienTai { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [DataType(DataType.Password)]
        public string MatKhauMoi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới")]
        [DataType(DataType.Password)]
        [Compare(nameof(MatKhauMoi), ErrorMessage = "Mật khẩu nhập lại không khớp")]
        public string NhapLai { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var kq = await _doiMatKhau.DoiMatKhauAsync(userId, Input.MatKhauHienTai, Input.MatKhauMoi, ct);
        if (!kq.ThanhCong)
        {
            ModelState.AddModelError(string.Empty, kq.ThongBao);
            return Page();
        }

        // Giữ đăng nhập ở trình duyệt này (cookie mới mang security stamp mới); các phiên khác hết hiệu lực.
        var user = await _userManager.FindByIdAsync(userId);
        if (user is not null) await _signInManager.RefreshSignInAsync(user);

        ThongBao = kq.ThongBao;
        Input = new InputModel();
        ModelState.Clear();
        return Page();
    }
}
