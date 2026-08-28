using HCS.DocumentService.Documents;
using HCS.DocumentService.Integration;
using HCS.DocumentService.Signing;
using HCS.DocumentService.Workflows;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;

namespace HCS.DocumentService;

[ConnectionStringName("DocumentService")]
public sealed class DocumentServiceDbContext(DbContextOptions<DocumentServiceDbContext> options) : DbContext(options)
{
    public DbSet<DocumentAggregate> Documents => Set<DocumentAggregate>();
    public DbSet<DocumentFile> DocumentFiles => Set<DocumentFile>();
    public DbSet<DocumentAssignment> DocumentAssignments => Set<DocumentAssignment>();
    public DbSet<DocumentHistory> DocumentHistories => Set<DocumentHistory>();
    public DbSet<WorkflowKind> WorkflowKinds => Set<WorkflowKind>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<ApprovalTask> ApprovalTasks => Set<ApprovalTask>();
    public DbSet<SigningCredential> SigningCredentials => Set<SigningCredential>();
    public DbSet<UserSignature> UserSignatures => Set<UserSignature>();
    public DbSet<SigningAttempt> SigningAttempts => Set<SigningAttempt>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("document");

        builder.Entity<DocumentAggregate>(b =>
        {
            b.ToTable("Documents"); b.HasKey(x => x.Id); b.Property(x => x.Number).HasMaxLength(64).IsRequired();
            b.Property(x => x.Title).HasMaxLength(256).IsRequired(); b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.Version).IsRowVersion(); b.HasIndex(x => x.Number).IsUnique();
            b.HasIndex(x => x.SourceType);
            b.HasIndex(x => x.ParentDocumentId);
            b.HasMany(x => x.Files).WithOne().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Assignments).WithOne().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Files).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Navigation(x => x.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<DocumentFile>(b =>
        {
            b.ToTable("DocumentFiles"); b.HasKey(x => x.Id);
            b.Property(x => x.FileName).HasMaxLength(256); b.Property(x => x.ContentType).HasMaxLength(128);
            b.Property(x => x.Sha256).HasMaxLength(64); b.Property(x => x.BlobName).HasMaxLength(512);
            b.HasIndex(x => new { x.DocumentId, x.Sha256 }).IsUnique();
        });
        builder.Entity<DocumentAssignment>(b =>
        {
            b.ToTable("DocumentAssignments"); b.HasKey(x => x.Id);
            b.Property(x => x.Responsibility).HasMaxLength(128);
            b.Property(x => x.StepCode).HasMaxLength(64);
            b.HasIndex(x => new { x.DocumentId, x.AssigneeUserId, x.Responsibility }).IsUnique();
            b.HasIndex(x => new { x.AssigneeUserId, x.IsCurrent, x.Responsibility, x.StepCode });
        });
        builder.Entity<DocumentHistory>(b =>
        {
            b.ToTable("DocumentHistories"); b.HasKey(x => x.Id); b.Property(x => x.Action).HasMaxLength(128); b.Property(x => x.Detail).HasMaxLength(2000);
            b.HasIndex(x => new { x.DocumentId, x.OccurredAt });
            b.HasIndex(x => new { x.ActorUserId, x.Action, x.DocumentId });
        });

        builder.Entity<WorkflowKind>(b =>
        {
            b.ToTable("WorkflowKinds"); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(64); b.Property(x => x.Name).HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.HasIndex(x => x.Code).IsUnique();
        });
        builder.Entity<WorkflowDefinition>(b =>
        {
            b.ToTable("WorkflowDefinitions"); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(64); b.Property(x => x.Name).HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.SignMode).HasMaxLength(16);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasOne<WorkflowKind>().WithMany().HasForeignKey(x => x.KindId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<WorkflowStep>(b =>
        {
            b.ToTable("WorkflowSteps"); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(64); b.Property(x => x.Name).HasMaxLength(256);
            b.Property(x => x.RequiredPermission).HasMaxLength(128);
            b.Property(x => x.Type).HasMaxLength(32);
            b.Property(x => x.AssigneeType).HasMaxLength(32);
            b.Property(x => x.UserIdsJson).HasColumnType("jsonb");
            b.Property(x => x.DepartmentIdsJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.DefinitionId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.DefinitionId, x.Order }).IsUnique();
        });
        builder.Entity<WorkflowTemplate>(b =>
        {
            b.ToTable("WorkflowTemplates"); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(64); b.Property(x => x.Name).HasMaxLength(256);
            b.Property(x => x.TemplateJson).HasColumnType("jsonb");
            b.Property(x => x.OutputFormat).HasMaxLength(16);
            b.Property(x => x.WordFileName).HasMaxLength(256);
            b.Property(x => x.WordContentType).HasMaxLength(128);
            b.Property(x => x.WordBlobName).HasMaxLength(512);
            b.Property(x => x.PdfFileName).HasMaxLength(256);
            b.Property(x => x.PdfContentType).HasMaxLength(128);
            b.Property(x => x.PdfBlobName).HasMaxLength(512);
            b.HasIndex(x => new { x.Code, x.Version }).IsUnique();
            b.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<WorkflowInstance>(b =>
        {
            b.ToTable("WorkflowInstances"); b.HasKey(x => x.Id);
            b.Property(x => x.IdempotencyKey).HasMaxLength(128);
            b.Property(x => x.ViewScopesJson).HasColumnType("jsonb");
            b.HasIndex(x => x.IdempotencyKey).IsUnique();
            b.HasIndex(x => new { x.Status, x.CreationTime });
            b.HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<ApprovalTask>(b =>
        {
            b.ToTable("ApprovalTasks"); b.HasKey(x => x.Id); b.Property(x => x.StepCode).HasMaxLength(64); b.Property(x => x.DecisionKey).HasMaxLength(128); b.Property(x => x.Comment).HasMaxLength(1000);
            b.HasIndex(x => x.DecisionKey).IsUnique().HasFilter("\"DecisionKey\" IS NOT NULL");
            b.HasIndex(x => new { x.AssigneeUserId, x.Status, x.InstanceId });
        });

        builder.Entity<SigningCredential>(b =>
        {
            b.ToTable("SigningCredentials"); b.HasKey(x => x.Id);
            b.Property(x => x.Endpoint).HasMaxLength(1024);
            b.Property(x => x.ProtectedSecret).HasMaxLength(4096);
            b.Property(x => x.ProviderCode).HasMaxLength(256);
            b.Property(x => x.LayoutImageBase64).HasMaxLength(4_000_000);
            b.HasIndex(x => new { x.UserId, x.Kind }).IsUnique();
        });
        builder.Entity<UserSignature>(b =>
        {
            b.ToTable("UserSignatures"); b.HasKey(x => x.Id);
            b.Property(x => x.FileName).HasMaxLength(256);
            b.Property(x => x.ContentType).HasMaxLength(128);
            b.Property(x => x.BlobName).HasMaxLength(512);
            b.Property(x => x.ProviderCode).HasMaxLength(256);
            b.Property(x => x.TokenRef).HasMaxLength(256);
            b.Property(x => x.ProtectedSecret).HasMaxLength(4096);
            b.Property(x => x.SealImageBase64).HasMaxLength(4_000_000);
            b.Property(x => x.Type).HasDefaultValue(UserSignatureType.Electronic);
            b.HasIndex(x => x.UserId);
        });
        builder.Entity<SigningAttempt>(b => { b.ToTable("SigningAttempts"); b.HasKey(x => x.Id); b.Property(x => x.InputSha256).HasMaxLength(64); b.Property(x => x.OutputSha256).HasMaxLength(64); b.Property(x => x.OutputBlobName).HasMaxLength(512); b.Property(x => x.IdempotencyKey).HasMaxLength(128); b.Property(x => x.Error).HasMaxLength(1000); b.HasIndex(x => new { x.UserId, x.DocumentId, x.FileId, x.Kind, x.IdempotencyKey }).IsUnique(); b.HasIndex(x => new { x.DocumentId, x.CreationTime }); });
        builder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages"); b.HasKey(x => x.Id);
            b.Property(x => x.EventName).HasMaxLength(512);
            b.Property(x => x.CorrelationId).HasMaxLength(128);
            b.Property(x => x.Payload).HasColumnType("jsonb");
            b.Property(x => x.LastError).HasMaxLength(1000);
            b.HasIndex(x => new { x.PublishedAt, x.DeadLetteredAt, x.LeaseUntil, x.NextAttemptAt, x.CreationTime });
        });
        builder.Entity<InboxMessage>(b => { b.ToTable("InboxMessages"); b.HasKey(x => new { x.EventId, x.Handler }); b.Property(x => x.Handler).HasMaxLength(512); });
    }
}
