using System.ComponentModel.DataAnnotations;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Application.DTOs;

public class ExamSummaryDto
{
    public Guid Id { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string AccessCode { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public decimal PassingScorePercent { get; set; }
    public decimal TotalMarks { get; set; }
    public int TotalQuestions { get; set; }
    public bool HasActiveSession { get; set; }
    public Guid? ActiveSessionId { get; set; }
    public ExamSessionStatus? LastSessionStatus { get; set; }
}

public class StartExamDto
{
    [Required]
    public Guid ExamId { get; set; }

    [Required]
    [Display(Name = "Exam Access Passcode")]
    public string AccessCode { get; set; } = string.Empty;
}

public class ExamSessionLiveDto
{
    public Guid SessionId { get; set; }
    public Guid ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int TimeRemainingSeconds { get; set; }
    public int TabSwitchCount { get; set; }
    public int MaxTabSwitchesAllowed { get; set; }
    public List<QuestionInSessionDto> Questions { get; set; } = new();
}

public class QuestionInSessionDto
{
    public Guid QuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public decimal Points { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? TextResponse { get; set; }
    public List<OptionInSessionDto> Options { get; set; } = new();
}

public class OptionInSessionDto
{
    public Guid OptionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
}

public class SubmitAnswerDto
{
    [Required] public Guid SessionId { get; set; }
    [Required] public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? TextResponse { get; set; }
}

public class ExamResultDto
{
    public Guid SessionId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public decimal FinalScore { get; set; }
    public decimal TotalMarks { get; set; }
    public decimal Percentage { get; set; }
    public bool HasPassed { get; set; }
    public bool IsFlagged { get; set; }
    public string? FlagReason { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
