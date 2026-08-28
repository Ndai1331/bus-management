using HCS.OrganizationService.Domain;
using HCS.OrganizationService.Integration;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace HCS.OrganizationService.Data;

[ConnectionStringName(ConnectionStringName)]
public sealed class OrganizationDbContext : AbpDbContext<OrganizationDbContext>
{
    public const string ConnectionStringName = "Organization";
    public const string Schema = "hcs_organization";

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<MasterDataItem> MasterDataItems => Set<MasterDataItem>();
    public DbSet<UserOrganizationMapping> UserOrganizationMappings => Set<UserOrganizationMapping>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        ConfigureCoded<Department>(builder, "Departments");
        ConfigureCoded<Unit>(builder, "Units");
        ConfigureCoded<Position>(builder, "Positions");
        ConfigureCoded<MasterDataItem>(builder, "MasterDataItems", uniqueCode: false);

        builder.Entity<Department>(b =>
        {
            b.Property(x => x.ParentId);
            b.HasIndex(x => x.ParentId);
            b.HasOne<Department>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Unit>(b =>
        {
            b.Property(x => x.DepartmentId).IsRequired();
            b.HasIndex(x => x.DepartmentId);
            b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Position>(b => b.Property(x => x.SignOrder).IsRequired());
        builder.Entity<MasterDataItem>(b =>
        {
            b.Property(x => x.Type).IsRequired().HasMaxLength(OrganizationConsts.MaxTypeLength);
            b.HasIndex(x => new { x.Type, x.Code }).IsUnique();
        });

        builder.Entity<UserOrganizationMapping>(b =>
        {
            b.ToTable("UserOrganizationMappings");
            b.ConfigureByConvention();
            b.Property(x => x.UserId).IsRequired();
            b.Property(x => x.DepartmentId).IsRequired();
            b.HasIndex(x => new { x.UserId, x.DepartmentId, x.UnitId, x.PositionId }).IsUnique();
            b.HasIndex(x => x.UserId).IsUnique()
                .HasFilter("\"IsPrimary\" = true");
            b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages"); b.HasKey(x => x.Id);
            b.Property(x => x.EventName).HasMaxLength(512);
            b.Property(x => x.CorrelationId).HasMaxLength(128);
            b.Property(x => x.Payload).HasColumnType("jsonb");
            b.Property(x => x.LastError).HasMaxLength(1000);
            b.HasIndex(x => new { x.PublishedAt, x.DeadLetteredAt, x.LeaseUntil, x.NextAttemptAt, x.CreationTime });
        });
        builder.Entity<InboxMessage>(b =>
        {
            b.ToTable("InboxMessages"); b.HasKey(x => new { x.EventId, x.Handler });
            b.Property(x => x.Handler).HasMaxLength(512);
        });
    }

    private static void ConfigureCoded<TEntity>(ModelBuilder builder, string table, bool uniqueCode = true)
        where TEntity : CodedAggregate
    {
        builder.Entity<TEntity>(b =>
        {
            b.ToTable(table);
            b.ConfigureByConvention();
            b.Property(x => x.Code).IsRequired().HasMaxLength(OrganizationConsts.MaxCodeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(OrganizationConsts.MaxNameLength);
            b.Property(x => x.SortOrder).IsRequired();
            if (uniqueCode) b.HasIndex(x => x.Code).IsUnique();
        });
    }
}
