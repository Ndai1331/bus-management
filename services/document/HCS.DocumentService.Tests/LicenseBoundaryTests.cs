namespace HCS.DocumentService.Tests;

public sealed class LicenseBoundaryTests
{
    [Fact]
    public void DocumentService_project_does_not_depend_on_sibling_licensed_source_tree()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../HCS.DocumentService/HCS.DocumentService.csproj"));
        var projectFile = File.ReadAllText(projectPath);

        Assert.DoesNotContain("HCS_web_with_license", projectFile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runtime_references_keep_abp_boundary_and_include_approved_signing_providers()
    {
        var references = typeof(HcsDocumentServiceModule).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToList();
        Assert.DoesNotContain(references, name => name.Contains(string.Concat("Volo.Abp.", "Commercial"), StringComparison.Ordinal)
            || name.Split('.').Contains("Pro", StringComparer.Ordinal));
        Assert.Contains(references, name => name.Contains("Bnn.SignLib", StringComparison.Ordinal));
        Assert.Contains(references, name => name.Contains("Bnn.Sdk", StringComparison.Ordinal));
    }
}
