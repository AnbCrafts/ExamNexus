using System.ComponentModel.DataAnnotations;
using ExamNexus.Domain.Entities;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Application.DTOs;

public class SubscriptionRequestInputDto
{
    [Required(ErrorMessage = "Organization name is required.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Organization name must be 3-150 characters.")]
    public string OrganizationName { get; set; } = string.Empty;

    public string? SuggestedIdentifier { get; set; }

    [Required(ErrorMessage = "Contact person name is required.")]
    [StringLength(100, MinimumLength = 2)]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Work email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string ContactEmail { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number.")]
    public string? ContactPhone { get; set; }

    [Required]
    public TenantPlan RequestedPlan { get; set; } = TenantPlan.Professional;

    [Range(10, 100000, ErrorMessage = "Candidate slots must be between 10 and 100,000.")]
    public int EstimatedCandidates { get; set; } = 500;

    [StringLength(1000)]
    public string? AdditionalNotes { get; set; }
}

public class CreateTenantInputDto
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Identifier must be lowercase alphanumeric with hyphens (e.g. harvard-univ).")]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    public TenantPlan Plan { get; set; } = TenantPlan.Professional;

    [Range(10, 50000)]
    public int MaxCandidateSlots { get; set; } = 1000;

    public string BrandColorHex { get; set; } = "#4f46e5";

    [Required]
    [EmailAddress]
    public string AdminEmail { get; set; } = string.Empty;

    [Required]
    public string AdminFirstName { get; set; } = string.Empty;

    [Required]
    public string AdminLastName { get; set; } = string.Empty;

    public string DefaultPassword { get; set; } = "P@ssword123!";
}

public class UpdateTenantSubscriptionDto
{
    public Guid TenantId { get; set; }
    public TenantPlan Plan { get; set; }
    public int MaxCandidateSlots { get; set; }
}

public class TenantManagementViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public TenantPlan Plan { get; set; }
    public int MaxCandidateSlots { get; set; }
    public string BrandColorHex { get; set; } = "#0284c7";
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Counts
    public int TotalUsers { get; set; }
    public int TotalExams { get; set; }
    public int TotalSessions { get; set; }
}

public class PlatformAdminDashboardViewModel
{
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int TotalPlatformExams { get; set; }
    public int TotalPlatformSessions { get; set; }
    public int PendingRequestsCount { get; set; }

    public List<TenantManagementViewModel> Tenants { get; set; } = new();
    public List<SubscriptionRequest> SubscriptionRequests { get; set; } = new();
}
