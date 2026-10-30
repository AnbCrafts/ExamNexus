using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ExamNexus.Application.Interfaces;
using ExamNexus.Domain.Common;
using ExamNexus.Domain.Entities;

namespace ExamNexus.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>, IApplicationDbContext
{
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService? _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantService tenantService,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _tenantService = tenantService;
        _currentUserService = currentUserService;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<QuestionBank> QuestionBanks => Set<QuestionBank>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<CandidateExamSession> CandidateExamSessions => Set<CandidateExamSession>();
    public DbSet<CandidateAnswer> CandidateAnswers => Set<CandidateAnswer>();
    public DbSet<ProctorEvent> ProctorEvents => Set<ProctorEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SubscriptionRequest> SubscriptionRequests => Set<SubscriptionRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Schema
        builder.HasDefaultSchema("ExamSaaS");

        // Identity Table Names
        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("AspNetUsers");
            b.Property(u => u.UserCode).HasMaxLength(30).IsRequired();
            b.Property(u => u.FirstName).HasMaxLength(50).IsRequired();
            b.Property(u => u.LastName).HasMaxLength(50).IsRequired();

            b.HasOne(u => u.Tenant)
             .WithMany(t => t.Users)
             .HasForeignKey(u => u.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("AspNetRoles");
            b.Property(r => r.Description).HasMaxLength(250);
        });

        builder.Entity<Tenant>(b =>
        {
            b.ToTable("Tenants");
            b.HasKey(t => t.Id);
            b.HasIndex(t => t.Identifier).IsUnique();
            b.Property(t => t.Identifier).HasMaxLength(50).IsRequired();
            b.Property(t => t.Name).HasMaxLength(150).IsRequired();
            b.Property(t => t.BrandColorHex).HasMaxLength(10);
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("RefreshTokens");
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.Token).IsUnique();
            b.Property(r => r.Token).HasMaxLength(256).IsRequired();
            b.Property(r => r.JwtId).HasMaxLength(128).IsRequired();
        });

        builder.Entity<Course>(b =>
        {
            b.ToTable("Courses");
            b.HasKey(c => c.Id);
            b.Property(c => c.Code).HasMaxLength(30).IsRequired();
            b.Property(c => c.Title).HasMaxLength(150).IsRequired();

            b.HasOne(c => c.CreatedByInstructor)
             .WithMany()
             .HasForeignKey(c => c.CreatedByInstructorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Question>(b =>
        {
            b.ToTable("Questions");
            b.HasKey(q => q.Id);
            b.Property(q => q.Points).HasPrecision(5, 2);
        });

        builder.Entity<QuestionOption>(b =>
        {
            b.ToTable("QuestionOptions");
            b.HasKey(o => o.Id);

            b.HasOne(o => o.Question)
             .WithMany(q => q.Options)
             .HasForeignKey(o => o.QuestionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Exam>(b =>
        {
            b.ToTable("Exams");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.AccessCode);
            b.Property(e => e.AccessCode).HasMaxLength(20).IsRequired();
            b.Property(e => e.Title).HasMaxLength(200).IsRequired();
            b.Property(e => e.PassingScorePercent).HasPrecision(5, 2);
            b.Property(e => e.TotalMarks).HasPrecision(5, 2);
        });

        builder.Entity<CandidateExamSession>(b =>
        {
            b.ToTable("CandidateExamSessions");
            b.HasKey(s => s.Id);
            b.Property(s => s.FinalScore).HasPrecision(5, 2);
            b.Property(s => s.Percentage).HasPrecision(5, 2);
            b.Property(s => s.FlagReason).HasMaxLength(250);

            b.HasOne(s => s.Candidate)
             .WithMany(u => u.ExamSessions)
             .HasForeignKey(s => s.CandidateId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CandidateAnswer>(b =>
        {
            b.ToTable("CandidateAnswers");
            b.HasKey(a => a.Id);
            b.Property(a => a.AwardedMarks).HasPrecision(5, 2);
        });

        builder.Entity<ProctorEvent>(b =>
        {
            b.ToTable("ProctorEvents");
            b.HasKey(p => p.Id);
            b.Property(p => p.EventType).HasMaxLength(50).IsRequired();
            b.Property(p => p.Details).HasMaxLength(500);
        });

        builder.Entity<SubscriptionRequest>(b =>
        {
            b.ToTable("SubscriptionRequests");
            b.HasKey(s => s.Id);
            b.Property(s => s.OrganizationName).HasMaxLength(150).IsRequired();
            b.Property(s => s.ContactName).HasMaxLength(100).IsRequired();
            b.Property(s => s.ContactEmail).HasMaxLength(150).IsRequired();
            b.Property(s => s.ContactPhone).HasMaxLength(30);
            b.Property(s => s.SuggestedIdentifier).HasMaxLength(50);
            b.Property(s => s.RejectionReason).HasMaxLength(500);

            b.HasOne(s => s.CreatedTenant)
             .WithMany()
             .HasForeignKey(s => s.CreatedTenantId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ====================================================================
        // MULTI-TENANT GLOBAL QUERY FILTERS
        // Every database read automatically isolates data by TenantId!
        // ====================================================================
        builder.Entity<Course>().HasQueryFilter(e => !_tenantService.CurrentTenantId.HasValue || e.TenantId == _tenantService.CurrentTenantId.Value);
        builder.Entity<QuestionBank>().HasQueryFilter(e => !_tenantService.CurrentTenantId.HasValue || e.TenantId == _tenantService.CurrentTenantId.Value);
        builder.Entity<Question>().HasQueryFilter(e => !_tenantService.CurrentTenantId.HasValue || e.TenantId == _tenantService.CurrentTenantId.Value);
        builder.Entity<Exam>().HasQueryFilter(e => !_tenantService.CurrentTenantId.HasValue || e.TenantId == _tenantService.CurrentTenantId.Value);
        builder.Entity<ExamQuestion>().HasQueryFilter(e => !_tenantService.CurrentTenantId.HasValue || e.TenantId == _tenantService.CurrentTenantId.Value);
        builder.Entity<CandidateExamSession>().HasQueryFilter(e => !_tenantService.CurrentTenantId.HasValue || e.TenantId == _tenantService.CurrentTenantId.Value);
        builder.Entity<CandidateAnswer>().HasQueryFilter(e => !_tenantService.CurrentTenantId.HasValue || e.TenantId == _tenantService.CurrentTenantId.Value);
        builder.Entity<ProctorEvent>().HasQueryFilter(e => !_tenantService.CurrentTenantId.HasValue || e.TenantId == _tenantService.CurrentTenantId.Value);
        builder.Entity<RefreshToken>().HasQueryFilter(e => !_tenantService.CurrentTenantId.HasValue || e.TenantId == _tenantService.CurrentTenantId.Value);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Auto-assign TenantId to new tenant-scoped entities
        if (_tenantService.CurrentTenantId.HasValue)
        {
            foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
            {
                if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Entity.TenantId = _tenantService.CurrentTenantId.Value;
                }
            }
        }

        // 2. Automated Timestamp tracking
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        // 3. Automated ChangeTracker Audit Trail
        var auditEntries = CaptureAuditLogs();
        var result = await base.SaveChangesAsync(cancellationToken);
        if (auditEntries.Count > 0)
        {
            await PersistAuditLogs(auditEntries, cancellationToken);
        }
        return result;
    }

    private List<AuditEntry> CaptureAuditLogs()
    {
        ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditEntry>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.Entity is RefreshToken || entry.Entity is ProctorEvent ||
                entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntry(entry)
            {
                TenantId = _tenantService.CurrentTenantId,
                EntityName = entry.Entity.GetType().Name,
                UserId = _currentUserService?.UserId,
                UserEmail = _currentUserService?.UserEmail
            };

            auditEntries.Add(auditEntry);

            foreach (var property in entry.Properties)
            {
                if (property.IsTemporary)
                {
                    auditEntry.TemporaryProperties.Add(property);
                    continue;
                }

                string propertyName = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[propertyName] = property.CurrentValue;
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.Action = "CREATE";
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        auditEntry.Action = "DELETE";
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        break;
                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.Action = "UPDATE";
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }
        }

        foreach (var auditEntry in auditEntries.Where(e => !e.HasTemporaryProperties))
        {
            AuditLogs.Add(auditEntry.ToAuditLog());
        }

        return auditEntries.Where(e => e.HasTemporaryProperties).ToList();
    }

    private async Task PersistAuditLogs(List<AuditEntry> auditEntries, CancellationToken cancellationToken)
    {
        foreach (var auditEntry in auditEntries)
        {
            foreach (var prop in auditEntry.TemporaryProperties)
            {
                if (prop.Metadata.IsPrimaryKey())
                    auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                else
                    auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
            }
            AuditLogs.Add(auditEntry.ToAuditLog());
        }

        await base.SaveChangesAsync(cancellationToken);
    }
}

internal class AuditEntry
{
    public AuditEntry(EntityEntry entry) { Entry = entry; }
    public EntityEntry Entry { get; }
    public Guid? TenantId { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, object?> KeyValues { get; } = new();
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();
    public List<PropertyEntry> TemporaryProperties { get; } = new();
    public bool HasTemporaryProperties => TemporaryProperties.Count > 0;

    public AuditLog ToAuditLog() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        UserId = UserId,
        UserEmail = UserEmail,
        EntityName = EntityName,
        Action = Action,
        TimestampUtc = DateTime.UtcNow,
        EntityId = JsonSerializer.Serialize(KeyValues),
        OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues),
        NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues)
    };
}
