using HCP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HCP.Web.Pages.Account;

[AllowAnonymous]
public class DangKyModel : PageModel
{
    private readonly ICoSoService _coSoService;

    public DangKyModel(ICoSoService coSoService) => _coSoService = coSoService;

    [BindProperty]
    public DangKyCoSoRequest Input { get; set; } = new();

    public bool ThanhCong { get; private set; }
    public string? ThongBao { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var ketQua = await _coSoService.DangKyAsync(Input);

        if (!ketQua.ThanhCong)
        {
            ModelState.AddModelError(string.Empty, ketQua.ThongBao);
            return Page();
        }

        ThanhCong = true;
        ThongBao = ketQua.ThongBao;
        return Page();
    }
}
