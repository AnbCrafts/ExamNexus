using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamNexus.Application.DTOs;
using ExamNexus.Application.Interfaces;
using ExamNexus.Domain.Entities;
using ExamNexus.Infrastructure.Data;

namespace ExamNexus.Web.Controllers.Api;

[ApiController]
[Route("api/v1/auth")]
[IgnoreAntiforgeryToken]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IJwtTokenService jwtTokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    [HttpPost("token")]
    public async Task<IActionResult> GenerateToken([FromBody] LoginRequestDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Identifier == model.TenantIdentifier.Trim().ToLowerInvariant() && t.IsActive);

        if (tenant == null)
            return Unauthorized(new { message = "Organization tenant not found." });

        var user = await _userManager.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => (u.Email == model.UsernameOrEmail || u.UserName == model.UsernameOrEmail) &&
                                      (u.TenantId == tenant.Id || u.TenantId == null));

        if (user == null || !user.IsActive)
            return Unauthorized(new { message = "Invalid credentials." });

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!isPasswordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, refreshToken, expiresAt) = await _jwtTokenService.GenerateTokensAsync(user, roles);

        return Ok(new LoginResponseDto
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            FullName = user.FullName,
            TenantIdentifier = tenant.Identifier,
            Roles = roles
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto model)
    {
        try
        {
            var (accessToken, refreshToken, expiresAt) = await _jwtTokenService.RefreshTokenAsync(model.AccessToken, model.RefreshToken);
            return Ok(new { success = true, accessToken, refreshToken, expiresAt });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token refresh error");
            return Unauthorized(new { success = false, message = ex.Message });
        }
    }
}
