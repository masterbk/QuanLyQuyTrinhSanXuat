using System.ComponentModel.DataAnnotations;
using HCP.Domain.Enums;
using HCP.Infrastructure.Identity;
using HCP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HCP.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICoSoService _coSoService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(SignInManager<ApplicationUser> signInManager,
                      UserManager<ApplicationUser> userManager,
                      ICoSoService coSoService,
                      ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _coSoService = coSoService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        public string MatKhau { get; set; } = string.Empty;

        public bool GhiNho { get; set; }
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToTrangChinh();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email.Trim());

        // Thông báo chung cho email sai hoặc mật khẩu sai, tránh lộ email nào tồn tại.
        const string LoiDangNhapChung = "Email hoặc mật khẩu không đúng.";

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, LoiDangNhapChung);
            return Page();
        }

        if (!user.DangHoatDong)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản đã bị vô hiệu hoá. Liên hệ quản trị viên cơ sở.");
            return Page();
        }

        // Người dùng thuộc một cơ sở thì cơ sở đó phải đã được duyệt.
        if (!string.IsNullOrWhiteSpace(user.TenantId))
        {
            var coSo = await _coSoService.LayTheoIdAsync(user.TenantId);

            if (coSo is null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy hồ sơ cơ sở của tài khoản này.");
                return Page();
            }

            var loiTrangThai = coSo.TrangThai switch
            {
                TenantStatus.PendingApproval =>
                    "Hồ sơ đăng ký của cơ sở đang chờ quản trị viên duyệt. Vui lòng quay lại sau.",
                TenantStatus.Rejected =>
                    $"Hồ sơ đăng ký đã bị từ chối. Lý do: {coSo.LyDoTuChoi ?? "(không ghi rõ)"}",
                TenantStatus.Suspended =>
                    "Cơ sở đang bị tạm khoá. Vui lòng liên hệ đơn vị vận hành nền tảng.",
                _ => null
            };

            if (loiTrangThai is not null)
            {
                ModelState.AddModelError(string.Empty, loiTrangThai);
                return Page();
            }
        }

        var ketQua = await _signInManager.PasswordSignInAsync(
            user, Input.MatKhau, Input.GhiNho, lockoutOnFailure: true);

        if (ketQua.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Tài khoản tạm khoá do đăng nhập sai nhiều lần. Vui lòng thử lại sau.");
            return Page();
        }

        if (!ketQua.Succeeded)
        {
            ModelState.AddModelError(string.Empty, LoiDangNhapChung);
            return Page();
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Người dùng {Email} đăng nhập thành công.", user.Email);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);

        return RedirectToTrangChinh();
    }

    private IActionResult RedirectToTrangChinh() => LocalRedirect("/");
}
