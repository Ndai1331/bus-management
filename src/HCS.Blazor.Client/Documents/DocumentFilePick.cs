using System;
using System.Collections.Generic;
using System.Linq;

namespace HCS.Blazor.Client.Documents;

internal static class DocumentFilePick
{
    public static DocumentFileDto? PreferPdf(IReadOnlyList<DocumentFileDto> files)
    {
        if (files.Count == 0) return null;
        var ordered = files.OrderByDescending(x => x.CreationTime).ThenByDescending(x => x.Id).ToList();
        var pdf = ordered.FirstOrDefault(IsPdf);
        if (pdf is not null) return pdf;
        foreach (var file in ordered)
        {
            if (file.PairedFileId is not { } pair) continue;
            var paired = files.FirstOrDefault(x => x.Id == pair);
            if (paired is not null && IsPdf(paired)) return paired;
        }
        return null;
    }

    public static bool IsPdf(DocumentFileDto file) =>
        file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
        || file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
}
