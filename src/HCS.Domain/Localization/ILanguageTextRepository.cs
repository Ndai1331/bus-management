using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HCS.Localization;

public interface ILanguageTextRepository : IRepository<LanguageText, Guid>
{
    Task<LanguageText?> FindByKeyAsync(string resourceName, string cultureName, string name, CancellationToken cancellationToken = default);
    Task<List<LanguageText>> GetByResourceCultureAsync(string resourceName, string cultureName, CancellationToken cancellationToken = default);
    Task<List<LanguageText>> GetFilteredListAsync(string? resourceName, string? cultureName, string? filter, int skipCount, int maxResultCount, string sorting, CancellationToken cancellationToken = default);
    Task<long> GetFilteredCountAsync(string? resourceName, string? cultureName, string? filter, CancellationToken cancellationToken = default);
}
