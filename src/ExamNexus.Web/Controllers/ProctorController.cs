using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamNexus.Application.DTOs;
using ExamNexus.Application.Interfaces;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Web.Controllers;

[Authorize(Policy = "ProctorOrAdmin")]
public class ProctorController : Controller
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public ProctorController(IApplicationDbContext context, ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    public async Task<IActionResult> Index()
    {
        var tenantId = _tenantService.CurrentTenantId;

        var activeSessions = await _context.CandidateExamSessions
            .Include(s => s.Candidate)
            .Include(s => s.Exam)
            .Where(s => s.Status == ExamSessionStatus.InProgress)
            .OrderByDescending(s => s.TabSwitchCount)
            .ThenBy(s => s.TimeRemainingSeconds)
            .Select(s => new LiveCandidateSessionDto
            {
                SessionId = s.Id,
                CandidateName = s.Candidate!.FullName,
                CandidateEmail = s.Candidate.Email ?? string.Empty,
                UserCode = s.Candidate.UserCode,
                ExamTitle = s.Exam!.Title,
                Status = s.Status,
                TimeRemainingSeconds = s.TimeRemainingSeconds,
                TabSwitchCount = s.TabSwitchCount,
                MaxTabSwitchesAllowed = s.Exam.MaxTabSwitchesAllowed,
                IsFlagged = s.IsFlagged,
                FlagReason = s.FlagReason,
                StartTimeUtc = s.StartTimeUtc
            })
            .ToListAsync();

        var recentEvents = await _context.ProctorEvents
            .Include(p => p.Session)
                .ThenInclude(s => s!.Candidate)
            .Include(p => p.Session)
                .ThenInclude(s => s!.Exam)
            .OrderByDescending(p => p.TimestampUtc)
            .Take(15)
            .Select(p => new ProctorTelemetryEventDto
            {
                Id = p.Id,
                SessionId = p.SessionId,
                CandidateName = p.Session!.Candidate!.FullName,
                ExamTitle = p.Session.Exam!.Title,
                EventType = p.EventType,
                Severity = p.Severity,
                Details = p.Details,
                TabSwitchCount = p.Session.TabSwitchCount,
                TimestampUtc = p.TimestampUtc
            })
            .ToListAsync();

        var dashboard = new ProctorDashboardDto
        {
            ActiveLiveSessionsCount = activeSessions.Count,
            CompletedSessionsCount = await _context.CandidateExamSessions.CountAsync(s => s.Status == ExamSessionStatus.Submitted),
            FlaggedCheatingCount = await _context.CandidateExamSessions.CountAsync(s => s.IsFlagged),
            AverageCandidateScorePercent = await _context.CandidateExamSessions.Where(s => s.Percentage.HasValue).AverageAsync(s => s.Percentage) ?? 0.00m,
            ActiveSessions = activeSessions,
            RecentEvents = recentEvents
        };

        ViewBag.TenantId = tenantId?.ToString();
        ViewBag.TenantName = _tenantService.CurrentTenantName ?? "Acme Academy";

        return View(dashboard);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TerminateSession(Guid sessionId, string reason)
    {
        var session = await _context.CandidateExamSessions.FindAsync(sessionId);
        if (session == null)
            return NotFound();

        session.Status = ExamSessionStatus.FlaggedCheating;
        session.IsFlagged = true;
        session.FlagReason = $"Disqualified by Proctor: {reason}";
        session.EndTimeUtc = DateTime.UtcNow;

        _context.ProctorEvents.Add(new Domain.Entities.ProctorEvent
        {
            Id = Guid.NewGuid(),
            TenantId = session.TenantId,
            SessionId = session.Id,
            EventType = "TERMINATED_BY_PROCTOR",
            Severity = ProctorSeverity.Critical,
            Details = reason,
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Candidate session terminated and flagged.";
        return RedirectToAction(nameof(Index));
    }
}
