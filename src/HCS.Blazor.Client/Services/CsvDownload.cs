using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace HCS.Blazor.Client.Services;

internal static class CsvDownload
{
    public static async Task DownloadAsync(IJSRuntime js, string fileName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",", row.Select(Escape)));
        }

        await js.InvokeVoidAsync("hcsDownloadTextFile", fileName, csv.ToString(), "text/csv;charset=utf-8");
    }

    private static string Escape(string value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}
