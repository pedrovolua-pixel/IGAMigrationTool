using Microsoft.EntityFrameworkCore;

namespace AssessmentRuns;

internal sealed class SyntheticRunDbContext(DbContextOptions<SyntheticRunDbContext> options) : DbContext(options)
{
    internal DbSet<RunRow> Runs => Set<RunRow>();
    internal DbSet<PlanUnitRow> PlanUnits => Set<PlanUnitRow>();
    internal DbSet<ResultRow> Results => Set<ResultRow>();
    internal DbSet<AttemptRow> Attempts => Set<AttemptRow>();
    internal DbSet<OutboxRow> Outbox => Set<OutboxRow>();
    internal DbSet<InboxRow> Inbox => Set<InboxRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("synthetic_assessment");
        builder.Entity<RunRow>().ToTable("runs").HasKey(row => row.RunId);
        builder.Entity<PlanUnitRow>().ToTable("plan_units").HasKey(row => new { row.RunId, row.InventoryId, row.CategoryId });
        builder.Entity<ResultRow>().ToTable("results").HasKey(row => new { row.RunId, row.InventoryId, row.CategoryId });
        builder.Entity<AttemptRow>().ToTable("attempts").HasKey(row => new { row.RunId, row.InventoryId, row.CategoryId, row.AttemptNumber });
        builder.Entity<OutboxRow>().ToTable("outbox").HasKey(row => row.EventId);
        builder.Entity<InboxRow>().ToTable("inbox").HasKey(row => new { row.ConsumerId, row.EventId });
        builder.Entity<RunRow>().Property(row => row.Revision).IsConcurrencyToken();
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                var name = string.Concat(property.Name.Select((character, index) =>
                    index > 0 && char.IsUpper(character) ? "_" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));
                property.SetColumnName(name);
            }
        }
    }
}

internal sealed class RunRow
{
    public Guid RunId { get; set; }
    public string CustomerId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string EnvironmentId { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string BaselineCatalogId { get; set; } = "";
    public string ProfileCatalogId { get; set; } = "";
    public string InputDigest { get; set; } = "";
    public string VersionsJson { get; set; } = "";
    public string PlanJson { get; set; } = "";
    public int State { get; set; }
    public long Revision { get; set; }
    public bool CancelRequested { get; set; }
    public long CheckpointSequence { get; set; }
    public string? LeaseOwner { get; set; }
    public Guid? LeaseGeneration { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public string? SummaryJson { get; set; }
    public string ActiveWorkJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
internal sealed class PlanUnitRow
{
    public Guid RunId { get; set; }
    public string InventoryId { get; set; } = "";
    public string CategoryId { get; set; } = "";
}
internal sealed class ResultRow
{
    public Guid RunId { get; set; }
    public string InventoryId { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public int State { get; set; }
    public string? ReasonCode { get; set; }
    public string? ResponsibleStage { get; set; }
    public string? EvidenceReference { get; set; }
    public string ResultDigest { get; set; } = "";
}
internal sealed class AttemptRow
{
    public Guid RunId { get; set; }
    public string InventoryId { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public int AttemptNumber { get; set; }
    public int Outcome { get; set; }
    public string? ReasonCode { get; set; }
    public Guid LeaseGeneration { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
internal sealed class OutboxRow
{
    public Guid EventId { get; set; }
    public Guid RunId { get; set; }
    public long RunRevision { get; set; }
    public string SchemaVersion { get; set; } = "";
    public string MinimumWorkerVersion { get; set; } = "";
    public string Kind { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
}
internal sealed class InboxRow
{
    public string ConsumerId { get; set; } = "";
    public Guid EventId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
