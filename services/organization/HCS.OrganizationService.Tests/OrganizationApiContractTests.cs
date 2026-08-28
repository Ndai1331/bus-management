using HCS.OrganizationService.Contracts;
using HCS.OrganizationService.Host.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace HCS.OrganizationService.Tests;

public sealed class OrganizationApiContractTests
{
    public static TheoryData<Type, string, string> Routes => new()
    {
        { typeof(DepartmentsController), "api/organization/departments", OrganizationPermissions.Departments },
        { typeof(UnitsController), "api/organization/units", OrganizationPermissions.Units },
        { typeof(PositionsController), "api/organization/positions", OrganizationPermissions.Positions },
        { typeof(MasterDataController), "api/organization/master-data", OrganizationPermissions.MasterData },
        { typeof(UserMappingsController), "api/organization/user-mappings", OrganizationPermissions.UserMappings }
    };

    [Fact]
    public void User_department_lookup_is_read_only_and_permission_scoped()
    {
        var controller = typeof(UserDepartmentLookupController);
        controller.GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>().Single().Template
            .ShouldBe("api/organization/user-departments");
        controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy
            .ShouldBeNull();
        controller.GetMethods().Count(x => x.GetCustomAttributes(typeof(HttpGetAttribute), true).Length > 0).ShouldBe(1);
        controller.GetMethods().Count(x => x.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0).ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Controllers_keep_gateway_routes_and_permission_boundaries(Type controller, string route, string permission)
    {
        controller.GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>().Single().Template.ShouldBe(route);
        controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy.ShouldBe(permission);
        controller.GetCustomAttributes(typeof(IgnoreAntiforgeryTokenAttribute), true).Count().ShouldBe(1);
        controller.GetMethods().Count(x => x.GetCustomAttributes(typeof(HttpGetAttribute), true).Length > 0).ShouldBe(1);
        controller.GetMethods().Count(x => x.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0).ShouldBe(1);
        controller.GetMethods().Count(x => x.GetCustomAttributes(typeof(HttpPutAttribute), true).Length > 0).ShouldBe(1);
        controller.GetMethods().Count(x => x.GetCustomAttributes(typeof(HttpDeleteAttribute), true).Length > 0).ShouldBe(1);
    }
}
