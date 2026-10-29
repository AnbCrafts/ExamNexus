using ExamNexus.Domain.Common;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Domain.Entities;

public class Course : BaseTenantEntity
{
    public string Code { get; set; } = string.Empty; // e.g. "CS-101"
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CreatedByInstructorId { get; set; } = string.Empty;

    // Navigations
    public virtual Tenant? Tenant { get; set; }
    public virtual ApplicationUser? CreatedByInstructor { get; set; }
    public virtual ICollection<QuestionBank> QuestionBanks { get; set; } = new List<QuestionBank>();
    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();
}

public class QuestionBank : BaseTenantEntity
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigations
    public virtual Course? Course { get; set; }
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}

public class Question : BaseTenantEntity
{
    public Guid BankId { get; set; }
    public string Text { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;
    public decimal Points { get; set; } = 1.00m;
    public string? Explanation { get; set; }

    // Navigations
    public virtual QuestionBank? Bank { get; set; }
    public virtual ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}

public class QuestionOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; } = false;
    public int OrderIndex { get; set; } = 1;

    // Navigation
    public virtual Question? Question { get; set; }
}
