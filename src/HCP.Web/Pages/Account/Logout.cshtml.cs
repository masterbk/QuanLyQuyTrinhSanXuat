using HCP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HCP.Web.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public LogoutModel(SignInManager<ApplicationUser> signInManager) => _signInManager = signInManager;

    public async Task<IActionResult> OnGetAsync()
    {
        await _signInManager.SignOutAsync();
        return LocalRedirect("/dang-nhap");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _signInManager.SignOutAsync();
        return LocalRedirect("/dang-nhap");
    }
}
