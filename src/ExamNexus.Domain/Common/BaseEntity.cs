namespace ExamNexus.Domain.Common;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

public abstract class BaseTenantEntity : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}
