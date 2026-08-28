using System;
using System.Collections.Generic;
using System.Linq;
using HCS.Blazor.Client.Work;

namespace HCS.Blazor.Client.Components;

public sealed record HcsCalendarRange(string Start, string End, string CurrentStart, string ViewType);

public sealed class HcsCalendarJsEvent
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Start { get; init; } = "";
    public string End { get; init; } = "";
    public bool AllDay { get; init; }
    public string BackgroundColor { get; init; } = "";
    public string BorderColor { get; init; } = "";
    public string TextColor { get; init; } = "";
    public string[] ClassNames { get; init; } = [];
}

public static class CalendarEventDisplay
{
    public const string ProjectAccent = "#d97706";
    public const string ProjectBackground = "#fff4d6";
    public const string TaskAccent = "#0f9d8e";
    public const string TaskBackground = "#d9f6f3";
    public const string EventAccent = "#3d5cff";
    public const string EventBackground = "#e8f0ff";

    public static HcsCalendarJsEvent ToJs(CalendarEventDto item)
    {
        var (kind, accent, background) = Style(item);
        var startLocal = item.StartTime.ToLocalTime();
        var endLocal = item.EndTime.ToLocalTime();
        string start;
        string end;
        if (item.AllDay)
        {
            start = startLocal.ToString("yyyy-MM-dd");
            var lastInclusive = endLocal.Date < startLocal.Date ? startLocal.Date : endLocal.Date;
            end = lastInclusive.AddDays(1).ToString("yyyy-MM-dd");
        }
        else
        {
            start = startLocal.ToString("o");
            end = endLocal.ToString("o");
        }

        return new HcsCalendarJsEvent
        {
            Id = item.Id.ToString("D"),
            Title = item.Title,
            Start = start,
            End = end,
            AllDay = item.AllDay,
            BackgroundColor = background,
            BorderColor = accent,
            TextColor = accent,
            ClassNames = ["hcs-cal-event", "hcs-cal-event--" + kind]
        };
    }

    public static IReadOnlyList<HcsCalendarJsEvent> ToJs(IEnumerable<CalendarEventDto> items) =>
        items.Select(ToJs).ToList();

    private static (string Kind, string Accent, string Background) Style(CalendarEventDto item)
    {
        if (Is(item.EventType, "PROJECT") || Is(item.RelatedType, "PROJECT")) return ("project", ProjectAccent, ProjectBackground);
        if (Is(item.EventType, "TASK") || Is(item.RelatedType, "TASK")) return ("task", TaskAccent, TaskBackground);
        return ("event", EventAccent, EventBackground);
    }

    private static bool Is(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
