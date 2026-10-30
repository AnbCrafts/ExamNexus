using System.ComponentModel.DataAnnotations.Schema;
using ExamNexus.Domain.Common;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Identifier { get; set; } = string.Empty; // Subdomain slug e.g. "acme-corp"
    public string Name { get; set; } = string.Empty;
    public TenantPlan Plan { get; set; } = TenantPlan.Professional;
    public int MaxCandidateSlots { get; set; } = 500;
    public string? BrandColorHex { get; set; } = "#2563eb";
    public bool IsActive { get; set; } = true;

    [NotMapped]
    public int PlanMaxExams => Plan switch
    {
        TenantPlan.Free => 2,
        TenantPlan.Professional => 5,
        TenantPlan.Enterprise => 9999,
        _ => 5
    };

    // Navigations
    public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public virtual ICollection<Course> Courses { get; set; } = new List<Course>();
    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
