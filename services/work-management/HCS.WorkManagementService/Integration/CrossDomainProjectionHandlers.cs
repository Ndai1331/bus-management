using HCS.IntegrationEvents.Documents;
using HCS.IntegrationEvents.Identity;
using HCS.WorkManagementService.Data;
using HCS.WorkManagementService.Domain;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace HCS.WorkManagementService.Integration;

public sealed class UserProvisionedProjectionHandler(IInboxExecutor inbox, WorkManagementDbContext db)
    : IDistributedEventHandler<UserProvisionedEto>, ITransientDependency
{
    public Task HandleEventAsync(UserProvisionedEto eventData) => inbox.ExecuteOnceAsync(eventData.EventId,
        nameof(UserProvisionedProjectionHandler), _ =>
        {
            db.ReportReadModels.Add(new ReportReadModel(Guid.NewGuid(), "users", eventData.UserId.ToString("N"),
                eventData.UserName, 1, DateTime.UtcNow));
            return Task.CompletedTask;
        });
}

public sealed class DocumentAssignedProjectionHandler(IInboxExecutor inbox, WorkManagementDbContext db)
    : IDistributedEventHandler<DocumentAssignedEto>, ITransientDependency
{
    public Task HandleEventAsync(DocumentAssignedEto eventData) => inbox.ExecuteOnceAsync(eventData.EventId,
        nameof(DocumentAssignedProjectionHandler), _ =>
        {
            db.ReportReadModels.Add(new ReportReadModel(Guid.NewGuid(), "document-assignments",
                eventData.AssignmentId.ToString("N"), eventData.DocumentId.ToString("N"), 1, DateTime.UtcNow));
            return Task.CompletedTask;
        });
}
