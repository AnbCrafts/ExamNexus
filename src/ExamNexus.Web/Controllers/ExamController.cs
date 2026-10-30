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

        _context.ProctorEvents.Add(new ProctorEvent
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

    [HttpGet]
    [Authorize(Policy = "InstructorOrAdmin")]
    public async Task<IActionResult> Create()
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantService.CurrentTenantId);
        var examCount = await _context.Exams.CountAsync();
        var maxAllowed = tenant?.PlanMaxExams ?? 5;

        ViewBag.ExamCount = examCount;
        ViewBag.MaxAllowed = maxAllowed;
        ViewBag.PlanName = tenant?.Plan.ToString() ?? "Professional";
        ViewBag.CanCreate = examCount < maxAllowed;
        ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();

        var defaultModel = new CreateExamRequestDto
        {
            CustomAccessCode = $"NX-{Random.Shared.Next(1000, 9999)}",
            DurationMinutes = 30,
            PassingScorePercent = 70.00m,
            MaxTabSwitchesAllowed = 3,
            Questions = new List<CreateQuestionItemDto>
            {
                new()
                {
                    Text = "What is the primary benefit of deploying applications across multiple availability zones?",
                    Points = 10,
                    CorrectOptionIndex = 1,
                    OptionA = "Reduced computational licensing cost",
                    OptionB = "High availability and protection against datacenter failures",
                    OptionC = "Elimination of external identity providers",
                    OptionD = "Local database storage caching"
                },
                new()
                {
                    Text = "Which security architecture operates on the principle of 'never trust, always explicitly verify'?",
                    Points = 10,
                    CorrectOptionIndex = 2,
                    OptionA = "Perimeter Defense Only",
                    OptionB = "Implicit Network Trust",
                    OptionC = "Zero Trust Security Architecture",
                    OptionD = "Static Firewall Access Control"
                }
            }
        };

        return View(defaultModel);
    }

    [HttpPost]
    [Authorize(Policy = "InstructorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateExamRequestDto model)
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantService.CurrentTenantId);
        var examCount = await _context.Exams.CountAsync();
        var maxAllowed = tenant?.PlanMaxExams ?? 5;

        if (examCount >= maxAllowed)
        {
            ModelState.AddModelError(string.Empty, $"Subscription limit reached! Your {tenant?.Plan} plan allows up to {maxAllowed} assessments. Please upgrade your subscription to create additional exams.");
            ViewBag.ExamCount = examCount;
            ViewBag.MaxAllowed = maxAllowed;
            ViewBag.PlanName = tenant?.Plan.ToString() ?? "Professional";
            ViewBag.CanCreate = false;
            ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();
            return View(model);
        }

        if (model.Questions == null || model.Questions.Count == 0)
        {
            ModelState.AddModelError("Questions", "You must provide at least one question for this assessment.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ExamCount = examCount;
            ViewBag.MaxAllowed = maxAllowed;
            ViewBag.PlanName = tenant?.Plan.ToString() ?? "Professional";
            ViewBag.CanCreate = true;
            ViewBag.Courses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();
            return View(model);
        }

        // 1. Resolve Course
        Guid courseId;
        if (model.CourseId.HasValue && model.CourseId.Value != Guid.Empty)
        {
            courseId = model.CourseId.Value;
        }
        else
        {
            var courseCode = string.IsNullOrWhiteSpace(model.NewCourseCode) ? "CRS-GEN" : model.NewCourseCode.Trim().ToUpperInvariant();
            var courseTitle = string.IsNullOrWhiteSpace(model.NewCourseTitle) ? $"{model.Title} Curriculum" : model.NewCourseTitle.Trim();

            var newCourse = new Course
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantService.CurrentTenantId ?? Guid.Empty,
                Code = courseCode,
                Title = courseTitle,
                Description = "Custom assessment curriculum container",
                CreatedByInstructorId = _currentUserService.UserId ?? string.Empty
            };
            _context.Courses.Add(newCourse);
            courseId = newCourse.Id;
        }

        // 2. Create QuestionBank
        var bank = new QuestionBank
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantService.CurrentTenantId ?? Guid.Empty,
            CourseId = courseId,
            Title = $"{model.Title} Question Pool",
            Description = "Automated pool generated with assessment"
        };
        _context.QuestionBanks.Add(bank);

        // 3. Resolve AccessCode
        var accessCode = string.IsNullOrWhiteSpace(model.CustomAccessCode)
            ? $"NX-{Random.Shared.Next(1000, 9999)}"
            : model.CustomAccessCode.Trim().ToUpperInvariant();

        // 4. Create Exam
        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantService.CurrentTenantId ?? Guid.Empty,
            CourseId = courseId,
            Title = model.Title.Trim(),
            AccessCode = accessCode,
            DurationMinutes = model.DurationMinutes,
            PassingScorePercent = model.PassingScorePercent,
            MaxTabSwitchesAllowed = model.MaxTabSwitchesAllowed,
            IsActive = true,
            CreatedById = _currentUserService.UserId ?? string.Empty,
            CreatedAtUtc = DateTime.UtcNow
        };

        decimal totalMarks = 0;
        int orderIndex = 1;

        foreach (var qDto in model.Questions)
        {
            var question = new Question
            {
                Id = Guid.NewGuid(),
                TenantId = exam.TenantId,
                BankId = bank.Id,
                Text = qDto.Text.Trim(),
                Points = qDto.Points,
                Explanation = qDto.Explanation?.Trim(),
                QuestionType = QuestionType.MultipleChoice
            };

            var optionsText = new[] { qDto.OptionA, qDto.OptionB, qDto.OptionC, qDto.OptionD };
            for (int i = 0; i < optionsText.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(optionsText[i])) continue;
                question.Options.Add(new QuestionOption
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    OptionText = optionsText[i].Trim(),
                    IsCorrect = (i == qDto.CorrectOptionIndex),
                    OrderIndex = i + 1
                });
            }

            _context.Questions.Add(question);

            exam.ExamQuestions.Add(new ExamQuestion
            {
                Id = Guid.NewGuid(),
                TenantId = exam.TenantId,
                ExamId = exam.Id,
                QuestionId = question.Id,
                OrderIndex = orderIndex++
            });

            totalMarks += qDto.Points;
        }

        exam.TotalMarks = totalMarks > 0 ? totalMarks : 100.00m;
        _context.Exams.Add(exam);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Assessment '{exam.Title}' created successfully! Share link is now live.";
        return RedirectToAction(nameof(Share), new { id = exam.Id });
    }

    [HttpGet]
    [Authorize(Policy = "InstructorOrAdmin")]
    public async Task<IActionResult> Share(Guid id)
    {
        var exam = await _context.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exam == null) return NotFound();

        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantService.CurrentTenantId);
        var examCount = await _context.Exams.CountAsync();

        var dto = new ExamCreatedSuccessDto
        {
            ExamId = exam.Id,
            Title = exam.Title,
            AccessCode = exam.AccessCode,
            ShareableUrl = $"{Request.Scheme}://{Request.Host}/Exam/Join?code={exam.AccessCode}",
            DurationMinutes = exam.DurationMinutes,
            TotalQuestions = exam.ExamQuestions.Count,
            TotalMarks = exam.TotalMarks,
            TenantName = tenant?.Name ?? "Acme Academy",
            ExamsCreatedCount = examCount,
            MaxExamsAllowed = tenant?.PlanMaxExams ?? 5
        };

        return View(dto);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Join(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return RedirectToAction(nameof(Index));
        }

        code = code.Trim().ToUpperInvariant();

        // If not authenticated, send to login with returnUrl
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = $"/Exam/Join?code={code}" });
        }

        var exam = await _context.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions)
            .FirstOrDefaultAsync(e => e.AccessCode == code && e.IsActive);

        if (exam == null)
        {
            TempData["ErrorMessage"] = $"No active assessment found matching access code '{code}'.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.AccessCode = code;
        return View("JoinConfirm", exam);
    }
}
