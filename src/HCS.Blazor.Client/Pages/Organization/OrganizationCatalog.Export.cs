using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HCS.Blazor.Client.Pages.Organization;

public partial class OrganizationCatalog
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

    private async Task ExportCurrentPageAsync()
    {
        if (rows.Count == 0)
        {
            return;
        }

        var headers = new List<string>();
        if (!Definition.IsTypedMasterData)
        {
            headers.Add(L["Catalog:Type"].Value);
        }

        headers.Add(L["Catalog:Code"].Value);
        headers.Add(L["Catalog:Name"].Value);
        if (Definition.Kind is OrganizationCatalogKind.Department or OrganizationCatalogKind.Unit)
        {
            headers.Add(RelationColumnTitle);
        }

        if (Definition.Kind == OrganizationCatalogKind.Position)
        {
            headers.Add(L["Catalog:SignOrder"].Value);
        }

        headers.Add(L["Catalog:SortOrder"].Value);
        headers.Add(L["Catalog:Status"].Value);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            var values = new List<string>();
            if (!Definition.IsTypedMasterData)
            {
                values.Add(row.Type);
            }

            values.Add(row.Code);
            values.Add(row.Name);
            if (Definition.Kind is OrganizationCatalogKind.Department or OrganizationCatalogKind.Unit)
            {
                values.Add(RelationName(row.RelationId));
            }

            if (Definition.Kind == OrganizationCatalogKind.Position)
            {
                values.Add(row.SignOrder.ToString(CultureInfo.InvariantCulture));
            }

            values.Add(row.SortOrder.ToString(CultureInfo.InvariantCulture));
            values.Add(row.IsActive ? L["Catalog:Active"].Value : L["Catalog:Inactive"].Value);
            csv.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        var fileName = $"{Definition.Title}-{DateTime.Now:yyyyMMdd-HHmm}.csv";
        try
        {
            await JsRuntime.InvokeVoidAsync("hcsDownloadTextFile", fileName, csv.ToString(), "text/csv;charset=utf-8");
            await UiMessageService.Success(L["Catalog:Exported"].Value);
        }
        catch (Exception)
        {
            await UiMessageService.Error(L["Catalog:ExportError"].Value);
        }
    }

    private static string EscapeCsv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
