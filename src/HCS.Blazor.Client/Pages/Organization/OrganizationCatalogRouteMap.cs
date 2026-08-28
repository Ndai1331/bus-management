using System.Collections.Generic;

namespace HCS.Blazor.Client.Pages.Organization;

internal sealed record MasterDataRouteDefinition(string Type, string TitleKey);

internal static class OrganizationCatalogRouteMap
{
    public static IReadOnlyList<MasterDataRouteDefinition> MasterDataTypes { get; } =
    [
        new("DocumentType", "Catalog:DocumentTypeTitle"),
        new("Sector", "Catalog:SectorTitle"),
        new("UrgencyLevel", "Catalog:UrgencyLevelTitle"),
        new("ConfidentialityLevel", "Catalog:ConfidentialityLevelTitle"),
        new("ProcessingMethod", "Catalog:ProcessingMethodTitle"),
        new("DocumentStatus", "Catalog:DocumentStatusTitle"),
        new("SigningMethod", "Catalog:SigningMethodTitle"),
        new("EventType", "Catalog:EventTypeTitle")
    ];

    public static bool TryResolve(string route, out MasterDataRouteDefinition definition)
    {
        definition = route switch
        {
            "document-types" => MasterDataTypes[0],
            "sectors" => MasterDataTypes[1],
            "urgency-levels" => MasterDataTypes[2],
            "confidentiality-levels" => MasterDataTypes[3],
            "processing-methods" => MasterDataTypes[4],
            "document-status" => MasterDataTypes[5],
            "signing-methods" => MasterDataTypes[6],
            "even-types" or "event-types" => MasterDataTypes[7],
            _ => null!
        };

        return definition is not null;
    }
}
