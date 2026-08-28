using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace HCS.Localization;

public class LocalizationChangedHandler : IDistributedEventHandler<LocalizationChangedEto>, ITransientDependency
{
    private readonly ILocalizationStore _store;

    public LocalizationChangedHandler(ILocalizationStore store)
    {
        _store = store;
    }

    public Task HandleEventAsync(LocalizationChangedEto eventData) => _store.InvalidateAsync(eventData);
}
