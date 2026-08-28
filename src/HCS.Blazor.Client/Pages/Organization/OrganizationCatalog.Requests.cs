using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HCS.Blazor.Client.Components;

namespace HCS.Blazor.Client.Pages.Organization;

public partial class OrganizationCatalog
{
    private void OnParentDepartmentChanged(Guid? id) => form.ParentId = id?.ToString() ?? string.Empty;

    private void OnDepartmentChanged(Guid? id) => form.DepartmentId = id?.ToString() ?? string.Empty;

    private CatalogSelect2Item? DepartmentSelectItem(string value)
    {
        if (!Guid.TryParse(value, out var id))
        {
            return null;
        }

        return departmentOptions.FirstOrDefault(item => item.Id == id) is { } department
            ? new CatalogSelect2Item(department.Id.ToString(), CatalogSelect2Text.CodeName(department.Code, department.Name))
            : null;
    }

    private Task<CatalogSelect2SearchResponse> SearchParentDepartmentsAsync(string term, int page) =>
        SearchDepartmentsAsync(term, page, excludeEditingDepartment: true);

    private Task<CatalogSelect2SearchResponse> SearchDepartmentsAsync(string term, int page) =>
        SearchDepartmentsAsync(term, page, excludeEditingDepartment: false);

    private Task<CatalogSelect2SearchResponse> SearchDepartmentsAsync(
        string term,
        int page,
        bool excludeEditingDepartment)
    {
        var normalizedTerm = CatalogSelect2Text.NormalizeSearch(term);
        var options = (excludeEditingDepartment ? ParentDepartmentOptions : departmentOptions)
            .Where(item => normalizedTerm.Length == 0
                || CatalogSelect2Text.NormalizeSearch(item.Code).Contains(normalizedTerm, StringComparison.Ordinal)
                || CatalogSelect2Text.NormalizeSearch(item.Name).Contains(normalizedTerm, StringComparison.Ordinal))
            .ToList();

        return Task.FromResult(CatalogSelect2Cache.Merge(
            departmentOptions,
            options,
            item => item.Id,
            item => CatalogSelect2Text.CodeName(item.Code, item.Name),
            more: false));
    }

    private bool TryBuildRequest(out object request, out string validationMessage)
    {
        request = new object();
        validationMessage = string.Empty;

        // Code/Name use HcsIsolatedTextInput (no Blazorise Validation) — enforce required here.
        if (string.IsNullOrWhiteSpace(form.Code) || string.IsNullOrWhiteSpace(form.Name))
        {
            validationMessage = L["Catalog:ValidationError"].Value;
            return false;
        }

        var code = form.Code.Trim();
        var name = form.Name.Trim();

        switch (Kind)
        {
            case OrganizationCatalogKind.Department:
                request = new DepartmentUpsertRequest(
                    code,
                    name,
                    ParseOptionalGuid(form.ParentId),
                    form.SortOrder,
                    form.IsActive);
                return true;
            case OrganizationCatalogKind.Unit:
                if (!Guid.TryParse(form.DepartmentId, out var departmentId))
                {
                    validationMessage = L["Catalog:DepartmentRequired"].Value;
                    return false;
                }

                request = new UnitUpsertRequest(
                    departmentId,
                    code,
                    name,
                    form.SortOrder,
                    form.IsActive);
                return true;
            case OrganizationCatalogKind.Position:
                request = new PositionUpsertRequest(
                    code,
                    name,
                    form.SignOrder,
                    form.SortOrder,
                    form.IsActive);
                return true;
            case OrganizationCatalogKind.MasterData:
                var type = Definition.MasterType ?? form.Type;
                if (!MasterTypeOptions.Any(option => string.Equals(option.Type, type, StringComparison.Ordinal)))
                {
                    validationMessage = L["Catalog:TypeRequired"].Value;
                    return false;
                }

                request = new MasterDataUpsertRequest(
                    type,
                    code,
                    name,
                    form.SortOrder,
                    form.IsActive);
                return true;
            default:
                validationMessage = L["Catalog:InvalidType"].Value;
                return false;
        }
    }

    private string RelationName(Guid? relationId)
    {
        if (!relationId.HasValue)
        {
            return "—";
        }

        var department = departmentOptions.FirstOrDefault(item => item.Id == relationId.Value);
        return department is null ? relationId.Value.ToString() : $"{department.Code} — {department.Name}";
    }

    private string GetFriendlyErrorMessage(HttpStatusCode statusCode, bool isMutation) => statusCode switch
    {
        HttpStatusCode.Unauthorized => L["Catalog:Unauthorized"].Value,
        HttpStatusCode.Forbidden => L["Catalog:ForbiddenDescription"].Value,
        HttpStatusCode.BadRequest => L["Catalog:ValidationError"].Value,
        HttpStatusCode.NotFound => L["Catalog:NotFound"].Value,
        HttpStatusCode.Conflict => L["Catalog:Conflict"].Value,
        _ when isMutation => L["Catalog:SaveError"].Value,
        _ => L["Catalog:LoadError"].Value
    };

    private string GetErrorTitleKey(HttpStatusCode statusCode, bool isMutation) => statusCode switch
    {
        HttpStatusCode.BadRequest => "Catalog:ValidationErrorTitle",
        HttpStatusCode.Conflict => "Catalog:ConflictTitle",
        HttpStatusCode.NotFound => "Catalog:NotFoundTitle",
        _ when isMutation => "Catalog:SaveErrorTitle",
        _ => "Catalog:LoadErrorTitle"
    };

    private bool? ParseStatusFilter() => statusFilter switch
    {
        "true" => true,
        "false" => false,
        _ => null
    };

    private static Guid? ParseOptionalGuid(string value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;

    private static int ToDataGridTotal(long value) =>
        value >= int.MaxValue ? int.MaxValue : Math.Max(0, (int)value);
}
