using CEA.Core.Entities;
using CEA.Data;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Customers
{
    [Authorize(Policy = "CanCreateSurvey")]
    public class ImportModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ImportModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public IFormFile ExcelFile { get; set; } = null!;

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public List<ImportResult> Results { get; set; } = new();

        public class ImportResult
        {
            public int Row { get; set; }
            public string Email { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ExcelFile == null || ExcelFile.Length == 0)
            {
                ErrorMessage = "Lütfen bir Excel dosyası seçin.";
                return Page();
            }

            try
            {
                using var stream = new MemoryStream();
                await ExcelFile.CopyToAsync(stream);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);
                var rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;

                var existingEmails = await _context.Customers
                    .Where(c => !c.IsDeleted)
                    .Select(c => c.Email.ToLower())
                    .ToListAsync();

                for (int row = 2; row <= rowCount; row++)
                {
                    var email = worksheet.Cell(row, 1).GetString().Trim();
                    var name = worksheet.Cell(row, 2).GetString().Trim();
                    var company = worksheet.Cell(row, 3).GetString().Trim();
                    var phone = worksheet.Cell(row, 4).GetString().Trim();
                    var segment = worksheet.Cell(row, 5).GetString().Trim();

                    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(name))
                    {
                        Results.Add(new ImportResult { Row = row, Email = email, Name = name, Success = false, Message = "E-posta ve isim zorunlu." });
                        continue;
                    }

                    if (existingEmails.Contains(email.ToLower()))
                    {
                        Results.Add(new ImportResult { Row = row, Email = email, Name = name, Success = false, Message = "Zaten kayıtlı." });
                        continue;
                    }

                    var customer = new Customer
                    {
                        Email = email,
                        Name = name,
                        CompanyName = string.IsNullOrEmpty(company) ? null : company,
                        Phone = string.IsNullOrEmpty(phone) ? null : phone,
                        Segment = string.IsNullOrEmpty(segment) ? "Standard" : segment,
                        CreatedAt = DateTime.Now
                    };

                    _context.Customers.Add(customer);
                    existingEmails.Add(email.ToLower());
                    Results.Add(new ImportResult { Row = row, Email = email, Name = name, Success = true, Message = "Eklendi." });
                }

                await _context.SaveChangesAsync();
                SuccessMessage = $"İmport tamamlandı: {Results.Count(r => r.Success)} başarılı, {Results.Count(r => !r.Success)} hatalı.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Hata: {ex.Message}";
            }

            return Page();
        }

        public IActionResult OnGetDownloadTemplate()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Musteriler");

            worksheet.Cell(1, 1).Value = "E-posta *";
            worksheet.Cell(1, 2).Value = "İsim *";
            worksheet.Cell(1, 3).Value = "Şirket";
            worksheet.Cell(1, 4).Value = "Telefon";
            worksheet.Cell(1, 5).Value = "Segment";

            worksheet.Row(1).Style.Font.Bold = true;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "musteri_sablonu.xlsx");
        }
    }
}