using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;
using HCS.IntegrationEvents.Auditing;

namespace HCS.Auditing;

public class AuditRecordIntegrationEventHandler(
    IAuditRecordProjectionRepository repository) :
    IDistributedEventHandler<AuditRecordCapturedEto>,
    ITransientDependency
{
    [UnitOfWork]
    public virtual async Task HandleEventAsync(AuditRecordCapturedEto eventData)
    {
        if (await repository.FindAsync(eventData.Id) is not null)
        {
            return;
        }

        await repository.InsertAsync(new AuditRecordProjection(eventData));
    }
}
