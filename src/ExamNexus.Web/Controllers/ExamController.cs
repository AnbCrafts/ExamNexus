using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamNexus.Application.DTOs;
using ExamNexus.Application.Interfaces;
using ExamNexus.Domain.Entities;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Web.Controllers;

[Authorize]
public class ExamController : Controller
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;

    public ExamController(
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
        var candidateId = _currentUserService.UserId;
        var exams = await _context.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions)
            .Include(e => e.Sessions.Where(s => s.CandidateId == candidateId))
            .Where(e => e.IsActive)
            .OrderBy(e => e.Title)
            .Select(e => new ExamSummaryDto
            {
                Id = e.Id,
                CourseTitle = e.Course!.Title,
                CourseCode = e.Course.Code,
                Title = e.Title,
                AccessCode = e.AccessCode,
                DurationMinutes = e.DurationMinutes,
                PassingScorePercent = e.PassingScorePercent,
                TotalMarks = e.TotalMarks,
                TotalQuestions = e.ExamQuestions.Count,
                HasActiveSession = e.Sessions.Any(s => s.CandidateId == candidateId && s.Status == ExamSessionStatus.InProgress),
                ActiveSessionId = e.Sessions.Where(s => s.CandidateId == candidateId && s.Status == ExamSessionStatus.InProgress).Select(s => (Guid?)s.Id).FirstOrDefault(),
                LastSessionStatus = e.Sessions.Where(s => s.CandidateId == candidateId).OrderByDescending(s => s.CreatedAtUtc).Select(s => (ExamSessionStatus?)s.Status).FirstOrDefault()
            })
            .ToListAsync();

        return View(exams);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(StartExamDto model)
    {
        var exam = await _context.Exams
            .Include(e => e.ExamQuestions)
            .FirstOrDefaultAsync(e => e.Id == model.ExamId && e.IsActive);

        if (exam == null)
        {
            TempData["ErrorMessage"] = "Exam not found or inactive.";
            return RedirectToAction(nameof(Index));
        }

        if (!exam.AccessCode.Equals(model.AccessCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Invalid exam access passcode.";
            return RedirectToAction(nameof(Index));
        }

        var candidateId = _currentUserService.UserId ?? string.Empty;

        // Check existing session
        var existingSession = await _context.CandidateExamSessions
            .FirstOrDefaultAsync(s => s.ExamId == exam.Id && s.CandidateId == candidateId);

        if (existingSession != null)
        {
            if (existingSession.Status == ExamSessionStatus.InProgress)
            {
                return RedirectToAction(nameof(Take), new { sessionId = existingSession.Id });
            }
            if (existingSession.Status == ExamSessionStatus.Submitted || existingSession.Status == ExamSessionStatus.AutoSubmittedTimeout)
            {
                return RedirectToAction(nameof(Result), new { sessionId = existingSession.Id });
            }
        }

        // Initialize fresh session
        var session = new CandidateExamSession
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantService.CurrentTenantId ?? Guid.Empty,
            ExamId = exam.Id,
            CandidateId = candidateId,
            Status = ExamSessionStatus.InProgress,
            StartTimeUtc = DateTime.UtcNow,
            TimeRemainingSeconds = exam.DurationMinutes * 60,
            TabSwitchCount = 0,
            IsFlagged = false
        };

        // Initialize empty answer slots
        foreach (var eq in exam.ExamQuestions)
        {
            session.Answers.Add(new CandidateAnswer
            {
                Id = Guid.NewGuid(),
                TenantId = session.TenantId,
                SessionId = session.Id,
                QuestionId = eq.QuestionId,
                IsEvaluated = false,
                AwardedMarks = 0.00m
            });
        }

        session.ProctorEvents.Add(new ProctorEvent
        {
            Id = Guid.NewGuid(),
            TenantId = session.TenantId,
            SessionId = session.Id,
            EventType = "EXAM_STARTED",
            Severity = ProctorSeverity.Info,
            Details = $"Candidate launched exam session with {exam.DurationMinutes} minutes allotted.",
            TimestampUtc = DateTime.UtcNow
        });

        _context.CandidateExamSessions.Add(session);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Take), new { sessionId = session.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Take(Guid sessionId)
    {
        var candidateId = _currentUserService.UserId;
        var session = await _context.CandidateExamSessions
            .Include(s => s.Exam)
                .ThenInclude(e => e!.ExamQuestions.OrderBy(eq => eq.OrderIndex))
                    .ThenInclude(eq => eq.Question)
                        .ThenInclude(q => q!.Options.OrderBy(o => o.OrderIndex))
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CandidateId == candidateId);

        if (session == null)
            return NotFound();

        if (session.Status != ExamSessionStatus.InProgress)
        {
            return RedirectToAction(nameof(Result), new { sessionId });
        }

        // Calculate dynamic elapsed time
        var totalAllotted = (session.Exam?.DurationMinutes ?? 60) * 60;
        var elapsed = (int)(DateTime.UtcNow - (session.StartTimeUtc ?? DateTime.UtcNow)).TotalSeconds;
        var remaining = Math.Max(0, totalAllotted - elapsed);
        session.TimeRemainingSeconds = remaining;

        var dto = new ExamSessionLiveDto
        {
            SessionId = session.Id,
            ExamId = session.ExamId,
            ExamTitle = session.Exam!.Title,
            CandidateName = _currentUserService.FullName ?? "Candidate",
            DurationMinutes = session.Exam.DurationMinutes,
            TimeRemainingSeconds = remaining,
            TabSwitchCount = session.TabSwitchCount,
            MaxTabSwitchesAllowed = session.Exam.MaxTabSwitchesAllowed,
            Questions = session.Exam.ExamQuestions.Select(eq => new QuestionInSessionDto
            {
                QuestionId = eq.QuestionId,
                OrderIndex = eq.OrderIndex,
                Text = eq.Question!.Text,
                Points = eq.Question.Points,
                SelectedOptionId = session.Answers.FirstOrDefault(a => a.QuestionId == eq.QuestionId)?.SelectedOptionId,
                Options = eq.Question.Options.Select(o => new OptionInSessionDto
                {
                    OptionId = o.Id,
                    OptionText = o.OptionText
                }).ToList()
            }).ToList()
        };

        ViewBag.TenantId = _tenantService.CurrentTenantId?.ToString();
        return View(dto);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // AJAX endpoint with live answers
    public async Task<IActionResult> SaveAnswer([FromBody] SubmitAnswerDto model)
    {
        var candidateId = _currentUserService.UserId;
        var session = await _context.CandidateExamSessions
            .FirstOrDefaultAsync(s => s.Id == model.SessionId && s.CandidateId == candidateId && s.Status == ExamSessionStatus.InProgress);

        if (session == null)
            return Unauthorized();

        var answer = await _context.CandidateAnswers
            .FirstOrDefaultAsync(a => a.SessionId == model.SessionId && a.QuestionId == model.QuestionId);

        if (answer == null)
        {
            answer = new CandidateAnswer
            {
                Id = Guid.NewGuid(),
                TenantId = session.TenantId,
                SessionId = session.Id,
                QuestionId = model.QuestionId,
                SelectedOptionId = model.SelectedOptionId,
                TextResponse = model.TextResponse,
                AnsweredAtUtc = DateTime.UtcNow
            };
            _context.CandidateAnswers.Add(answer);
        }
        else
        {
            answer.SelectedOptionId = model.SelectedOptionId;
            answer.TextResponse = model.TextResponse;
            answer.AnsweredAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, savedAt = DateTime.UtcNow.ToString("HH:mm:ss") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid sessionId)
    {
        var candidateId = _currentUserService.UserId;
        var session = await _context.CandidateExamSessions
            .Include(s => s.Exam)
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CandidateId == candidateId);

        if (session == null)
            return NotFound();

        if (session.Status != ExamSessionStatus.InProgress)
        {
            return RedirectToAction(nameof(Result), new { sessionId });
        }

        // Auto-Grade against QuestionOptions
        decimal totalScore = 0.00m;
        var questionIds = session.Answers.Select(a => a.QuestionId).ToList();
        var questions = await _context.Questions
            .Include(q => q.Options)
            .Where(q => questionIds.Contains(q.Id))
            .ToListAsync();

        foreach (var answer in session.Answers)
        {
            var q = questions.FirstOrDefault(x => x.Id == answer.QuestionId);
            if (q != null && answer.SelectedOptionId.HasValue)
            {
                var correctOption = q.Options.FirstOrDefault(o => o.IsCorrect);
                if (correctOption != null && correctOption.Id == answer.SelectedOptionId.Value)
                {
                    answer.AwardedMarks = q.Points;
                    totalScore += q.Points;
                }
                else
                {
                    answer.AwardedMarks = 0.00m;
                }
            }
            answer.IsEvaluated = true;
        }

        var totalMarks = session.Exam?.TotalMarks ?? 100.00m;
        var percentage = totalMarks > 0 ? Math.Round((totalScore / totalMarks) * 100.0m, 2) : 0.00m;
        var passingScore = session.Exam?.PassingScorePercent ?? 70.00m;
        var hasPassed = percentage >= passingScore;

        session.Status = ExamSessionStatus.Submitted;
        session.EndTimeUtc = DateTime.UtcNow;
        session.TimeRemainingSeconds = 0;
        session.FinalScore = totalScore;
        session.Percentage = percentage;
        session.HasPassed = hasPassed;

        // Check tab switches
        if (session.TabSwitchCount > (session.Exam?.MaxTabSwitchesAllowed ?? 3))
        {
            session.IsFlagged = true;
            session.FlagReason = $"Exceeded allowable tab switches ({session.TabSwitchCount}). Flagged for review.";
        }

        session.ProctorEvents.Add(new ProctorEvent
        {
            Id = Guid.NewGuid(),
            TenantId = session.TenantId,
            SessionId = session.Id,
            EventType = "EXAM_SUBMITTED",
            Severity = ProctorSeverity.Info,
            Details = $"Exam submitted. Final Score: {totalScore}/{totalMarks} ({percentage}%). Passed: {hasPassed}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Assessment submitted and evaluated successfully.";
        return RedirectToAction(nameof(Result), new { sessionId });
    }

    [HttpGet]
    public async Task<IActionResult> Result(Guid sessionId)
    {
        var candidateId = _currentUserService.UserId;
        var session = await _context.CandidateExamSessions
            .Include(s => s.Exam)
            .Include(s => s.Candidate)
            .FirstOrDefaultAsync(s => s.Id == sessionId && (s.CandidateId == candidateId || User.IsInRole("Instructor") || User.IsInRole("Proctor") || User.IsInRole("TenantAdmin")));

        if (session == null)
            return NotFound();

        var dto = new ExamResultDto
        {
            SessionId = session.Id,
            ExamTitle = session.Exam?.Title ?? "Assessment",
            CandidateName = session.Candidate?.FullName ?? "Candidate",
            FinalScore = session.FinalScore ?? 0.00m,
            TotalMarks = session.Exam?.TotalMarks ?? 100.00m,
            Percentage = session.Percentage ?? 0.00m,
            HasPassed = session.HasPassed ?? false,
            IsFlagged = session.IsFlagged,
            FlagReason = session.FlagReason,
            CompletedAtUtc = session.EndTimeUtc
        };

        return View(dto);
    }
}
