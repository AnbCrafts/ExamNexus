using ExamNexus.Domain.Common;

namespace ExamNexus.Domain.Entities;

public class RefreshToken : BaseTenantEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string JwtId { get; set; } = string.Empty;
    public bool IsUsed { get; set; } = false;
    public bool IsRevoked { get; set; } = false;
    public DateTime ExpiresAtUtc { get; set; }
    public string? ReplacedByToken { get; set; }

    public bool IsActive => !IsRevoked && !IsUsed && DateTime.UtcNow < ExpiresAtUtc;

    // Navigation
    public virtual ApplicationUser? User { get; set; }
}
