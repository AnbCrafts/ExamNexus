using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamNexus.Application.DTOs;
using ExamNexus.Application.Interfaces;
using ExamNexus.Domain.Entities;
using ExamNexus.Domain.Enums;
using ExamNexus.Infrastructure.Data;

namespace ExamNexus.Web.Controllers;

[Authorize(Policy = "SystemAdminOnly")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUserService,
        ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tenants = await _dbContext.Tenants
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();

        // Using IgnoreQueryFilters to aggregate global platform telemetry
        var tenantViewModels = new List<TenantManagementViewModel>();
        foreach (var t in tenants)
        {
            var userCount = await _dbContext.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == t.Id);
            var examCount = await _dbContext.Exams.IgnoreQueryFilters().CountAsync(e => e.TenantId == t.Id);
            var sessionCount = await _dbContext.CandidateExamSessions.IgnoreQueryFilters().CountAsync(s => s.TenantId == t.Id);

            tenantViewModels.Add(new TenantManagementViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Identifier = t.Identifier,
                Plan = t.Plan,
                MaxCandidateSlots = t.MaxCandidateSlots,
                BrandColorHex = t.BrandColorHex ?? "#0284c7",
                IsActive = t.IsActive,
                CreatedAtUtc = t.CreatedAtUtc,
                TotalUsers = userCount,
                TotalExams = examCount,
                TotalSessions = sessionCount
            });
        }

        var requests = await _dbContext.SubscriptionRequests
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        var totalPlatformExams = await _dbContext.Exams.IgnoreQueryFilters().CountAsync();
        var totalPlatformSessions = await _dbContext.CandidateExamSessions.IgnoreQueryFilters().CountAsync();

        var viewModel = new PlatformAdminDashboardViewModel
        {
            TotalTenants = tenants.Count,
            ActiveTenants = tenants.Count(t => t.IsActive),
            TotalPlatformExams = totalPlatformExams,
            TotalPlatformSessions = totalPlatformSessions,
            PendingRequestsCount = requests.Count(r => r.Status == RequestStatus.Pending),
            Tenants = tenantViewModels,
            SubscriptionRequests = requests
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTenant(CreateTenantInputDto input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Validation failed. Please verify the tenant form inputs.";
            return RedirectToAction(nameof(Index));
        }

        var cleanIdentifier = input.Identifier.Trim().ToLowerInvariant();
        if (await _dbContext.Tenants.AnyAsync(t => t.Identifier.ToLower() == cleanIdentifier))
        {
            TempData["ErrorMessage"] = $"A tenant with identifier '{cleanIdentifier}' already exists.";
            return RedirectToAction(nameof(Index));
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = input.Name.Trim(),
            Identifier = cleanIdentifier,
            Plan = input.Plan,
            MaxCandidateSlots = input.MaxCandidateSlots,
            BrandColorHex = string.IsNullOrWhiteSpace(input.BrandColorHex) ? "#4f46e5" : input.BrandColorHex,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        // Create initial TenantAdmin
        var adminUser = new ApplicationUser
        {
            UserName = input.AdminEmail.Trim(),
            Email = input.AdminEmail.Trim(),
            EmailConfirmed = true,
            FirstName = input.AdminFirstName.Trim(),
            LastName = input.AdminLastName.Trim(),
            UserCode = "ADM-001",
            TenantId = tenant.Id,
            IsActive = true
        };

        var pwd = string.IsNullOrWhiteSpace(input.DefaultPassword) ? "P@ssword123!" : input.DefaultPassword;
        var createResult = await _userManager.CreateAsync(adminUser, pwd);
        if (createResult.Succeeded)
        {
            await _userManager.AddToRoleAsync(adminUser, "TenantAdmin");
            TempData["SuccessMessage"] = $"Tenant '{tenant.Name}' and Admin '{adminUser.Email}' created successfully!";
        }
        else
        {
            var err = string.Join("; ", createResult.Errors.Select(e => e.Description));
            TempData["WarningMessage"] = $"Tenant was created, but admin creation had warnings: {err}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSubscription(UpdateTenantSubscriptionDto input)
    {
        var tenant = await _dbContext.Tenants.FindAsync(input.TenantId);
        if (tenant == null)
        {
            TempData["ErrorMessage"] = "Tenant not found.";
            return RedirectToAction(nameof(Index));
        }

        tenant.Plan = input.Plan;
        tenant.MaxCandidateSlots = input.MaxCandidateSlots;
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Tenant '{tenant.Name}' subscription updated to {tenant.Plan} with {tenant.MaxCandidateSlots} slots.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(Guid tenantId)
    {
        var tenant = await _dbContext.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            TempData["ErrorMessage"] = "Tenant not found.";
            return RedirectToAction(nameof(Index));
        }

        tenant.IsActive = !tenant.IsActive;
        tenant.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var state = tenant.IsActive ? "Activated" : "Suspended";
        TempData["SuccessMessage"] = $"Tenant '{tenant.Name}' has been {state}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTenant(Guid tenantId)
    {
        var tenant = await _dbContext.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            TempData["ErrorMessage"] = "Tenant not found.";
            return RedirectToAction(nameof(Index));
        }

        var tenantName = tenant.Name;
        _dbContext.Tenants.Remove(tenant);
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Tenant '{tenantName}' has been removed from the platform.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRequest(Guid requestId)
    {
        var request = await _dbContext.SubscriptionRequests.FindAsync(requestId);
        if (request == null)
        {
            TempData["ErrorMessage"] = "Subscription request not found.";
            return RedirectToAction(nameof(Index));
        }

        if (request.Status != RequestStatus.Pending)
        {
            TempData["ErrorMessage"] = "This request has already been processed.";
            return RedirectToAction(nameof(Index));
        }

        // Generate clean unique slug
        var baseSlug = !string.IsNullOrWhiteSpace(request.SuggestedIdentifier)
            ? request.SuggestedIdentifier
            : Regex.Replace(request.OrganizationName.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "org";

        var uniqueSlug = baseSlug;
        int counter = 1;
        while (await _dbContext.Tenants.AnyAsync(t => t.Identifier.ToLower() == uniqueSlug))
        {
            uniqueSlug = $"{baseSlug}-{counter++}";
        }

        // Determine candidate capacity from plan
        int slots = request.EstimatedCandidates > 0 ? request.EstimatedCandidates : request.RequestedPlan switch
        {
            TenantPlan.Free => 50,
            TenantPlan.Professional => 1000,
            TenantPlan.Enterprise => 5000,
            _ => 500
        };

        // Create Tenant
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.OrganizationName.Trim(),
            Identifier = uniqueSlug,
            Plan = request.RequestedPlan,
            MaxCandidateSlots = slots,
            BrandColorHex = "#0284c7",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.Tenants.AddAsync(tenant);

        // Provision TenantAdmin
        var names = request.ContactName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = names.Length > 0 ? names[0] : "Admin";
        var lastName = names.Length > 1 ? names[1] : "User";

        var adminUser = new ApplicationUser
        {
            UserName = request.ContactEmail.Trim(),
            Email = request.ContactEmail.Trim(),
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            UserCode = "ADM-001",
            TenantId = tenant.Id,
            IsActive = true
        };

        const string initialPassword = "P@ssword123!";
        var userResult = await _userManager.CreateAsync(adminUser, initialPassword);
        if (userResult.Succeeded)
        {
            await _userManager.AddToRoleAsync(adminUser, "TenantAdmin");
        }

        // Update Request state
        request.Status = RequestStatus.Approved;
        request.ProcessedAtUtc = DateTime.UtcNow;
        request.CreatedTenantId = tenant.Id;
        request.ProcessedByUserId = _currentUserService.UserId;
        request.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Approved! Provisioned tenant '{tenant.Name}' ({tenant.Identifier}) with admin account '{adminUser.Email}' (Password: {initialPassword}).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequest(Guid requestId, string? rejectionReason)
    {
        var request = await _dbContext.SubscriptionRequests.FindAsync(requestId);
        if (request == null)
        {
            TempData["ErrorMessage"] = "Subscription request not found.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = RequestStatus.Rejected;
        request.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? "Plan request declined by platform administration." : rejectionReason.Trim();
        request.ProcessedAtUtc = DateTime.UtcNow;
        request.ProcessedByUserId = _currentUserService.UserId;
        request.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Request from '{request.OrganizationName}' marked as Rejected.";
        return RedirectToAction(nameof(Index));
    }
}
