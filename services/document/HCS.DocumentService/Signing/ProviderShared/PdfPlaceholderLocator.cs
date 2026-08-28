using System.Text;
using UglyToad.PdfPig;

namespace HCS.DocumentService.Signing;

internal static class PdfPlaceholderLocator
{
    internal sealed record Hit(int Page, double X, double Y, double Width, double Height);
    internal sealed record TextRun(string Value, double Left, double Bottom, double Right, double Top);
    private const double MaxWidthRatio = 0.75;
    private const double MaxHeightRatio = 0.5;

    internal static Hit? Find(byte[] pdfBytes, string placeholder)
    {
        if (pdfBytes is not { Length: > 0 } || string.IsNullOrWhiteSpace(placeholder)) return null;

        using var document = PdfDocument.Open(pdfBytes);
        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            var page = document.GetPage(pageNumber);
            var letters = page.Letters.ToList();
            if (letters.Count == 0) continue;

            var runs = letters.Select(letter =>
            {
                var rect = letter.GlyphRectangle;
                return new TextRun(letter.Value, rect.Left, rect.Bottom, rect.Right, rect.Top);
            }).ToList();
            if (FindInRuns(pageNumber, page.Width, page.Height, runs, placeholder) is { } hit)
                return hit;
        }

        return null;
    }

    internal static Hit? FindInRuns(int pageNumber, double pageWidth, double pageHeight,
        IReadOnlyList<TextRun> runs, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(placeholder)) return null;

        var text = new StringBuilder(runs.Sum(run => run.Value.Length));
        var characterMap = new List<TextRun>();
        foreach (var run in runs)
        {
            if (string.IsNullOrEmpty(run.Value)) continue;
            foreach (var character in run.Value)
            {
                text.Append(character);
                characterMap.Add(run);
            }
        }

        var textValue = text.ToString();
        var searchFromIndex = 0;
        while (searchFromIndex < textValue.Length)
        {
            var index = textValue.IndexOf(placeholder, searchFromIndex, StringComparison.Ordinal);
            if (index < 0 || index + placeholder.Length > characterMap.Count) return null;

            var matchedRuns = characterMap.Skip(index).Take(placeholder.Length).ToList();
            if (matchedRuns.Count == 0) return null;

            var first = matchedRuns[0];
            var left = first.Left;
            var bottom = first.Bottom;
            var right = first.Right;
            var top = first.Top;
            foreach (var run in matchedRuns.Skip(1))
            {
                left = Math.Min(left, run.Left);
                bottom = Math.Min(bottom, run.Bottom);
                right = Math.Max(right, run.Right);
                top = Math.Max(top, run.Top);
            }

            var width = right - left;
            var height = top - bottom;
            if (IsSafeBounds(left, bottom, width, height, pageWidth, pageHeight))
                return new Hit(pageNumber, left, bottom, width, height);

            searchFromIndex = index + placeholder.Length;
        }

        return null;
    }

    private static bool IsSafeBounds(double x, double y, double width, double height, double pageWidth, double pageHeight) =>
        double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(width) && double.IsFinite(height)
        && width > 0 && height > 0
        && x >= -1 && y >= -1
        && x + width <= pageWidth + 1
        && y + height <= pageHeight + 1
        && width <= pageWidth * MaxWidthRatio
        && height <= pageHeight * MaxHeightRatio;
}
