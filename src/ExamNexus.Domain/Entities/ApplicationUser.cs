using Microsoft.AspNetCore.Identity;

namespace ExamNexus.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public Guid? TenantId { get; set; } // Nullable for global SystemAdmin
    public string UserCode { get; set; } = string.Empty; // Student or Staff ID
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    // Navigations
    public virtual Tenant? Tenant { get; set; }
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public virtual ICollection<CandidateExamSession> ExamSessions { get; set; } = new List<CandidateExamSession>();
}

public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName, string? description = null) : base(roleName)
    {
        Description = description;
    }
}
