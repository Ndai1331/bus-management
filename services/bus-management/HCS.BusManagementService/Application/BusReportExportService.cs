using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using HCS.BusManagementService.Contracts;
using Volo.Abp.DependencyInjection;

namespace HCS.BusManagementService.Application;

public sealed record BusReportExportResult(byte[] Content, string ContentType, string FileName);

public sealed class BusReportExportService(BusManagementAppService reports) : ITransientDependency
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const int MaxDateSpanDays = 366;
    private const int MaxRows = 100_000;

    public async Task<BusReportExportResult> ExportAsync(string reportType, string format, DateTime from, DateTime to,
        Guid? stationId, CancellationToken ct)
    {
        if (from.Date > to.Date) throw new ArgumentException("from must be before or equal to to.");
        if ((to.Date - from.Date).TotalDays > MaxDateSpanDays)
            throw new ArgumentException($"Export date range cannot exceed {MaxDateSpanDays} days.");
        var table = await LoadTableAsync(reportType, from, to, stationId, ct);
        var rows = table.Rows.ToList();
        if (rows.Count > MaxRows) throw new ArgumentException($"Export result cannot exceed {MaxRows:N0} rows.");
        table = table with { Rows = rows };
        var normalizedFormat = format.Trim().ToLowerInvariant();
        var slug = reportType.Trim().ToLowerInvariant();
        var datePart = $"{from:yyyyMMdd}-{to:yyyyMMdd}";
        return normalizedFormat switch
        {
            "xlsx" => new(BuildXlsx(table), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"bus-{slug}-{datePart}.xlsx"),
            "pdf" => new(BuildPdf(table), "application/pdf", $"bus-{slug}-{datePart}.pdf"),
            "html" or "print" => new(Encoding.UTF8.GetBytes(BuildHtml(table)), "text/html; charset=utf-8", $"bus-{slug}-{datePart}.html"),
            _ => throw new ArgumentException("Supported export formats are xlsx, pdf and html.", nameof(format))
        };
    }

    private async Task<ExportTable> LoadTableAsync(string reportType, DateTime from, DateTime to, Guid? stationId, CancellationToken ct)
    {
        return reportType.Trim().ToLowerInvariant() switch
        {
            "revenue" => new ExportTable("Doanh thu bến xe",
                ["StationId", "SourceType", "BaseAmount", "AdjustmentAmount", "NetAmount", "ReceiptCount"],
                (await reports.GetRevenueReportAsync(from, to, stationId, ct)).Select(x => (IReadOnlyList<string>)[
                    x.StationId.ToString("D"), x.SourceType, Money(x.TotalAmount), Money(x.AdjustmentAmount), Money(x.NetAmount), x.ReceiptCount.ToString(Invariant)])),
            "departures" => new ExportTable("Chuyến xe",
                ["StationId", "BusinessDate", "Status", "TripCount", "PassengerCount"],
                (await reports.GetDepartureReportAsync(from, to, stationId, ct)).Select(x => (IReadOnlyList<string>)[
                    x.StationId.ToString("D"), x.BusinessDate.ToString("yyyy-MM-dd", Invariant), x.Status,
                    x.TripCount.ToString(Invariant), x.PassengerCount.ToString(Invariant)])),
            "reconciliation" => new ExportTable("Đối soát ca",
                ["StationId", "BusinessDate", "ShiftCode", "Status", "BaseRevenue", "RevenueAdjustment", "NetRevenue", "BaseExpense", "ExpenseAdjustment", "NetExpense"],
                (await reports.GetReconciliationReportAsync(from, to, stationId, ct)).Select(x => (IReadOnlyList<string>)[
                    x.StationId.ToString("D"), x.BusinessDate.ToString("yyyy-MM-dd", Invariant), x.ShiftCode, x.Status,
                    Money(x.TotalRevenue), Money(x.RevenueAdjustmentAmount), Money(x.NetRevenue), Money(x.TotalExpense),
                    Money(x.ExpenseAdjustmentAmount), Money(x.NetExpense)])),
            "compliance" => new ExportTable("Cảnh báo hồ sơ và hợp đồng",
                ["StationId", "ExpiringVehicleDocuments", "ExpiringCarrierContracts", "ExpiringLeases"],
                (await reports.GetComplianceReportAsync(stationId, to, ct)).Select(x => (IReadOnlyList<string>)[
                    x.StationId.ToString("D"), x.ExpiringDocumentCount.ToString(Invariant),
                    x.ExpiringContractCount.ToString(Invariant), x.ExpiringLeaseCount.ToString(Invariant)])),
            _ => throw new ArgumentException("Supported reports are revenue, departures, reconciliation and compliance.", nameof(reportType))
        };
    }

    private static string Money(decimal value) => value.ToString("0.00", Invariant);

    private static byte[] BuildXlsx(ExportTable table)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            var title = new Row();
            title.Append(new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(table.Title)) });
            sheetData.Append(title);
            var header = new Row();
            foreach (var value in table.Headers) header.Append(StringCell(value));
            sheetData.Append(header);
            foreach (var values in table.Rows)
            {
                var row = new Row();
                foreach (var value in values) row.Append(StringCell(value));
                sheetData.Append(row);
            }
            worksheetPart.Worksheet = new Worksheet(sheetData);
            worksheetPart.Worksheet.Save();
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet { Name = "Report", SheetId = 1, Id = workbookPart.GetIdOfPart(worksheetPart) });
            workbookPart.Workbook.Save();
        }
        return stream.ToArray();
    }

    private static Cell StringCell(string value) => new()
    {
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(value) { Space = SpaceProcessingModeValues.Preserve })
    };

    private static string BuildHtml(ExportTable table)
    {
        var builder = new StringBuilder("<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\"><title>");
        builder.Append(System.Net.WebUtility.HtmlEncode(table.Title));
        builder.Append("</title><style>body{font-family:Arial,sans-serif;color:#172033}table{border-collapse:collapse;width:100%;font-size:12px}th,td{border:1px solid #ccd3df;padding:6px;text-align:left}th{background:#eaf0f8}@media print{button{display:none}}</style></head><body>");
        builder.Append("<h1>").Append(System.Net.WebUtility.HtmlEncode(table.Title)).Append("</h1><button onclick=\"window.print()\">In</button><table><thead><tr>");
        foreach (var header in table.Headers) builder.Append("<th>").Append(System.Net.WebUtility.HtmlEncode(header)).Append("</th>");
        builder.Append("</tr></thead><tbody>");
        foreach (var row in table.Rows)
        {
            builder.Append("<tr>");
            foreach (var value in row) builder.Append("<td>").Append(System.Net.WebUtility.HtmlEncode(value)).Append("</td>");
            builder.Append("</tr>");
        }
        return builder.Append("</tbody></table></body></html>").ToString();
    }

    private static byte[] BuildPdf(ExportTable table)
    {
        const int linesPerPage = 46;
        var rows = table.Rows.ToList();
        var dataLines = rows.SelectMany(row => WrapPdfLine(string.Join(" | ", row.Select(PdfText)))).ToList();
        var pages = dataLines.Count == 0
            ? new List<IReadOnlyList<IReadOnlyList<string>>> { new List<IReadOnlyList<string>>() }
            : dataLines.Chunk(linesPerPage).Select(chunk => (IReadOnlyList<IReadOnlyList<string>>)chunk
                .Select(line => (IReadOnlyList<string>)[line]).ToList()).ToList();
        var pageObjectIds = pages.Select((_, index) => 5 + index * 2).ToArray();
        var contentObjectIds = pages.Select((_, index) => 6 + index * 2).ToArray();
        var kids = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>",
            "<< /Type /Font /Subtype /Type0 /BaseFont /Identity /Encoding /Identity-H /DescendantFonts [4 0 R] >>",
            "<< /Type /Font /Subtype /CIDFontType0 /BaseFont /Identity /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> >>"
        };
        for (var index = 0; index < pages.Count; index++)
        {
            var content = new StringBuilder("BT\n/F1 9 Tf\n40 760 Td\n");
            var lines = new List<string> { PdfText(table.Title), string.Join(" | ", table.Headers.Select(PdfText)) };
            lines.AddRange(pages[index].Select(row => string.Join(" | ", row)));
            foreach (var line in lines)
                content.Append('<').Append(PdfUnicodeHex(line)).Append("> Tj\n0 -14 Td\n");
            content.Append("ET\n");
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectIds[index]} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}endstream");
        }
        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position); WriteAscii(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = output.Position;
        WriteAscii(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) WriteAscii(output, $"{offset:0000000000} 00000 n \n");
        WriteAscii(output, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return output.ToArray();
    }

    private static string PdfText(string value) => value.Replace("\r", " ").Replace("\n", " ");

    private static IEnumerable<string> WrapPdfLine(string value, int maxLength = 90)
    {
        if (value.Length == 0) { yield return string.Empty; yield break; }
        for (var offset = 0; offset < value.Length; offset += maxLength)
            yield return value.Substring(offset, Math.Min(maxLength, value.Length - offset));
    }

    private static string PdfUnicodeHex(string value) => Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value));
    private static void WriteAscii(Stream stream, string value) => stream.Write(Encoding.ASCII.GetBytes(value));

    private sealed record ExportTable(string Title, IReadOnlyList<string> Headers, IEnumerable<IReadOnlyList<string>> Rows);
}
