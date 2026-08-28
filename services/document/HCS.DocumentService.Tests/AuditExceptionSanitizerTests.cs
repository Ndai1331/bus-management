using HCS.IntegrationEvents.Auditing;

namespace HCS.DocumentService.Tests;

public sealed class AuditExceptionSanitizerTests
{
    [Fact]
    public void Exception_details_are_not_exposed_to_audit_viewer()
    {
        var exception = new InvalidOperationException("password=should-never-appear");

        var result = AuditExceptionSanitizer.ToAuditValue(exception);

        Assert.Equal(AuditExceptionSanitizer.RequestFailed, result);
        Assert.DoesNotContain("password", result!, StringComparison.OrdinalIgnoreCase);
    }
}
