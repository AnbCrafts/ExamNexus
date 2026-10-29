using Microsoft.AspNetCore.Http;
using ExamNexus.Application.Interfaces;

namespace ExamNexus.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _currentTenantId;
    private string? _currentTenantIdentifier;
    private string? _currentTenantName;

    public TenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        ResolveTenant();
    }

    public Guid? CurrentTenantId => _currentTenantId;
    public string? CurrentTenantIdentifier => _currentTenantIdentifier;
    public string? CurrentTenantName => _currentTenantName;

    public void SetTenant(Guid tenantId, string identifier, string name)
    {
        _currentTenantId = tenantId;
        _currentTenantIdentifier = identifier;
        _currentTenantName = name;
    }

    private void ResolveTenant()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return;

        // 1. Check User Claims (if authenticated)
        var user = context.User;
        var claimTenant = user.FindFirst("TenantId")?.Value;
        if (Guid.TryParse(claimTenant, out var tenantGuid))
        {
            _currentTenantId = tenantGuid;
            _currentTenantIdentifier = user.FindFirst("TenantIdentifier")?.Value;
            _currentTenantName = user.FindFirst("TenantName")?.Value;
            return;
        }

        // 2. Check HTTP Header (e.g. for REST APIs or AJAX)
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerTenant) &&
            Guid.TryParse(headerTenant.ToString(), out var headerGuid))
        {
            _currentTenantId = headerGuid;
            return;
        }

        // 3. Check Query parameter or Cookie
        if (context.Request.Query.TryGetValue("tenant", out var queryTenant))
        {
            _currentTenantIdentifier = queryTenant.ToString().ToLowerInvariant();
        }
        else if (context.Request.Cookies.TryGetValue(".ExamNexus.Tenant", out var cookieTenant))
        {
            _currentTenantIdentifier = cookieTenant;
        }
    }
}
