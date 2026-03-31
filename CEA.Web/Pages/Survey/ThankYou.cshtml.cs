using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CEA.Web.Pages.Survey
{
    public class ThankYouModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        public void OnGet()
        {
            // Token varsa survey bilgisi çekilebilir (opsiyonel)
        }
    }
}