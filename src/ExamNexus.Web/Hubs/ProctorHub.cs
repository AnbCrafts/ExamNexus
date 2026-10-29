using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ExamNexus.Application.Interfaces;
using ExamNexus.Domain.Enums;

namespace ExamNexus.Web.Hubs;

public class ProctorHub : Hub<IProctorHubClient>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ProctorHub> _logger;

    public ProctorHub(IApplicationDbContext context, ILogger<ProctorHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    // 1. Proctors join their tenant's broadcast group
    public async Task JoinProctorGroup(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Tenant_{tenantId}");
        _logger.LogInformation("Proctor connection {ConnectionId} joined Tenant_{TenantId}", Context.ConnectionId, tenantId);
    }

    // 2. Candidate sends periodic heartbeat / time sync
    public async Task CandidateHeartbeat(Guid sessionId, int timeRemainingSeconds)
    {
        var session = await _context.CandidateExamSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session != null && session.Status == ExamSessionStatus.InProgress)
        {
            session.TimeRemainingSeconds = timeRemainingSeconds;
            await _context.SaveChangesAsync();
        }
    }

    // 3. Real-Time Anti-Cheat Alert (Tab switch, window blur, second screen detected)
    public async Task SendProctorWarning(Guid sessionId, string eventType, int severity, string details)
    {
        var session = await _context.CandidateExamSessions
            .IgnoreQueryFilters()
            .Include(s => s.Candidate)
            .Include(s => s.Exam)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null || session.Status != ExamSessionStatus.InProgress)
            return;

        // Increment tab switches
        session.TabSwitchCount++;
        int maxAllowed = session.Exam?.MaxTabSwitchesAllowed ?? 3;

        if (session.TabSwitchCount > maxAllowed)
        {
            session.IsFlagged = true;
            session.FlagReason = $"Exceeded maximum allowed tab switches ({session.TabSwitchCount}/{maxAllowed}).";
        }

        // Record proctor telemetry event
        _context.ProctorEvents.Add(new Domain.Entities.ProctorEvent
        {
            Id = Guid.NewGuid(),
            TenantId = session.TenantId,
            SessionId = session.Id,
            EventType = eventType,
            Severity = (ProctorSeverity)severity,
            Details = details,
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // Broadcast live alert to all Proctors in this tenant
        await Clients.Group($"Tenant_{session.TenantId}").ProctorAlertReceived(
            sessionId,
            eventType,
            severity,
            details,
            session.TabSwitchCount
        );
    }
}
