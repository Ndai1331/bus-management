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

public class EfCoreLanguageTextRepository : EfCoreRepository<HCSDbContext, LanguageText, Guid>, ILanguageTextRepository
{
    public EfCoreLanguageTextRepository(IDbContextProvider<HCSDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public async Task<LanguageText?> FindByKeyAsync(string resourceName, string cultureName, string name, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.FirstOrDefaultAsync(x => x.ResourceName == resourceName && x.CultureName == cultureName && x.Name == name, GetCancellationToken(cancellationToken));
    }

    public async Task<List<LanguageText>> GetByResourceCultureAsync(string resourceName, string cultureName, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.Where(x => x.ResourceName == resourceName && x.CultureName == cultureName).ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<LanguageText>> GetFilteredListAsync(string? resourceName, string? cultureName, string? filter, int skipCount, int maxResultCount, string sorting, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(await GetQueryableAsync(), resourceName, cultureName, filter);
        return await query.OrderBy(sorting).PageBy(skipCount, maxResultCount).ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<long> GetFilteredCountAsync(string? resourceName, string? cultureName, string? filter, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(await GetQueryableAsync(), resourceName, cultureName, filter);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    private static IQueryable<LanguageText> ApplyFilter(IQueryable<LanguageText> query, string? resourceName, string? cultureName, string? filter) =>
        query.WhereIf(!resourceName.IsNullOrWhiteSpace(), x => x.ResourceName == resourceName)
            .WhereIf(!cultureName.IsNullOrWhiteSpace(), x => x.CultureName == cultureName)
            .WhereIf(!filter.IsNullOrWhiteSpace(), x => x.Name.Contains(filter!) || x.Value.Contains(filter!));
}
