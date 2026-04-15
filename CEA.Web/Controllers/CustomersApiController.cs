using CEA.Core.Entities;
using CEA.Data;
using CEA.Web.Dtos.Common;
using CEA.Web.Dtos.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Controllers
{
    [Route("api/customers")]
    [ApiController]
    [Authorize(Policy = "CanManageCustomers")]
    public class CustomersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CustomersApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers(
            [FromQuery] string? searchTerm,
            [FromQuery] string? segment,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : Math.Min(pageSize, 100);

            var query = _context.Customers
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(term) ||
                    c.Email.ToLower().Contains(term) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(segment))
            {
                query = query.Where(c => c.Segment == segment);
            }

            var totalRecords = await query.CountAsync();

            var customers = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CustomerListItemDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Email = c.Email,
                    CompanyName = c.CompanyName,
                    Phone = c.Phone,
                    Segment = c.Segment,
                    EmailVerified = c.EmailVerified,
                    BounceEmail = c.BounceEmail,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                items = customers
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCustomer(int id)
        {
            var customer = await _context.Customers
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new CustomerDetailDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Email = c.Email,
                    CompanyName = c.CompanyName,
                    Phone = c.Phone,
                    Segment = c.Segment,
                    Notes = c.Notes,
                    EmailVerified = c.EmailVerified,
                    BounceEmail = c.BounceEmail,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    ResponseCount = c.Responses.Count(r => !r.IsDeleted)
                })
                .FirstOrDefaultAsync();

            if (customer == null)
                return NotFound(new { message = "Müşteri bulunamadı." });

            return Ok(customer);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCustomer([FromBody] CustomerCreateDto request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "E-posta adresi zorunludur." });

            var normalizedEmail = request.Email.Trim().ToLower();

            var exists = await _context.Customers
                .AnyAsync(c => c.Email.ToLower() == normalizedEmail);

            if (exists)
                return Conflict(new { message = "Bu e-posta adresi zaten kayıtlı." });

            var customer = new Customer
            {
                Name = request.Name.Trim(),
                Email = request.Email.Trim(),
                CompanyName = request.CompanyName?.Trim(),
                Phone = request.Phone?.Trim(),
                Segment = request.Segment?.Trim(),
                Notes = request.Notes,
                EmailVerified = request.EmailVerified,
                BounceEmail = request.BounceEmail,
                CreatedAt = DateTime.Now,
                CreatedBy = User.Identity?.Name ?? "api"
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            var resultDto = new CustomerDto
            {
                Id = customer.Id,
                Name = customer.Name,
                Email = customer.Email,
                CompanyName = customer.CompanyName,
                Phone = customer.Phone,
                Segment = customer.Segment,
                Notes = customer.Notes,
                EmailVerified = customer.EmailVerified,
                BounceEmail = customer.BounceEmail,
                ResponseCount = 0,
                CreatedAt = customer.CreatedAt,
                UpdatedAt = customer.UpdatedAt
            };

            return CreatedAtAction(
                nameof(GetCustomer),
                new { id = customer.Id },
                ApiResponse<CustomerDto>.Ok(resultDto, "Müşteri oluşturuldu."));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] CustomerUpdateDto request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "E-posta adresi zorunludur." });

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return NotFound(new { message = "Müşteri bulunamadı." });

            var normalizedEmail = request.Email.Trim().ToLower();

            var emailInUse = await _context.Customers
                .AnyAsync(c => c.Id != id && c.Email.ToLower() == normalizedEmail);

            if (emailInUse)
                return Conflict(new { message = "Bu e-posta adresi başka bir müşteriye ait." });

            customer.Name = request.Name.Trim();
            customer.Email = request.Email.Trim();
            customer.CompanyName = request.CompanyName?.Trim();
            customer.Phone = request.Phone?.Trim();
            customer.Segment = request.Segment?.Trim();
            customer.Notes = request.Notes;
            customer.EmailVerified = request.EmailVerified;
            customer.BounceEmail = request.BounceEmail;
            customer.UpdatedAt = DateTime.Now;
            customer.UpdatedBy = User.Identity?.Name ?? "api";

            await _context.SaveChangesAsync();

            return Ok(new { message = "Müşteri başarıyla güncellendi." });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return NotFound(new { message = "Müşteri bulunamadı." });

            customer.IsDeleted = true;
            customer.DeletedAt = DateTime.Now;
            customer.UpdatedAt = DateTime.Now;
            customer.UpdatedBy = User.Identity?.Name ?? "api";

            await _context.SaveChangesAsync();

            return Ok(new { message = "Müşteri silindi." });
        }
    }
}