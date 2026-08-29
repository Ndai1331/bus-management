using System.Reflection;
using System.Text;
using HCS.BusManagementService.Application;

namespace HCS.BusManagementService.Tests;

public sealed class ReportExportTests
{
    [Fact]
    public void Pdf_export_splits_large_reports_into_multiple_pages()
    {
        var tableType = typeof(BusReportExportService).GetNestedType("ExportTable", BindingFlags.NonPublic)!;
        var table = Activator.CreateInstance(
            tableType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [
                "Report",
                (IReadOnlyList<string>)["Id", "Amount"],
                Enumerable.Range(1, 100).Select(index => (IReadOnlyList<string>)[index.ToString(), "100.00"]).ToList()
            ],
            culture: null)!;
        var buildPdf = typeof(BusReportExportService).GetMethod("BuildPdf", BindingFlags.NonPublic | BindingFlags.Static)!;

        var pdf = (byte[])buildPdf.Invoke(null, [table])!;
        var text = Encoding.ASCII.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.Contains("/Count 3", text, StringComparison.Ordinal);
        Assert.Contains("5 0 obj", text, StringComparison.Ordinal);
        Assert.Contains("9 0 obj", text, StringComparison.Ordinal);
    }
}
