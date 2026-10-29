using Microsoft.EntityFrameworkCore;
using ExamNexus.Application.Interfaces;
using ExamNexus.Infrastructure.Data;

namespace ExamNexus.Web.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantService tenantService, ApplicationDbContext dbContext)
    {
        // 1. Authenticated User Claims
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("TenantId")?.Value;
            if (Guid.TryParse(tenantClaim, out var tenantGuid))
            {
                var identifier = context.User.FindFirst("TenantIdentifier")?.Value ?? "acme-corp";
                var name = context.User.FindFirst("TenantName")?.Value ?? "Acme Academy";
                tenantService.SetTenant(tenantGuid, identifier, name);
                context.Response.Headers.Append("X-Tenant-ID", tenantGuid.ToString());
            }
        }
        else
        {
            // 2. Anonymous / Login page: Check Query string or Cookie
            string? slug = null;
            if (context.Request.Query.TryGetValue("tenant", out var querySlug))
            {
                slug = querySlug.ToString().ToLowerInvariant();
            }
            else if (context.Request.Cookies.TryGetValue(".ExamNexus.Tenant", out var cookieSlug))
            {
                slug = cookieSlug;
            }

            if (!string.IsNullOrEmpty(slug))
            {
                var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Identifier == slug && t.IsActive);
                if (tenant != null)
                {
                    tenantService.SetTenant(tenant.Id, tenant.Identifier, tenant.Name);
                    context.Response.Cookies.Append(".ExamNexus.Tenant", tenant.Identifier, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(30)
                    });
                }
            }
        }

        await _next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }
}
