using CEA.Core.Entities;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Customers
{
    [Authorize(Policy = "CanCreateSurvey")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Customer Customer { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            Customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (Customer == null) return NotFound();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (await _context.Customers.AnyAsync(c =>
                c.Email == Customer.Email &&
                c.Id != Customer.Id &&
                !c.IsDeleted))
            {
                ModelState.AddModelError("Customer.Email", "Bu e-posta adresi başka müşteriye ait.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var customer = await _context.Customers.FindAsync(Customer.Id);
            if (customer == null) return NotFound();

            customer.Name = Customer.Name;
            customer.Email = Customer.Email;
            customer.CompanyName = Customer.CompanyName;
            customer.Phone = Customer.Phone;
            customer.Segment = Customer.Segment;
            customer.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Müşteri bilgileri güncellendi.";
            return RedirectToPage("./Index");
        }
    }
}