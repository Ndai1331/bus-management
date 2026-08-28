using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HCS.Localization;

public interface ILanguageRepository : IRepository<Language, Guid>
{
    Task<Language?> FindByCultureNameAsync(string cultureName, CancellationToken cancellationToken = default);
    Task<Language?> FindDefaultAsync(CancellationToken cancellationToken = default);
    Task<List<Language>> GetFilteredListAsync(string? filter, bool? isEnabled, int skipCount, int maxResultCount, string sorting, CancellationToken cancellationToken = default);
    Task<long> GetFilteredCountAsync(string? filter, bool? isEnabled, CancellationToken cancellationToken = default);
}
