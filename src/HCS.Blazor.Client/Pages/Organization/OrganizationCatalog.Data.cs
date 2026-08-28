using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blazorise.DataGrid;

namespace HCS.Blazor.Client.Pages.Organization;

public partial class OrganizationCatalog
{
    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<OrganizationCatalogRow> args)
    {
        currentPage = Math.Max(1, args.Page);
        pageSize = Math.Clamp(args.PageSize, PageSizeOptions[0], PageSizeOptions[^1]);
        await LoadPageAsync(currentPage, pageSize, args.CancellationToken);
    }

    private async Task LoadPageAsync(int page, int requestedPageSize, CancellationToken cancellationToken = default)
    {
        if (!isAuthorized)
        {
            return;
        }

        activeLoadCancellation?.Cancel();
        activeLoadCancellation?.Dispose();
        activeLoadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestToken = activeLoadCancellation.Token;
        var requestVersion = ++loadVersion;
        isLoading = true;
        ClearError();
        try
        {
            var query = new OrganizationCatalogQuery(
                filterText,
                ParseStatusFilter(),
                (Math.Max(1, page) - 1) * Math.Clamp(requestedPageSize, 1, 100),
                Math.Clamp(requestedPageSize, 1, 100));

            var loadedRows = new List<OrganizationCatalogRow>();
            var loadedTotalCount = 0;
            switch (Kind)
            {
                case OrganizationCatalogKind.Department:
                {
                    var result = await CatalogClient.GetDepartmentsAsync(query, requestToken);
                    loadedRows.AddRange(result.Items.Select(item => new OrganizationCatalogRow(
                        item.Id, string.Empty, item.Code, item.Name, item.ParentId, 0, item.SortOrder, item.IsActive)));
                    loadedTotalCount = ToDataGridTotal(result.TotalCount);
                    break;
                }
                case OrganizationCatalogKind.Unit:
                {
                    var result = await CatalogClient.GetUnitsAsync(query, requestToken);
                    loadedRows.AddRange(result.Items.Select(item => new OrganizationCatalogRow(
                        item.Id, string.Empty, item.Code, item.Name, item.DepartmentId, 0, item.SortOrder, item.IsActive)));
                    loadedTotalCount = ToDataGridTotal(result.TotalCount);
                    break;
                }
                case OrganizationCatalogKind.Position:
                {
                    var result = await CatalogClient.GetPositionsAsync(query, requestToken);
                    loadedRows.AddRange(result.Items.Select(item => new OrganizationCatalogRow(
                        item.Id, string.Empty, item.Code, item.Name, null, item.SignOrder, item.SortOrder, item.IsActive)));
                    loadedTotalCount = ToDataGridTotal(result.TotalCount);
                    break;
                }
                case OrganizationCatalogKind.MasterData:
                {
                    var result = await CatalogClient.GetMasterDataAsync(Definition.MasterType, query, requestToken);
                    loadedRows.AddRange(result.Items.Select(item => new OrganizationCatalogRow(
                        item.Id, item.Type, item.Code, item.Name, null, 0, item.SortOrder, item.IsActive)));
                    loadedTotalCount = ToDataGridTotal(result.TotalCount);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (requestVersion == loadVersion)
            {
                rows.Clear();
                rows.AddRange(loadedRows);
                totalCount = loadedTotalCount;
            }
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
        {
        }
        catch (OrganizationCatalogApiException exception)
        {
            await SetErrorAsync(GetFriendlyErrorMessage(exception.StatusCode, false), "Catalog:LoadErrorTitle", true, exception.StatusCode);
        }
        catch (Exception)
        {
            await SetErrorAsync(L["Catalog:NetworkError"].Value, "Catalog:LoadErrorTitle", true);
        }
        finally
        {
            if (requestVersion == loadVersion)
            {
                isLoading = false;
                activeLoadCancellation?.Dispose();
                activeLoadCancellation = null;
            }
        }
    }

    private async Task LoadDepartmentOptionsAsync()
    {
        try
        {
            var options = await CatalogClient.GetDepartmentLookupAsync();
            departmentOptions.Clear();
            departmentOptions.AddRange(options.OrderBy(item => item.SortOrder).ThenBy(item => item.Name));
        }
        catch (OrganizationCatalogApiException exception)
        {
            departmentOptions.Clear();
            await SetErrorAsync(GetFriendlyErrorMessage(exception.StatusCode, false), "Catalog:LoadErrorTitle", true, exception.StatusCode);
        }
        catch (Exception)
        {
            departmentOptions.Clear();
            await SetErrorAsync(L["Catalog:LookupError"].Value, "Catalog:LoadErrorTitle", true);
        }
    }

    private async Task RefreshAsync()
    {
        if (dataGrid is null)
        {
            await LoadPageAsync(currentPage, pageSize);
            return;
        }

        await dataGrid.Reload();
    }

    private async Task SearchAsync()
    {
        currentPage = 1;
        if (dataGrid is null)
        {
            await LoadPageAsync(currentPage, pageSize);
            return;
        }

        await dataGrid.Paginate("1");
        await dataGrid.Reload();
    }

    private Task StatusFilterChangedAsync() => SearchAsync();

    private async Task ResetSearchAsync()
    {
        filterText = null;
        statusFilter = string.Empty;
        await SearchAsync();
    }

    private async Task EnsureDepartmentOptionsAsync()
    {
        if (IsDepartmentLookupRequired && departmentOptions.Count == 0)
        {
            await LoadDepartmentOptionsAsync();
        }
    }
}
