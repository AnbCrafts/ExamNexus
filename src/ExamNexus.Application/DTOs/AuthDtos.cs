using System.ComponentModel.DataAnnotations;

namespace ExamNexus.Application.DTOs;

public class LoginRequestDto
{
    [Required]
    [Display(Name = "Tenant Organization")]
    public string TenantIdentifier { get; set; } = string.Empty; // e.g. "acme-corp"

    [Required]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = false;
    public string? ReturnUrl { get; set; }
}

public class LoginResponseDto
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? UserId { get; set; }
    public string? FullName { get; set; }
    public string? TenantIdentifier { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public string? ErrorMessage { get; set; }
}

public class RefreshTokenRequestDto
{
    [Required] public string AccessToken { get; set; } = string.Empty;
    [Required] public string RefreshToken { get; set; } = string.Empty;
}
