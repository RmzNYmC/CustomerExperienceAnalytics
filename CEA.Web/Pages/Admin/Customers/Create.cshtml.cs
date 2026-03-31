using CEA.Core.Entities;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Customers
{
    [Authorize(Policy = "CanCreateSurvey")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // BU PROPERTY OLMALI - Hata burada yoksa düzelir
        [BindProperty]
        public Customer Customer { get; set; } = new();

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (await _context.Customers.AnyAsync(c => c.Email == Customer.Email && !c.IsDeleted))
            {
                ModelState.AddModelError("Customer.Email", "Bu e-posta adresi zaten kayıtlı.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            Customer.CreatedAt = DateTime.Now;
            _context.Customers.Add(Customer);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Müşteri başarıyla eklendi.";
            return RedirectToPage("./Index");
        }
    }
}