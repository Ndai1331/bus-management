using System;
using HCS.CollaborationService.Contracts;
using Microsoft.Extensions.Localization;

namespace HCS.Blazor.Client.Collaboration;

public static class NotificationText
{
    public static string Localize(IStringLocalizer localizer, string stored) =>
        NotificationLocalization.Format(
            stored,
            key => localizer[key],
            (key, args) => localizer[key, args]);

    public static string ResolveLink(NotificationDto item)
    {
        var link = item.Link ?? "";
        if (link.Contains("workflow", StringComparison.OrdinalIgnoreCase)
            || link.Contains("signing", StringComparison.OrdinalIgnoreCase)
            || item.Title.Contains("WORKFLOW", StringComparison.OrdinalIgnoreCase)
            || item.Body.Contains("WORKFLOW", StringComparison.OrdinalIgnoreCase))
            return "/document-signing";
        if (link.StartsWith('/')) return link;
        return "/workspace";
    }

    public static string FormatLocal(DateTime value, string format)
    {
        if (value.Year < 2000) return "—";
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime()
            : value.Kind == DateTimeKind.Local ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();
        return local.ToString(format);
    }
}
