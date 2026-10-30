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
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewBag.Tenants = await _dbContext.Tenants.Where(t => t.IsActive).ToListAsync();
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginRequestDto
        {
            TenantIdentifier = _tenantService.CurrentTenantIdentifier ?? "acme-corp",
            ReturnUrl = returnUrl
        });
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

        // 1. Resolve Tenant
        var tenantSlug = (model.TenantIdentifier ?? string.Empty).Trim().ToLowerInvariant();
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Identifier.ToLower() == tenantSlug && t.IsActive);

        if (tenant == null)
        {
            ModelState.AddModelError("TenantIdentifier", "Organization tenant not found or inactive.");
            return View(model);
        }

        // 2. Resolve User within Tenant (supports case-insensitive email or username)
        var inputIdentifier = (model.UsernameOrEmail ?? string.Empty).Trim();
        var user = await _userManager.FindByEmailAsync(inputIdentifier)
                   ?? await _userManager.FindByNameAsync(inputIdentifier);

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
