using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamNexus.Application.DTOs;
using ExamNexus.Application.Interfaces;
using ExamNexus.Domain.Entities;
using ExamNexus.Infrastructure.Data;

namespace ExamNexus.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantService _tenantService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ITenantService tenantService,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _dbContext = dbContext;
        _tenantService = tenantService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null, string? role = null)
    {
        // 1. If already authenticated:
        if (User.Identity?.IsAuthenticated == true)
        {
            // If the user requested a specific role, verify whether they already have that role or wish to switch
            if (!string.IsNullOrEmpty(role))
            {
                var targetRole = role.Trim().ToLowerInvariant();
                var currentIsSuperAdmin = User.IsInRole("SystemAdmin") || User.FindFirst("IsSystemAdmin")?.Value == "true";
                var currentIsProctor = User.IsInRole("Proctor");
                var currentIsInstructor = User.IsInRole("Instructor");

                if (targetRole == "superadmin" && currentIsSuperAdmin)
                {
                    return RedirectToAction("Index", "Admin");
                }
                if (targetRole == "proctor" && currentIsProctor)
                {
                    return RedirectToAction("Index", "Proctor");
                }
                if (targetRole == "instructor" && currentIsInstructor)
                {
                    return RedirectToAction("Index", "Exam");
                }

                // If user is logged in as another role, sign out first to allow switching roles cleanly
                await _signInManager.SignOutAsync();
                HttpContext.Session.Clear();
                return RedirectToAction("Login", new { role, returnUrl });
            }

            // Normal redirection for authenticated user
            if (User.IsInRole("SystemAdmin") || User.FindFirst("IsSystemAdmin")?.Value == "true")
            {
                return RedirectToAction("Index", "Admin");
            }
            if (User.IsInRole("Proctor"))
            {
                return RedirectToAction("Index", "Proctor");
            }
            return RedirectToAction("Index", "Dashboard");
        }

        ViewBag.Tenants = await _dbContext.Tenants.Where(t => t.IsActive).ToListAsync();
        ViewData["ReturnUrl"] = returnUrl;
        ViewBag.SelectedRole = role?.ToLowerInvariant();

        var defaultModel = new LoginRequestDto
        {
            TenantIdentifier = _tenantService.CurrentTenantIdentifier ?? "acme-corp",
            ReturnUrl = returnUrl
        };

        // Pre-fill test credentials based on selected role
        if (!string.IsNullOrEmpty(role))
        {
            switch (role.ToLowerInvariant())
            {
                case "superadmin":
                    defaultModel.TenantIdentifier = "acme-corp"; // dropdown default, handled dynamically
                    defaultModel.UsernameOrEmail = "superadmin@examnexus.com";
                    defaultModel.Password = "P@ssword123!";
                    break;
                case "instructor":
                    defaultModel.TenantIdentifier = "acme-corp";
                    defaultModel.UsernameOrEmail = "instructor@acme.com";
                    defaultModel.Password = "P@ssword123!";
                    break;
                case "proctor":
                    defaultModel.TenantIdentifier = "acme-corp";
                    defaultModel.UsernameOrEmail = "proctor@acme.com";
                    defaultModel.Password = "P@ssword123!";
                    break;
                case "admin":
                    defaultModel.TenantIdentifier = "acme-corp";
                    defaultModel.UsernameOrEmail = "admin@acme.com";
                    defaultModel.Password = "P@ssword123!";
                    break;
                case "candidate":
                    defaultModel.TenantIdentifier = "acme-corp";
                    defaultModel.UsernameOrEmail = "student@acme.com";
                    defaultModel.Password = "P@ssword123!";
                    break;
            }
        }

        return View(defaultModel);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequestDto model)
    {
        ViewBag.Tenants = await _dbContext.Tenants.Where(t => t.IsActive).ToListAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 1. Check if user is a Global SuperAdmin (TenantId == null, Role: SystemAdmin)
        var inputIdentifier = (model.UsernameOrEmail ?? string.Empty).Trim();
        var user = await _userManager.FindByEmailAsync(inputIdentifier)
                   ?? await _userManager.FindByNameAsync(inputIdentifier);

        if (user != null && !user.TenantId.HasValue && user.IsActive)
        {
            var isSysAdmin = await _userManager.IsInRoleAsync(user, "SystemAdmin");
            if (isSysAdmin)
            {
                var sysResult = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
                if (sysResult.Succeeded)
                {
                    await _userManager.ResetAccessFailedCountAsync(user);
                    var sysClaims = new List<Claim>
                    {
                        new("FullName", user.FullName),
                        new("UserCode", user.UserCode),
                        new("IsSystemAdmin", "true")
                    };
                    await _signInManager.SignInWithClaimsAsync(user, model.RememberMe, sysClaims);
                    _logger.LogInformation("Global SuperAdmin {Email} logged into ExamNexus Platform", user.Email);
                    return RedirectToAction("Index", "Admin");
                }
                else if (sysResult.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "SuperAdmin account locked due to consecutive failed attempts. Retry in 15 minutes.");
                    return View(model);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid SuperAdmin credentials.");
                    return View(model);
                }
            }
        }

        // 2. Resolve Tenant for Organization Users
        var tenantSlug = (model.TenantIdentifier ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(tenantSlug))
        {
            ModelState.AddModelError("TenantIdentifier", "Please select an organization tenant.");
            return View(model);
        }

        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Identifier.ToLower() == tenantSlug && t.IsActive);

        if (tenant == null)
        {
            ModelState.AddModelError("TenantIdentifier", "Organization tenant not found or inactive.");
            return View(model);
        }

        // 3. Resolve User within Tenant
        if (user == null || !user.IsActive || (user.TenantId.HasValue && user.TenantId.Value != tenant.Id))
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials or account does not belong to this organization.");
            return View(model);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            model.Password,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _userManager.ResetAccessFailedCountAsync(user);

            // Store Tenant Claims in the Cookie
            var claims = new List<Claim>
            {
                new("TenantId", tenant.Id.ToString()),
                new("TenantIdentifier", tenant.Identifier),
                new("TenantName", tenant.Name),
                new("FullName", user.FullName),
                new("UserCode", user.UserCode)
            };

            await _signInManager.SignInWithClaimsAsync(user, model.RememberMe, claims);

            Response.Cookies.Append(".ExamNexus.Tenant", tenant.Identifier, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            _logger.LogInformation("User {Email} logged into tenant {Tenant}", user.Email, tenant.Identifier);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) && model.ReturnUrl != "/" && !model.ReturnUrl.Contains("/Account/Login"))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account locked due to consecutive failed attempts. Please retry in 15 minutes.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid username or password.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
