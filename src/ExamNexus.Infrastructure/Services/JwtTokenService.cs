using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ExamNexus.Application.Interfaces;
using ExamNexus.Domain.Entities;
using ExamNexus.Infrastructure.Data;

namespace ExamNexus.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public JwtTokenService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> GenerateTokensAsync(
        ApplicationUser user,
        IList<string> roles)
    {
        var secret = _configuration["JwtSettings:Secret"] ?? "ExamNexus_SuperSecretSaaSKey_2026_Minimum32CharsLong!@#$";
        var issuer = _configuration["JwtSettings:Issuer"] ?? "ExamNexus";
        var audience = _configuration["JwtSettings:Audience"] ?? "ExamNexus-ProctorApp";
        var expiryMinutes = int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("UserCode", user.UserCode),
            new("FullName", user.FullName),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim("TenantId", user.TenantId.Value.ToString()));
            if (user.Tenant != null)
            {
                claims.Add(new Claim("TenantIdentifier", user.Tenant.Identifier));
                claims.Add(new Claim("TenantName", user.Tenant.Name));
            }
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(securityToken);

        // Secure Refresh Token
        var refreshTokenString = GenerateRefreshTokenString();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId ?? Guid.Empty,
            UserId = user.Id,
            Token = refreshTokenString,
            JwtId = securityToken.Id,
            IsUsed = false,
            IsRevoked = false,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };

        await _context.RefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();

        return (accessToken, refreshTokenString, expiresAt);
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> RefreshTokenAsync(
        string token,
        string refreshToken)
    {
        var principal = GetPrincipalFromExpiredToken(token);
        if (principal == null)
            throw new SecurityTokenException("Invalid token.");

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (string.IsNullOrEmpty(userId))
            throw new SecurityTokenException("User identifier missing.");

        var existingRefreshToken = await _context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == refreshToken && r.UserId == userId);

        if (existingRefreshToken == null || existingRefreshToken.IsRevoked || existingRefreshToken.IsUsed || existingRefreshToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            throw new SecurityTokenException("Refresh token is invalid, expired, or compromised.");
        }

        var user = await _context.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsActive)
            throw new SecurityTokenException("User inactive.");

        existingRefreshToken.IsUsed = true;

        var userRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        var roles = await _context.Roles
            .Where(r => userRoleIds.Contains(r.Id))
            .Select(r => r.Name!)
            .ToListAsync();

        var result = await GenerateTokensAsync(user, roles);
        existingRefreshToken.ReplacedByToken = result.RefreshToken;
        await _context.SaveChangesAsync();

        return result;
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken, string userId)
    {
        var token = await _context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == refreshToken && r.UserId == userId);

        if (token == null || !token.IsActive)
            return false;

        token.IsRevoked = true;
        await _context.SaveChangesAsync();
        return true;
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var secret = _configuration["JwtSettings:Secret"] ?? "ExamNexus_SuperSecretSaaSKey_2026_Minimum32CharsLong!@#$";
        var issuer = _configuration["JwtSettings:Issuer"] ?? "ExamNexus";
        var audience = _configuration["JwtSettings:Audience"] ?? "ExamNexus-ProctorApp";

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = false
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    private static string GenerateRefreshTokenString()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
