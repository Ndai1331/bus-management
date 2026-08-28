using HCS.CollaborationService.Domain;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace HCS.CollaborationService.Data;

[ConnectionStringName("Collaboration")]
public sealed class CollaborationDbContext(DbContextOptions<CollaborationDbContext> options)
    : AbpDbContext<CollaborationDbContext>(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<MessageAttachment> Attachments => Set<MessageAttachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationReceiver> NotificationReceivers => Set<NotificationReceiver>();
    public DbSet<PushDeviceToken> PushDeviceTokens => Set<PushDeviceToken>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<PushDelivery> PushDeliveries => Set<PushDelivery>();
    public DbSet<WorkSubjectProjection> WorkSubjects => Set<WorkSubjectProjection>();
    public DbSet<WorkSubjectMemberProjection> WorkSubjectMembers => Set<WorkSubjectMemberProjection>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Conversation>(b =>
        {
            b.ToTable("CollaborationConversations");
            b.ConfigureByConvention();
            b.Property(x => x.Name).HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(1024);
            b.Property(x => x.LastMessage).HasMaxLength(512);
            b.HasIndex(x => x.ProjectId).IsUnique().HasFilter("\"Type\" = 2 AND \"ProjectId\" IS NOT NULL");
            b.HasIndex(x => x.TaskId).IsUnique().HasFilter("\"Type\" = 3 AND \"TaskId\" IS NOT NULL");
            b.HasIndex(x => new { x.DirectUserLowId, x.DirectUserHighId }).IsUnique()
                .HasFilter("\"Type\" = 0 AND \"DirectUserLowId\" IS NOT NULL AND \"DirectUserHighId\" IS NOT NULL");
            b.ToTable(t => t.HasCheckConstraint("CK_Conversation_SubjectShape",
                "(\"Type\" IN (0,1) AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL) OR " +
                "(\"Type\" = 2 AND \"ProjectId\" IS NOT NULL AND \"TaskId\" IS NULL) OR " +
                "(\"Type\" = 3 AND \"ProjectId\" IS NULL AND \"TaskId\" IS NOT NULL)"));
            b.HasMany(x => x.Members).WithOne().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ConversationMember>(b =>
        {
            b.ToTable("CollaborationConversationMembers"); b.ConfigureByConvention();
            b.HasIndex(x => new { x.ConversationId, x.UserId }).IsUnique();
            b.HasIndex(x => new { x.UserId, x.IsPinned });
        });
        builder.Entity<ChatMessage>(b =>
        {
            b.ToTable("CollaborationMessages"); b.ConfigureByConvention();
            b.Property(x => x.Text).HasMaxLength(4000);
            b.HasIndex(x => new { x.ConversationId, x.CreationTime });
            b.HasIndex(x => new { x.ConversationId, x.ClientMessageId }).IsUnique()
                .HasFilter("\"ClientMessageId\" IS NOT NULL");
            b.HasIndex(x => new { x.ConversationId, x.IsPinned });
            b.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<MessageAttachment>(b =>
        {
            b.ToTable("CollaborationAttachments"); b.ConfigureByConvention();
            b.Property(x => x.BlobName).HasMaxLength(512);
            b.Property(x => x.FileName).HasMaxLength(256);
            b.Property(x => x.ContentType).HasMaxLength(128);
            b.HasIndex(x => new { x.ConversationId, x.CreationTime });
            b.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Notification>(b =>
        {
            b.ToTable("CollaborationNotifications"); b.ConfigureByConvention();
            b.Property(x => x.Title).HasMaxLength(256); b.Property(x => x.Body).HasMaxLength(2000); b.Property(x => x.Link).HasMaxLength(1024);
        });
        builder.Entity<NotificationReceiver>(b =>
        {
            b.ToTable("CollaborationNotificationReceivers"); b.ConfigureByConvention();
            b.HasIndex(x => new { x.NotificationId, x.UserId }).IsUnique();
            b.HasIndex(x => new { x.UserId, x.IsRead, x.CreationTime });
            b.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<PushDeviceToken>(b =>
        {
            b.ToTable("CollaborationPushDeviceTokens"); b.ConfigureByConvention();
            b.Property(x => x.Token).HasMaxLength(2048); b.Property(x => x.Platform).HasMaxLength(32);
            b.HasIndex(x => x.Token).IsUnique(); b.HasIndex(x => new { x.UserId, x.IsActive });
        });
        builder.Entity<InboxMessage>(b => { b.ToTable("CollaborationInbox"); b.Property(x => x.EventName).HasMaxLength(256); });
        builder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("CollaborationOutbox"); b.Property(x => x.EventName).HasMaxLength(256); b.Property(x => x.Payload).HasColumnType("jsonb");
            b.Property(x => x.LastError).HasMaxLength(1000);
            b.HasIndex(x => new { x.PublishedAt, x.DeadLetteredAt, x.LeaseUntil, x.OccurredAt });
        });
        builder.Entity<PushDelivery>(b =>
        {
            b.ToTable("CollaborationPushDeliveries"); b.Property(x => x.Title).HasMaxLength(256); b.Property(x => x.Body).HasMaxLength(2000); b.Property(x => x.Link).HasMaxLength(1024); b.Property(x => x.LastError).HasMaxLength(1000);
            b.HasIndex(x => new { x.DeliveredAt, x.DeadLetteredAt, x.LeaseUntil, x.NextAttemptAt });
        });
        builder.Entity<WorkSubjectProjection>(b =>
        {
            b.ToTable("CollaborationWorkSubjects"); b.Property(x => x.SubjectType).HasMaxLength(16);
            b.HasIndex(x => x.ProjectId).IsUnique().HasFilter("\"SubjectType\" = 'Project' AND \"TaskId\" IS NULL");
            b.HasIndex(x => x.TaskId).IsUnique().HasFilter("\"SubjectType\" = 'Task' AND \"TaskId\" IS NOT NULL");
            b.HasIndex(x => new { x.SubjectType, x.IsDeleted, x.LastOccurredAtUtc });
            b.HasMany(x => x.Members).WithOne().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<WorkSubjectMemberProjection>(b =>
        {
            b.ToTable("CollaborationWorkSubjectMembers");
            b.HasIndex(x => new { x.SubjectId, x.UserId }).IsUnique(); b.HasIndex(x => x.UserId);
        });
    }
}
