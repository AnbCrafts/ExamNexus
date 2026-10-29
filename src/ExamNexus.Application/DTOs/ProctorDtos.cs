using ExamNexus.Domain.Enums;

namespace ExamNexus.Application.DTOs;

public class ProctorDashboardDto
{
    public int ActiveLiveSessionsCount { get; set; }
    public int CompletedSessionsCount { get; set; }
    public int FlaggedCheatingCount { get; set; }
    public decimal AverageCandidateScorePercent { get; set; }
    public List<LiveCandidateSessionDto> ActiveSessions { get; set; } = new();
    public List<ProctorTelemetryEventDto> RecentEvents { get; set; } = new();
}

public class LiveCandidateSessionDto
{
    public Guid SessionId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string UserCode { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public ExamSessionStatus Status { get; set; }
    public int TimeRemainingSeconds { get; set; }
    public int TabSwitchCount { get; set; }
    public int MaxTabSwitchesAllowed { get; set; }
    public bool IsFlagged { get; set; }
    public string? FlagReason { get; set; }
    public DateTime? StartTimeUtc { get; set; }
}

public class ProctorTelemetryEventDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // TAB_SWITCH, WINDOW_BLUR, CHEAT_FLAGGED
    public ProctorSeverity Severity { get; set; }
    public string? Details { get; set; }
    public int TabSwitchCount { get; set; }
    public DateTime TimestampUtc { get; set; }
}
