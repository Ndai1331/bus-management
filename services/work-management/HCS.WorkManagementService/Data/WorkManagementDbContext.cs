using HCS.WorkManagementService.Domain;
using HCS.WorkManagementService.Integration;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace HCS.WorkManagementService.Data;

public sealed class WorkManagementDbContext(DbContextOptions<WorkManagementDbContext> options)
    : DbContext(options)
{
    public const string ConnectionStringName = "WorkManagement";
    public const string Schema = "hcs_work";

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<ProjectTaskAssignment> ProjectTaskAssignments => Set<ProjectTaskAssignment>();
    public DbSet<ProjectTaskDocument> ProjectTaskDocuments => Set<ProjectTaskDocument>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<CalendarEventParticipant> CalendarEventParticipants => Set<CalendarEventParticipant>();
    public DbSet<SurveyCriteria> SurveyCriteria => Set<SurveyCriteria>();
    public DbSet<SurveyLocation> SurveyLocations => Set<SurveyLocation>();
    public DbSet<SurveySession> SurveySessions => Set<SurveySession>();
    public DbSet<SurveyResult> SurveyResults => Set<SurveyResult>();
    public DbSet<SurveyFileReference> SurveyFiles => Set<SurveyFileReference>();
    public DbSet<DashboardMetric> DashboardMetrics => Set<DashboardMetric>();
    public DbSet<ReportReadModel> ReportReadModels => Set<ReportReadModel>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<Project>(b =>
        {
            b.ToTable("Projects"); b.ConfigureByConvention();
            b.Property(x => x.Code).HasMaxLength(WorkConsts.CodeLength).IsRequired();
            b.Property(x => x.Name).HasMaxLength(WorkConsts.NameLength).IsRequired();
            b.Property(x => x.Status).HasMaxLength(WorkConsts.StatusLength).IsRequired();
            b.HasIndex(x => x.Code).IsUnique(); b.HasIndex(x => x.OwnerDepartmentId); b.HasIndex(x => x.OwnerUserId);
        });
        builder.Entity<ProjectMember>(b =>
        {
            b.ToTable("ProjectMembers"); b.ConfigureByConvention();
            b.Property(x => x.Role).HasMaxLength(WorkConsts.TypeLength).IsRequired();
            b.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique(); b.HasIndex(x => x.UserId);
            b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ProjectTask>(b =>
        {
            b.ToTable("ProjectTasks"); b.ConfigureByConvention();
            b.Property(x => x.Code).HasMaxLength(WorkConsts.CodeLength).IsRequired();
            b.Property(x => x.Title).HasMaxLength(WorkConsts.NameLength).IsRequired();
            b.Property(x => x.Priority).HasMaxLength(WorkConsts.StatusLength).IsRequired();
            b.Property(x => x.Status).HasMaxLength(WorkConsts.StatusLength).IsRequired();
            b.HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();
            b.HasIndex(x => x.ParentTaskId); b.HasIndex(x => new { x.Status, x.DueDate });
            b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ProjectTaskAssignment>(b =>
        {
            b.ToTable("ProjectTaskAssignments"); b.ConfigureByConvention();
            b.Property(x => x.AssignmentType).HasMaxLength(WorkConsts.TypeLength).IsRequired();
            b.HasIndex(x => new { x.ProjectTaskId, x.UserId, x.AssignmentType }).IsUnique(); b.HasIndex(x => x.UserId);
            b.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.ProjectTaskId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ProjectTaskDocument>(b =>
        {
            b.ToTable("ProjectTaskDocuments"); b.ConfigureByConvention();
            b.Property(x => x.DocumentCode).HasMaxLength(WorkConsts.CodeLength);
            b.HasIndex(x => new { x.ProjectTaskId, x.DocumentId }).IsUnique(); b.HasIndex(x => x.DocumentId);
            b.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.ProjectTaskId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<CalendarEvent>(b =>
        {
            b.ToTable("CalendarEvents"); b.ConfigureByConvention();
            b.Property(x => x.Title).HasMaxLength(WorkConsts.NameLength).IsRequired();
            b.Property(x => x.EventType).HasMaxLength(WorkConsts.TypeLength).IsRequired();
            b.Property(x => x.RelatedType).HasMaxLength(WorkConsts.TypeLength).IsRequired();
            b.Property(x => x.RelatedId).HasMaxLength(128);
            b.Property(x => x.Visibility).HasMaxLength(WorkConsts.StatusLength).IsRequired();
            b.HasIndex(x => new { x.StartTime, x.EndTime });
            b.HasIndex(x => x.OwnerUserId);
            b.HasIndex(x => new { x.RelatedType, x.RelatedId, x.EventType })
                .IsUnique()
                .HasFilter("\"RelatedId\" IS NOT NULL AND \"EventType\" IN ('PROJECT', 'TASK')")
                .HasDatabaseName("IX_CalendarEvents_SyncedRelated");
        });
        builder.Entity<CalendarEventParticipant>(b =>
        {
            b.ToTable("CalendarEventParticipants"); b.ConfigureByConvention();
            b.HasIndex(x => new { x.CalendarEventId, x.UserId }).IsUnique(); b.HasIndex(x => x.UserId);
            b.HasOne<CalendarEvent>().WithMany().HasForeignKey(x => x.CalendarEventId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<SurveyCriteria>(b =>
        {
            ConfigureCatalog(b, "SurveyCriteria");
            b.Property(x => x.Image).HasMaxLength(512);
            b.HasIndex(x => x.LocationId);
            b.HasOne<SurveyLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<SurveyLocation>(b =>
        {
            ConfigureCatalog(b, "SurveyLocations");
            b.Property(x => x.Description).HasMaxLength(1000);
            b.HasIndex(x => x.OrganizationUnitId);
        });
        builder.Entity<SurveySession>(b =>
        {
            b.ToTable("SurveySessions"); b.ConfigureByConvention();
            b.Property(x => x.Code).HasMaxLength(WorkConsts.CodeLength).IsRequired();
            b.Property(x => x.Name).HasMaxLength(WorkConsts.NameLength).IsRequired();
            b.Property(x => x.Status).HasMaxLength(WorkConsts.StatusLength).IsRequired();
            b.Property(x => x.FullName).HasMaxLength(WorkConsts.NameLength);
            b.Property(x => x.PhoneNumber).HasMaxLength(64);
            b.Property(x => x.PatientCode).HasMaxLength(64);
            b.Property(x => x.DeviceType).HasMaxLength(WorkConsts.TypeLength);
            b.Property(x => x.Note).HasMaxLength(2000);
            b.Property(x => x.SessionDisplay).HasMaxLength(WorkConsts.NameLength);
            b.HasIndex(x => x.Code).IsUnique(); b.HasIndex(x => new { x.Status, x.StartsAt });
            b.HasIndex(x => x.OwnerUserId); b.HasIndex(x => x.IsPublic);
            b.HasOne<SurveyLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<SurveyResult>(b =>
        {
            b.ToTable("SurveyResults"); b.ConfigureByConvention();
            b.Property(x => x.Score).HasPrecision(7, 2);
            b.HasIndex(x => new { x.SessionId, x.CriteriaId, x.RespondentUserId });
            b.HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<SurveyCriteria>().WithMany().HasForeignKey(x => x.CriteriaId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<SurveyFileReference>(b =>
        {
            b.ToTable("SurveyFiles"); b.ConfigureByConvention();
            b.Property(x => x.BlobName).HasMaxLength(512).IsRequired();
            b.Property(x => x.FileName).HasMaxLength(256).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            b.HasIndex(x => x.BlobName).IsUnique(); b.HasIndex(x => x.SessionId); b.HasIndex(x => x.UploadedByUserId);
            b.HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<DashboardMetric>(b =>
        {
            b.ToTable("DashboardMetrics"); b.ConfigureByConvention();
            b.Property(x => x.Key).HasMaxLength(128).IsRequired(); b.HasIndex(x => x.Key).IsUnique();
        });
        builder.Entity<ReportReadModel>(b =>
        {
            b.ToTable("ReportReadModels"); b.ConfigureByConvention();
            b.Property(x => x.Dimension).HasMaxLength(128).IsRequired();
            b.Property(x => x.Key).HasMaxLength(128).IsRequired();
            b.Property(x => x.Label).HasMaxLength(256).IsRequired();
            b.HasIndex(x => new { x.Dimension, x.Key }).IsUnique();
        });
        builder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages"); b.HasKey(x => x.Id);
            b.Property(x => x.EventName).HasMaxLength(256).IsRequired();
            b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
            b.Property(x => x.LastError).HasMaxLength(1000); b.HasIndex(x => new { x.PublishedAt, x.DeadLetteredAt, x.LeaseUntil, x.CreationTime });
        });
        builder.Entity<InboxMessage>(b =>
        {
            b.ToTable("InboxMessages"); b.HasKey(x => new { x.EventId, x.Handler });
            b.Property(x => x.Handler).HasMaxLength(256); b.HasIndex(x => x.ProcessedAt);
        });
    }

    private static void ConfigureCatalog<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> b,
        string table) where TEntity : class
    {
        b.ToTable(table); b.ConfigureByConvention();
        b.Property("Code").HasMaxLength(WorkConsts.CodeLength).IsRequired();
        b.Property("Name").HasMaxLength(WorkConsts.NameLength).IsRequired();
        b.HasIndex("Code").IsUnique();
    }
}
