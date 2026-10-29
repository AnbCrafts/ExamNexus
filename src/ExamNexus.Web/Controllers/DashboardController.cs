using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamNexus.Application.Interfaces;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;

    public DashboardController(
        IApplicationDbContext context,
        ITenantService tenantService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _tenantService = tenantService;
        _currentUserService = currentUserService;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.TenantName = _tenantService.CurrentTenantName ?? "Acme Academy";
        ViewBag.TenantIdentifier = _tenantService.CurrentTenantIdentifier ?? "acme-corp";

        // Global / Tenant Statistics
        ViewBag.TotalCourses = await _context.Courses.CountAsync();
        ViewBag.TotalExams = await _context.Exams.CountAsync();
        ViewBag.TotalQuestions = await _context.Questions.CountAsync();
        ViewBag.ActiveLiveSessions = await _context.CandidateExamSessions.CountAsync(s => s.Status == ExamSessionStatus.InProgress);
        ViewBag.CompletedSessions = await _context.CandidateExamSessions.CountAsync(s => s.Status == ExamSessionStatus.Submitted || s.Status == ExamSessionStatus.AutoSubmittedTimeout);
        ViewBag.FlaggedSessions = await _context.CandidateExamSessions.CountAsync(s => s.IsFlagged);

        // Recent exams for candidate or instructor
        var exams = await _context.Exams
            .Include(e => e.Course)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(5)
            .ToListAsync();

        ViewBag.RecentExams = exams;

        // If Candidate: Fetch their recent sessions
        if (_currentUserService.IsInRole("Candidate"))
        {
            var mySessions = await _context.CandidateExamSessions
                .Include(s => s.Exam)
                .Where(s => s.CandidateId == _currentUserService.UserId)
                .OrderByDescending(s => s.CreatedAtUtc)
                .ToListAsync();

            ViewBag.MySessions = mySessions;
        }

        return View();
    }

    [HttpGet]
    public IActionResult Architecture()
    {
        return View();
    }
}
