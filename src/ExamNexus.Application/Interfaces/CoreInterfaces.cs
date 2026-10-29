namespace ExamNexus.Application.Interfaces;

public interface ITenantService
{
    Guid? CurrentTenantId { get; }
    string? CurrentTenantIdentifier { get; }
    string? CurrentTenantName { get; }
    void SetTenant(Guid tenantId, string identifier, string name);
}

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserEmail { get; }
    string? FullName { get; }
    Guid? TenantId { get; }
    bool IsAuthenticated { get; }
    IEnumerable<string> Roles { get; }
    bool IsInRole(string role);
}

public interface IProctorHubClient
{
    Task CandidateJoined(Guid sessionId, string candidateName, string examTitle);
    Task ProctorAlertReceived(Guid sessionId, string eventType, int severity, string details, int tabSwitchCount);
    Task CandidateSubmitted(Guid sessionId, string candidateName, decimal score, decimal percentage, bool passed);
    Task ExamSessionTerminated(Guid sessionId, string reason);
}
