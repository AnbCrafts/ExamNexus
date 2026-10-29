using ExamNexus.Domain.Common;

namespace ExamNexus.Domain.Entities;

public class Exam : BaseTenantEntity
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AccessCode { get; set; } = string.Empty; // Randomized exam entry code e.g. "EXAM-9941"
    public int DurationMinutes { get; set; } = 60;
    public decimal PassingScorePercent { get; set; } = 70.00m;
    public decimal TotalMarks { get; set; } = 100.00m;
    public int MaxTabSwitchesAllowed { get; set; } = 3;
    public bool IsActive { get; set; } = true;
    public string CreatedById { get; set; } = string.Empty;

    // Navigations
    public virtual Tenant? Tenant { get; set; }
    public virtual Course? Course { get; set; }
    public virtual ApplicationUser? CreatedBy { get; set; }
    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
    public virtual ICollection<CandidateExamSession> Sessions { get; set; } = new List<CandidateExamSession>();
}

public class ExamQuestion : BaseTenantEntity
{
    public Guid ExamId { get; set; }
    public Guid QuestionId { get; set; }
    public int OrderIndex { get; set; } = 1;

    // Navigations
    public virtual Exam? Exam { get; set; }
    public virtual Question? Question { get; set; }
}
