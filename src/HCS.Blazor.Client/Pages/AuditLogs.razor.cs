using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HCS.Auditing;
using HCS.Blazor.Client.Auditing;
using HCS.Blazor.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace HCS.Blazor.Client.Pages;

public partial class AuditLogs : HCSComponentBase, IDisposable
{
    private readonly List<AuditLogDto> rows = [];
    private CancellationTokenSource? loadCancellation;
    private CancellationTokenSource? detailCancellation;
    private ElementReference detailModal;
    private AuditLogDetailDto? selected;
    private bool isLoading;
    private bool isLoadingDetail;
    private bool focusDetail;
    private bool showFilters;
    private string? errorMessage;
    private string? detailError;
    private string filter = "";
    private string userName = "";
    private string userId = "";
    private string startTime = "";
    private string endTime = "";
    private string status = "";
    private string httpMethod = "";
    private string clientIpAddress = "";
    private string browserInfo = "";
    private string sourceService = "";
    private string applicationName = "";
    private string correlationId = "";
    private string action = "";
    private string url = "";
    private string exceptionState = "";
    private long totalCount;
    private int currentPage = 1;
    private int pageSize = 20;
    private string sortField = "ExecutionTime";
    private bool sortDescending = true;
    private DateTimeOffset? lastUpdated;

    [Inject] private AuditLogClient AuditClient { get; set; } = default!;

    private int totalPages => Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();
        var token = loadCancellation.Token;
        isLoading = true;
        errorMessage = null;
        try
        {
            var input = BuildInput();
            if (input.StartTime.HasValue && input.EndTimeExclusive.HasValue && input.StartTime >= input.EndTimeExclusive)
            {
                errorMessage = L["Audit:InvalidDateRange"].Value;
                rows.Clear();
                totalCount = 0;
                return;
            }

            var result = await AuditClient.GetListAsync(input, token);
            if (token.IsCancellationRequested) return;
            rows.Clear();
            rows.AddRange(result.Items);
            totalCount = result.TotalCount;
            lastUpdated = DateTimeOffset.Now;
            if (currentPage > totalPages && totalCount > 0)
            {
                currentPage = totalPages;
                await LoadAsync();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (token.IsCancellationRequested) return;
            if (exception is BffApiException { StatusCode: HttpStatusCode.Unauthorized })
            {
                await NotifyErrorAsync(exception);
                return;
            }

            rows.Clear();
            totalCount = 0;
            errorMessage = MapBffError(exception);
        }
        finally
        {
            if (!token.IsCancellationRequested) isLoading = false;
        }
    }

    private async Task SearchAsync()
    {
        currentPage = 1;
        await LoadAsync();
    }

    private Task OnKeywordKeyDown(KeyboardEventArgs args) =>
        args.Key == "Enter" ? SearchAsync() : Task.CompletedTask;

    private async Task OnPageSizeChanged(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var requestedSize))
        {
            pageSize = Math.Clamp(requestedSize, 20, 100);
            currentPage = 1;
            await LoadAsync();
        }
    }

    private void OnStartTimeChanged(ChangeEventArgs args) => startTime = args.Value?.ToString() ?? "";
    private void OnEndTimeChanged(ChangeEventArgs args) => endTime = args.Value?.ToString() ?? "";

    private async Task ResetAsync()
    {
        filter = userName = userId = startTime = endTime = status = httpMethod = clientIpAddress = "";
        browserInfo = sourceService = applicationName = correlationId = action = url = exceptionState = "";
        showFilters = false;
        currentPage = 1;
        await LoadAsync();
    }

    private async Task SortByAsync(string field)
    {
        if (string.Equals(sortField, field, StringComparison.Ordinal)) sortDescending = !sortDescending;
        else { sortField = field; sortDescending = true; }
        currentPage = 1;
        await LoadAsync();
    }

    private Task SortStatusAsync() => SortByAsync("HttpStatusCode");
    private Task SortUserAsync() => SortByAsync("UserName");
    private Task SortTimeAsync() => SortByAsync("ExecutionTime");
    private Task SortDurationAsync() => SortByAsync("ExecutionDuration");

    private string SortMarker(string field) =>
        string.Equals(sortField, field, StringComparison.Ordinal)
            ? sortDescending ? "↓" : "↑"
            : "";

    private string SortAria(string field) =>
        string.Equals(sortField, field, StringComparison.Ordinal)
            ? sortDescending ? "descending" : "ascending"
            : "none";

    private async Task MovePageAsync(int target)
    {
        if (isLoading || target < 1 || target > totalPages || target == currentPage) return;
        currentPage = target;
        await LoadAsync();
    }

    private async Task OpenDetailAsync(AuditLogDto row)
    {
        detailCancellation?.Cancel();
        detailCancellation?.Dispose();
        detailCancellation = new CancellationTokenSource();
        var token = detailCancellation.Token;
        var detailId = row.Id;
        selected = null;
        detailError = null;
        isLoadingDetail = true;
        focusDetail = true;
        try
        {
            var result = await AuditClient.GetAsync(detailId, token);
            if (!token.IsCancellationRequested) selected = result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
            {
                if (exception is BffApiException { StatusCode: HttpStatusCode.Unauthorized })
                    await NotifyErrorAsync(exception);
                else
                    detailError = MapBffError(exception);
            }
        }
        finally
        {
            if (!token.IsCancellationRequested) isLoadingDetail = false;
        }
    }

    private void CloseDetail()
    {
        detailCancellation?.Cancel();
        selected = null;
        detailError = null;
        isLoadingDetail = false;
        focusDetail = false;
    }

    private void HandleDialogKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape") CloseDetail();
    }

    private GetAuditLogsInput BuildInput() => new()
    {
        Filter = filter,
        UserName = userName,
        UserId = Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : null,
        StartTime = ParseLocalTime(startTime),
        EndTimeExclusive = ParseLocalTime(endTime),
        HttpStatusCode = int.TryParse(status, out var statusCode) ? statusCode : null,
        HttpMethod = httpMethod,
        ClientIpAddress = clientIpAddress,
        BrowserInfo = browserInfo,
        SourceService = sourceService,
        ApplicationName = applicationName,
        HasException = exceptionState switch { "true" => true, "false" => false, _ => null },
        CorrelationId = correlationId,
        Action = action,
        Url = url,
        SkipCount = (currentPage - 1) * pageSize,
        MaxResultCount = pageSize,
        Sorting = $"{sortField} {(sortDescending ? "DESC" : "ASC")}"
    };

    private static DateTime? ParseLocalTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)) return null;
        return DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime();
    }

    private static string DisplayUser(AuditLogDto row) =>
        FirstValue(row.UserName, row.UserId?.ToString("D"));

    private static string DisplayUser(AuditLogDetailDto row) =>
        FirstValue(row.UserName, row.UserId?.ToString("D"));

    private static string FirstValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "—";

    private static string FormatDate(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        return local.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private static string IsoDate(DateTime value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private string StatusText(int? statusCode) => statusCode switch
    {
        200 => L["Audit:StatusOk"].Value,
        201 => L["Audit:StatusCreated"].Value,
        202 => L["Audit:StatusAccepted"].Value,
        204 => L["Audit:StatusNoContent"].Value,
        400 => L["Audit:StatusBadRequest"].Value,
        401 => L["Audit:StatusUnauthorized"].Value,
        403 => L["Audit:StatusForbidden"].Value,
        404 => L["Audit:StatusNotFound"].Value,
        500 => L["Audit:StatusServerError"].Value,
        502 => L["Audit:StatusBadGateway"].Value,
        503 => L["Audit:StatusUnavailable"].Value,
        _ => statusCode?.ToString(CultureInfo.InvariantCulture) ?? "—"
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!focusDetail) return;
        focusDetail = false;
        await detailModal.FocusAsync();
    }

    private static string StatusClass(int? statusCode) => statusCode switch
    {
        >= 200 and < 300 => "hcs-audit-status--success",
        >= 300 and < 400 => "hcs-audit-status--info",
        >= 400 and < 500 => "hcs-audit-status--warning",
        >= 500 => "hcs-audit-status--danger",
        _ => "hcs-audit-status--muted"
    };

    public void Dispose()
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        detailCancellation?.Cancel();
        detailCancellation?.Dispose();
    }
}
