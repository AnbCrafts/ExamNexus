using ExamNexus.Domain.Common;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Domain.Entities;

public class CandidateExamSession : BaseTenantEntity
{
    public Guid ExamId { get; set; }
    public string CandidateId { get; set; } = string.Empty;
    public ExamSessionStatus Status { get; set; } = ExamSessionStatus.NotStarted;
    public DateTime? StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public int TimeRemainingSeconds { get; set; }
    public decimal? FinalScore { get; set; }
    public decimal? Percentage { get; set; }
    public bool? HasPassed { get; set; }
    public int TabSwitchCount { get; set; } = 0;
    public bool IsFlagged { get; set; } = false;
    public string? FlagReason { get; set; }
    public Guid SessionKey { get; set; } = Guid.NewGuid();

    // Concurrency Token
    public byte[]? RowVersion { get; set; }

    // Navigations
    public virtual Tenant? Tenant { get; set; }
    public virtual Exam? Exam { get; set; }
    public virtual ApplicationUser? Candidate { get; set; }
    public virtual ICollection<CandidateAnswer> Answers { get; set; } = new List<CandidateAnswer>();
    public virtual ICollection<ProctorEvent> ProctorEvents { get; set; } = new List<ProctorEvent>();
}

public class CandidateAnswer : BaseTenantEntity
{
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? TextResponse { get; set; }
    public bool IsEvaluated { get; set; } = false;
    public decimal AwardedMarks { get; set; } = 0.00m;
    public DateTime AnsweredAtUtc { get; set; } = DateTime.UtcNow;

    // Navigations
    public virtual CandidateExamSession? Session { get; set; }
    public virtual Question? Question { get; set; }
    public virtual QuestionOption? SelectedOption { get; set; }
}

public class ProctorEvent : BaseTenantEntity
{
    public Guid SessionId { get; set; }
    public string EventType { get; set; } = string.Empty; // TAB_SWITCH, WINDOW_BLUR, HEARTBEAT, CHEAT_FLAGGED
    public ProctorSeverity Severity { get; set; } = ProctorSeverity.Info;
    public string? Details { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual CandidateExamSession? Session { get; set; }
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
