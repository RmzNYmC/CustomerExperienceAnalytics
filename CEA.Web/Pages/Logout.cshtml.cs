using CEA.Core.Entities; // Ekle
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CEA.Web.Pages
{
    [Authorize]
    public class LogoutModel : PageModel
    {
        // DÜZELT: IdentityUser yerine ApplicationUser
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;

        // DÜZELT: Constructor parametresi
        public LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            // GET isteğini Login'e yönlendir
            return RedirectToPage("/Login");
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("Kullanıcı çıkış yaptı.");

            return RedirectToPage("/Login");
        }
    }
}