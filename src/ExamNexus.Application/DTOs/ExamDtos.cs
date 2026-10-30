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

public class CreateExamRequestDto
{
    [Required(ErrorMessage = "Assessment title is required")]
    [StringLength(150, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    public Guid? CourseId { get; set; }
    public string? NewCourseCode { get; set; }
    public string? NewCourseTitle { get; set; }

    [Range(5, 360, ErrorMessage = "Duration must be between 5 and 360 minutes")]
    public int DurationMinutes { get; set; } = 30;

    [Range(10, 100, ErrorMessage = "Passing score percent must be between 10% and 100%")]
    public decimal PassingScorePercent { get; set; } = 70.00m;

    [Range(1, 10, ErrorMessage = "Allowed tab switches must be between 1 and 10")]
    public int MaxTabSwitchesAllowed { get; set; } = 3;

    public string? CustomAccessCode { get; set; }

    public List<CreateQuestionItemDto> Questions { get; set; } = new();
}

public class CreateQuestionItemDto
{
    [Required(ErrorMessage = "Question text cannot be empty")]
    public string Text { get; set; } = string.Empty;

    [Range(1, 100)]
    public decimal Points { get; set; } = 10.00m;

    public string? Explanation { get; set; }

    public int CorrectOptionIndex { get; set; } = 0; // 0..3

    [Required(ErrorMessage = "Option A is required")]
    public string OptionA { get; set; } = string.Empty;

    [Required(ErrorMessage = "Option B is required")]
    public string OptionB { get; set; } = string.Empty;

    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
}

public class ExamCreatedSuccessDto
{
    public Guid ExamId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AccessCode { get; set; } = string.Empty;
    public string ShareableUrl { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public decimal TotalMarks { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public int ExamsCreatedCount { get; set; }
    public int MaxExamsAllowed { get; set; }
}
