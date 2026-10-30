using ExamNexus.Domain.Common;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Domain.Entities;

public enum RequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public class SubscriptionRequest : BaseEntity
{
    public string OrganizationName { get; set; } = string.Empty;
    public string? SuggestedIdentifier { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public TenantPlan RequestedPlan { get; set; } = TenantPlan.Professional;
    public int EstimatedCandidates { get; set; } = 500;
    public string? AdditionalNotes { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? ProcessedByUserId { get; set; }
    public Guid? CreatedTenantId { get; set; }

    // Navigations
    public virtual Tenant? CreatedTenant { get; set; }
}
