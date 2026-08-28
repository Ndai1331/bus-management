using System;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Components.Messages;

namespace HCS.Blazor.Client.Pages.Organization;

public partial class OrganizationCatalog
{
    private async Task OpenCreateModalAsync()
    {
        if (!canCreate)
        {
            return;
        }

        editingId = null;
        form = NewForm();
        await EnsureDepartmentOptionsAsync();
        if (validations is not null)
        {
            await validations.ClearAll();
        }

        if (editModal is not null)
        {
            await editModal.Show();
        }
    }

    private async Task OpenEditModalAsync(OrganizationCatalogRow row)
    {
        if (!canUpdate)
        {
            return;
        }

        editingId = row.Id;
        form = new OrganizationCatalogFormModel
        {
            Type = row.Type,
            Code = row.Code,
            Name = row.Name,
            ParentId = row.RelationId?.ToString() ?? string.Empty,
            DepartmentId = row.RelationId?.ToString() ?? string.Empty,
            SignOrder = row.SignOrder,
            SortOrder = row.SortOrder,
            IsActive = row.IsActive
        };
        await EnsureDepartmentOptionsAsync();
        if (validations is not null)
        {
            await validations.ClearAll();
        }

        if (editModal is not null)
        {
            await editModal.Show();
        }
    }

    private async Task CloseModalAsync()
    {
        if (editModal is not null)
        {
            await editModal.Hide();
        }

        editingId = null;
        form = NewForm();
    }

    private async Task SaveAsync()
    {
        if (isSaving)
        {
            return;
        }

        if ((editingId.HasValue && !canUpdate) || (!editingId.HasValue && !canCreate))
        {
            return;
        }

        isSaving = true;
        try
        {
            if (validations is not null && !await validations.ValidateAll())
            {
                return;
            }

            if (!TryBuildRequest(out var request, out var validationMessage))
            {
                SetError(validationMessage, "Catalog:ValidationErrorTitle", false);
                await UiMessageService.Warn(validationMessage);
                return;
            }

            var wasEditing = editingId.HasValue;
            ClearError();
            try
            {
                switch (Kind)
                {
                    case OrganizationCatalogKind.Department:
                        if (editingId.HasValue)
                        {
                            await CatalogClient.UpdateDepartmentAsync(editingId.Value, (DepartmentUpsertRequest)request);
                        }
                        else
                        {
                            await CatalogClient.CreateDepartmentAsync((DepartmentUpsertRequest)request);
                        }
                        break;
                    case OrganizationCatalogKind.Unit:
                        if (editingId.HasValue)
                        {
                            await CatalogClient.UpdateUnitAsync(editingId.Value, (UnitUpsertRequest)request);
                        }
                        else
                        {
                            await CatalogClient.CreateUnitAsync((UnitUpsertRequest)request);
                        }
                        break;
                    case OrganizationCatalogKind.Position:
                        if (editingId.HasValue)
                        {
                            await CatalogClient.UpdatePositionAsync(editingId.Value, (PositionUpsertRequest)request);
                        }
                        else
                        {
                            await CatalogClient.CreatePositionAsync((PositionUpsertRequest)request);
                        }
                        break;
                    case OrganizationCatalogKind.MasterData:
                        if (editingId.HasValue)
                        {
                            await CatalogClient.UpdateMasterDataAsync(editingId.Value, (MasterDataUpsertRequest)request);
                        }
                        else
                        {
                            await CatalogClient.CreateMasterDataAsync((MasterDataUpsertRequest)request);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                await CloseModalAsync();
                await UiMessageService.Success(wasEditing
                    ? L["Catalog:Updated"].Value
                    : L["Catalog:Created"].Value);
                await RefreshAsync();
            }
            catch (OrganizationCatalogApiException exception)
            {
                var message = GetFriendlyErrorMessage(exception.StatusCode, true);
                SetError(message, GetErrorTitleKey(exception.StatusCode, true), false);
                await ShowErrorAsync(message, exception.StatusCode);
            }
            catch (Exception)
            {
                var message = L["Catalog:SaveError"].Value;
                SetError(message, "Catalog:SaveErrorTitle", false);
                await ShowErrorAsync(message);
            }
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task DeleteAsync(OrganizationCatalogRow row)
    {
        if (!canDelete || isDeleting.Contains(row.Id))
        {
            return;
        }

        var confirmed = await UiMessageService.Confirm(
            string.Format(L["Catalog:DeleteConfirmation"].Value, row.Name));
        if (!confirmed || !isDeleting.Add(row.Id))
        {
            return;
        }

        ClearError();
        try
        {
            await CatalogClient.DeleteAsync(Kind, row.Id);
            await UiMessageService.Success(L["Catalog:Deleted"].Value);
            await RefreshAsync();
        }
        catch (OrganizationCatalogApiException exception)
        {
            var message = GetFriendlyErrorMessage(exception.StatusCode, true);
            SetError(message, GetErrorTitleKey(exception.StatusCode, true), false);
            await ShowErrorAsync(message, exception.StatusCode);
        }
        catch (Exception)
        {
            var message = L["Catalog:DeleteError"].Value;
            SetError(message, "Catalog:DeleteErrorTitle", false);
            await ShowErrorAsync(message);
        }
        finally
        {
            isDeleting.Remove(row.Id);
        }
    }
}
