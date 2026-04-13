using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CEA.Web.Pages;

[Authorize] // EKLENDİ: Giriş yapmadan görüntülenemez
public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        // Eğer giriş yapılmışsa Dashboard'a yönlendir
        if (User.Identity.IsAuthenticated)
        {
            return RedirectToPage("/Admin/Dashboard");
        }

        // Giriş yapılmamışsa Login sayfasına yönlendir
        return RedirectToPage("/Account/Login", new { area = "Identity" });
    }

    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

   
}
