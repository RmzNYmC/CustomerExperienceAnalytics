using CEA.Core.Entities;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Customers
{
    [Authorize(Policy = "CanCreateSurvey")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly int _pageSize = 25;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Customer> Customers { get; set; } = new List<Customer>();

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string SegmentFilter { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public async Task OnGetAsync()
        {
            var query = _context.Customers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(term) ||
                    c.Email.ToLower().Contains(term) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(SegmentFilter))
            {
                query = query.Where(c => c.Segment == SegmentFilter);
            }

            TotalRecords = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)_pageSize);

            Customers = await query
                .OrderBy(c => c.Name)
                .Skip((PageIndex - 1) * _pageSize)
                .Take(_pageSize)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return NotFound();

            customer.IsDeleted = true;
            customer.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Müşteri başarıyla silindi.";
            return RedirectToPage();
        }
    }
}