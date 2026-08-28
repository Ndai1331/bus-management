using System;
using System.Collections.Generic;

namespace HCS.Blazor.Client.Pages;

internal sealed record SurveyCatalogItem(Guid Id, string Code, string Name, int SortOrder, bool IsActive);

internal sealed class SurveyCatalogCache
{
    public List<SurveyCatalogItem> Locations { get; } = [];
    public List<SurveyCatalogItem> Criterias { get; } = [];
    public bool LocationsLoaded { get; set; }
    public bool CriteriasLoaded { get; set; }

    public List<SurveyCatalogItem> For(bool isCriteria) => isCriteria ? Criterias : Locations;

    public bool IsLoaded(bool isCriteria) => isCriteria ? CriteriasLoaded : LocationsLoaded;

    public void Set(bool isCriteria, IReadOnlyList<SurveyCatalogItem> items)
    {
        var target = For(isCriteria);
        target.Clear();
        target.AddRange(items);
        if (isCriteria)
        {
            CriteriasLoaded = true;
        }
        else
        {
            LocationsLoaded = true;
        }
    }
}
