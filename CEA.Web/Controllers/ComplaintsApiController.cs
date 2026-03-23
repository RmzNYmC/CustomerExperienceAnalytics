using CEA.Business.Services;
using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Core.Enums;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Controllers
{
    [Route("api/complaints")]
    [ApiController]
    [Authorize]
    public class ComplaintsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public ComplaintsApiController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet("open-count")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOpenCount()
        {
            var count = await _context.Complaints
                .CountAsync(c => c.Status != ComplaintStatus.Closed
                    && c.Status != ComplaintStatus.Resolved
                    && !c.IsDeleted);
            return Ok(count);
        }

        [HttpGet("new-count")]
        public async Task<IActionResult> GetNewCount()
        {
            var count = await _context.Complaints
                .CountAsync(c => c.Status == ComplaintStatus.New && !c.IsDeleted);
            return Ok(count);
        }

        [HttpPost("assign")]
        [Authorize(Policy = "CanHandleComplaints")]
        public async Task<IActionResult> Assign([FromBody] AssignRequest request)
        {
            var complaint = await _context.Complaints
                .Include(c => c.SurveyResponse)
                .FirstOrDefaultAsync(c => c.Id == request.ComplaintId);

            if (complaint == null)
                return NotFound();

            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
                return BadRequest("Kullanıcı bulunamadı");

            complaint.AssignedToUserId = request.UserId;
            complaint.AssignedAt = DateTime.Now;
            complaint.Status = ComplaintStatus.InProgress;
            complaint.UpdatedAt = DateTime.Now;
            complaint.UpdatedBy = User.Identity?.Name;

            await _context.SaveChangesAsync();

            // Mail bildirimi
            if (!string.IsNullOrEmpty(user.Email))
            {
                try
                {
                    await _emailService.SendComplaintNotificationAsync(
                        user.Email,
                        complaint.TicketNumber,
                        complaint.CustomerName ?? "İsimsiz",
                        $"Size yeni bir şikayet atandı: {complaint.Title}"
                    );
                }
                catch { }
            }

            return Ok(new { success = true });
        }

        [HttpPost("add-note")]
        public async Task<IActionResult> AddNote([FromBody] AddNoteRequest request)
        {
            var complaint = await _context.Complaints.FindAsync(request.ComplaintId);
            if (complaint == null)
                return NotFound();

            var note = new ComplaintNote
            {
                ComplaintId = request.ComplaintId,
                Note = request.Note,
                IsInternal = request.IsInternal,
                UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown",
                CreatedAt = DateTime.Now
            };

            _context.ComplaintNotes.Add(note);

            // Durum güncelleme
            if (!string.IsNullOrEmpty(request.NewStatus))
            {
                complaint.Status = Enum.Parse<ComplaintStatus>(request.NewStatus);
                if (complaint.Status == ComplaintStatus.Resolved)
                    complaint.ResolvedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, noteId = note.Id });
        }
    }

    public class AssignRequest
    {
        public int ComplaintId { get; set; }
        public string UserId { get; set; } = string.Empty;
    }

    public class AddNoteRequest
    {
        public int ComplaintId { get; set; }
        public string Note { get; set; } = string.Empty;
        public bool IsInternal { get; set; } = true;
        public string? NewStatus { get; set; }
    }
}