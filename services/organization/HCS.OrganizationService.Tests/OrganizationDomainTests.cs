using HCS.OrganizationService.Domain;
using Shouldly;
using Volo.Abp;

namespace HCS.OrganizationService.Tests;

public sealed class OrganizationDomainTests
{
    [Fact]
    public void Department_cannot_be_its_own_parent()
    {
        var id = Guid.NewGuid();
        var exception = Should.Throw<BusinessException>(() =>
            new Department(id, "IT", "Information Technology", id, 0));
        exception.Code.ShouldBe(OrganizationErrorCodes.DepartmentCannotBeOwnParent);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Position_rejects_sign_order_outside_supported_range(int signOrder)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new Position(Guid.NewGuid(), "HEAD", "Department Head", signOrder, 0));
    }

    [Fact]
    public void Codes_and_names_are_trimmed_at_the_domain_boundary()
    {
        var unit = new Unit(Guid.NewGuid(), Guid.NewGuid(), "  ICU  ", "  Intensive Care  ", 1);
        unit.Code.ShouldBe("ICU");
        unit.Name.ShouldBe("Intensive Care");
    }
}
