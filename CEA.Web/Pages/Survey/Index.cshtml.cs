using CEA.Business.Services;
using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation; // ✅ EKLENDİ
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Survey
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IComplaintAutomationService _complaintService;
        private readonly ILogger<IndexModel> _logger;
        private readonly ICacheService _cacheService;

        public IndexModel(ApplicationDbContext context, IComplaintAutomationService complaintService, ICacheService cacheService, ILogger<IndexModel> logger)
        {
            _context = context;
            _complaintService = complaintService;
            _cacheService = cacheService;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = string.Empty;

        // ✅ DÜZELTME: ValidateNever eklendi - POST'ta validate edilmesin
        [ValidateNever]
        public CEA.Core.Entities.Survey? Survey { get; set; }

        [ValidateNever]
        public List<CEA.Core.Entities.Question> Questions { get; set; } = new();

        // ✅ DÜZELTME: Sadece SurveyResponse kullan, Response yerine
        [BindProperty]
        public SurveyResponse SurveyResponse { get; set; } = new();

        // ❌ KALDIR: public new SurveyResponse Response { get; set; }

        [BindProperty]
        public List<AnswerViewModel> Answers { get; set; } = new();

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public bool ShowForm { get; set; } = true;

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Token))
                return NotFound();

            Survey = await _context.Surveys
                .FirstOrDefaultAsync(s => s.PublicToken == Token && !s.IsDeleted
                    && s.Status == SurveyStatus.Active);

            if (Survey == null)
            {
                ErrorMessage = "Anket bulunamadı veya süresi dolmuş.";
                ShowForm = false;
                return Page();
            }

            Questions = await _context.Questions
                .Where(q => q.SurveyId == Survey.Id && !q.IsDeleted)
                .Include(q => q.Options)
                .OrderBy(q => q.DisplayOrder)
                .ToListAsync();

            ShowForm = true;
            return Page();

        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("📝 Answers count: {Count}", Answers?.Count ?? 0);
            _logger.LogInformation("🟢 POST BAŞLADI - Token: {Token}", Token);
            if (Answers == null || !Answers.Any())
            {
                _logger.LogWarning("⚠️ Answers listesi boş! Model binding çalışmadı.");

                // Hata mesajı göster ve sayfayı tekrar yükle
                ErrorMessage = "Cevaplar alınamadı. Lütfen tekrar deneyin.";
                await ReloadQuestions();
                return Page();
            }
            if (Answers != null)
            {
                foreach (var ans in Answers)
                {
                    _logger.LogInformation("Answer: QuestionId={QId}, Value={Val}, Rating={Rating}",
                        ans.QuestionId, ans.Value, ans.RatingValue);
                }
            }

            try
            {
                // Tüm navigation property'leri validasyondan çıkar
                ModelState.Remove("SurveyResponse.Survey");
                ModelState.Remove("SurveyResponse.Customer");
                ModelState.Remove("SurveyResponse.User");

                foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Answers[")).ToList())
                {
                    ModelState.Remove(key);
                }

                // ModelState kontrolü
                if (!ModelState.IsValid)
                {
                    var errors = string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    _logger.LogWarning("🔴 ModelState HATALI: {Errors}", errors);
                    ErrorMessage = "Lütfen zorunlu alanları doldurun: " + errors;

                    await ReloadQuestions();
                    return Page();
                }

                _logger.LogInformation("✅ ModelState geçerli");

                // Survey bul
                Survey = await _context.Surveys
                    .FirstOrDefaultAsync(s => s.PublicToken == Token && !s.IsDeleted);

                if (Survey == null)
                {
                    _logger.LogWarning("🔴 Survey bulunamadı: {Token}", Token);
                    return NotFound();
                }

                _logger.LogInformation("✅ Survey bulundu: {SurveyId}", Survey.Id);

                // Email kontrolü
                if (!string.IsNullOrEmpty(SurveyResponse.CustomerEmail))
                {
                    var existingResponse = await _context.SurveyResponses
                        .FirstOrDefaultAsync(r => r.SurveyId == Survey.Id
                            && r.CustomerEmail == SurveyResponse.CustomerEmail
                            && !r.IsDeleted);

                    if (existingResponse != null)
                    {
                        _logger.LogInformation("⚠️ Aynı email ile zaten yanıtlanmış: {Email}", SurveyResponse.CustomerEmail);
                        SuccessMessage = "Bu e-posta adresi ile anketi zaten tamamladınız.";
                        ShowForm = false;
                        return Page();
                    }
                }

                // VERİYİ HAZIRLA
                SurveyResponse.SurveyId = Survey.Id;
                SurveyResponse.SubmittedAt = DateTime.Now;
                SurveyResponse.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                SurveyResponse.UserAgent = Request.Headers["User-Agent"].ToString();
                SurveyResponse.ResponseYear = DateTime.Now.Year;
                SurveyResponse.ResponseMonth = DateTime.Now.Month;

                // CustomerId bul
                if (!string.IsNullOrEmpty(SurveyResponse.CustomerEmail))
                {
                    var existingCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.Email == SurveyResponse.CustomerEmail && !c.IsDeleted);

                    if (existingCustomer != null)
                    {
                        SurveyResponse.CustomerId = existingCustomer.Id;
                        _logger.LogInformation("✅ Mevcut müşteri bulundu: {CustomerId}", existingCustomer.Id);
                    }
                    else
                    {
                        _logger.LogInformation("⚠️ Yeni müşteri - CustomerId null kalacak");
                    }
                }

                _logger.LogInformation("📝 Yanıt ekleniyor: SurveyId={SurveyId}, Email={Email}",
                    SurveyResponse.SurveyId, SurveyResponse.CustomerEmail);

                // VERİTABANINA EKLE
                _context.SurveyResponses.Add(SurveyResponse);


                _logger.LogInformation("💾 SaveChangesAsync çağrılıyor (Response)...");
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Response kaydedildi. ID: {ResponseId}", SurveyResponse.Id);

                // Cevapları kaydet
                if (Answers != null && Answers.Any())
                {
                    _logger.LogInformation("📝 {Count} cevap ekleniyor...", Answers.Count);

                    foreach (var answer in Answers.Where(a => !string.IsNullOrWhiteSpace(a.Value) || a.RatingValue.HasValue))
                    {
                        //if (string.IsNullOrWhiteSpace(answer.Value) && !answer.RatingValue.HasValue)
                        //{
                        //    _logger.LogInformation("⏭️ Boş cevap atlandı: QuestionId={QuestionId}", answer.QuestionId);
                        //    continue;
                        //}

                        var newAnswer = new CEA.Core.Entities.Answer
                        {
                            ResponseId = SurveyResponse.Id,
                            QuestionId = answer.QuestionId,
                            NumericAnswer = answer.RatingValue,
                            TextAnswer = answer.Value,
                            CreatedAt = DateTime.Now
                        };

                        _context.Answers.Add(newAnswer);
                        _logger.LogInformation("➕ Cevap eklendi: QuestionId={QuestionId}, Value={Value}",
                            answer.QuestionId, answer.Value ?? answer.RatingValue?.ToString() ?? "null");
                    }

                    _logger.LogInformation("💾 SaveChangesAsync çağrılıyor (Answers)...");
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ Cevaplar kaydedildi");
                }
                else
                {
                    _logger.LogWarning("⚠️ Answers listesi boş veya null!");
                }

                await _cacheService.RemoveAsync("analytics:dashboard:summary");
                await _cacheService.RemoveByPrefixAsync("analytics:monthly");
                await _cacheService.RemoveByPrefixAsync("analytics:yearly");

                _logger.LogInformation("Cache temizlendi - Dashboard güncellenecek");


                // Şikayet kontrolü
                _logger.LogInformation("🔍 Şikayet kontrolü başlıyor...");
                await _complaintService.CheckAndCreateComplaintAsync(SurveyResponse.Id);
                _logger.LogInformation("✅ Şikayet kontrolü tamamlandı");

                _logger.LogInformation("🟢 POST BAŞARILI - Redirect yapılıyor");
                return RedirectToPage("/Survey/ThankYou", new { token = Token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔴🔴🔴 KRİTİK HATA: {Message}", ex.Message);
                _logger.LogError(ex, "StackTrace: {StackTrace}", ex.StackTrace);

                ErrorMessage = "Bir hata oluştu: " + ex.Message;
                await ReloadQuestions();
                return Page();
            }
        }

        private async Task ReloadQuestions()
        {
            if (Survey != null)
            {
                Questions = await _context.Questions
                    .Where(q => q.SurveyId == Survey.Id && !q.IsDeleted)
                    .Include(q => q.Options)
                    .OrderBy(q => q.DisplayOrder)
                    .ToListAsync();
            }
        }
    }

    public class AnswerViewModel
    {
        public int QuestionId { get; set; }
        public List<string> SelectedValues { get; set; } = new(); // Multiple için
        public string? Value { get; set; }
        public int? RatingValue { get; set; }
    }
}