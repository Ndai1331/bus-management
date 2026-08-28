using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using HCS.Localization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HCS.EntityFrameworkCore.Localization;

public class EfCoreLanguageRepository : EfCoreRepository<HCSDbContext, Language, Guid>, ILanguageRepository
{
    public EfCoreLanguageRepository(IDbContextProvider<HCSDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public async Task<Language?> FindByCultureNameAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.FirstOrDefaultAsync(x => x.CultureName == cultureName, GetCancellationToken(cancellationToken));
    }

    public async Task<Language?> FindDefaultAsync(CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.FirstOrDefaultAsync(x => x.IsDefault, GetCancellationToken(cancellationToken));
    }

    public async Task<List<Language>> GetFilteredListAsync(string? filter, bool? isEnabled, int skipCount, int maxResultCount, string sorting, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(await GetQueryableAsync(), filter, isEnabled);
        return await query.OrderBy(sorting).PageBy(skipCount, maxResultCount).ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<long> GetFilteredCountAsync(string? filter, bool? isEnabled, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(await GetQueryableAsync(), filter, isEnabled);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    private static IQueryable<Language> ApplyFilter(IQueryable<Language> query, string? filter, bool? isEnabled) =>
        query.WhereIf(!filter.IsNullOrWhiteSpace(), x => x.CultureName.Contains(filter!) || x.DisplayName.Contains(filter!))
            .WhereIf(isEnabled.HasValue, x => x.IsEnabled == isEnabled);
}
