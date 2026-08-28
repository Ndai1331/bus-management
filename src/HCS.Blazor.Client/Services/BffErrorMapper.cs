using System;
using System.Net;
using System.Text.Json;
using HCS.Blazor.Client.Collaboration;
using Microsoft.Extensions.Localization;

namespace HCS.Blazor.Client.Services;

public enum BffErrorKind
{
    Load,
    Save,
    Delete
}

public static class BffErrorMapper
{
    public static string From(IStringLocalizer localizer, Exception exception, BffErrorKind kind = BffErrorKind.Load)
    {
        var status = GetStatusCode(exception);
        var body = exception switch
        {
            BffApiException bff => bff.ResponseBody,
            CollaborationApiException collaboration => collaboration.ResponseBody,
            _ => null
        };
        if (TryLocalizeErrorCode(localizer, body, out var localized))
            return localized;

        return status is null ? Fallback(localizer, kind) : From(localizer, status.Value, kind);
    }

    public static HttpStatusCode? GetStatusCode(Exception exception) => exception switch
    {
        BffApiException bff => bff.StatusCode,
        CollaborationApiException collaboration => collaboration.StatusCode,
        _ => null
    };

    public static string From(IStringLocalizer localizer, HttpStatusCode status, BffErrorKind kind = BffErrorKind.Load) =>
        status switch
        {
            HttpStatusCode.Unauthorized => localizer["Catalog:Unauthorized"].Value,
            HttpStatusCode.Forbidden => localizer["Catalog:ForbiddenDescription"].Value,
            HttpStatusCode.BadRequest => localizer["Catalog:ValidationError"].Value,
            HttpStatusCode.NotFound => localizer["Catalog:NotFound"].Value,
            HttpStatusCode.Conflict => localizer["Catalog:Conflict"].Value,
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => localizer["Catalog:ServiceUnavailable"].Value,
            _ => Fallback(localizer, kind)
        };

    private static bool TryLocalizeErrorCode(IStringLocalizer localizer, string? body, out string localized)
    {
        localized = "";
        if (string.IsNullOrWhiteSpace(body) || !TryReadErrorCode(body, out var code))
            return false;

        var match = localizer[code];
        if (string.IsNullOrWhiteSpace(match.Value))
            return false;
        if (match.ResourceNotFound && string.Equals(match.Value, code, StringComparison.Ordinal))
            return false;

        localized = match.Value;
        return true;
    }

    internal static bool TryReadErrorCode(string body, out string code)
    {
        code = "";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (TryReadCode(doc.RootElement, out code))
                return true;
            if (doc.RootElement.TryGetProperty("error", out var error) && TryReadCode(error, out code))
                return true;
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadCode(JsonElement node, out string code)
    {
        code = "";
        if (node.ValueKind == JsonValueKind.String)
        {
            code = node.GetString()?.Trim() ?? "";
            return code.Contains(':', StringComparison.Ordinal) && code.Length > 0;
        }
        if (node.ValueKind != JsonValueKind.Object)
            return false;
        if (!node.TryGetProperty("code", out var codeNode) || codeNode.ValueKind != JsonValueKind.String)
            return false;
        code = codeNode.GetString()?.Trim() ?? "";
        return code.Length > 0;
    }

    private static string Fallback(IStringLocalizer localizer, BffErrorKind kind) => kind switch
    {
        BffErrorKind.Save => localizer["Catalog:SaveError"].Value,
        BffErrorKind.Delete => localizer["Catalog:DeleteError"].Value,
        _ => localizer["Catalog:LoadError"].Value
    };
}
