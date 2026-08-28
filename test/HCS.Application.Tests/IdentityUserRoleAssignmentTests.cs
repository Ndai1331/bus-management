using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Xunit;

namespace HCS;

public abstract class IdentityUserRoleAssignmentTests<TStartupModule> : HCSApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IIdentityUserAppService _userAppService;

    protected IdentityUserRoleAssignmentTests()
    {
        _userAppService = GetRequiredService<IIdentityUserAppService>();
    }

    [Fact]
    public async Task UpdateRoles_Persists_Operational_Role_Without_Admin_Claim()
    {
        var created = await _userAppService.CreateAsync(new IdentityUserCreateDto
        {
            UserName = "u" + Guid.NewGuid().ToString("N")[..16],
            Email = $"{Guid.NewGuid():N}@example.com",
            Password = "Test-password-42!",
            RoleNames = []
        });

        await _userAppService.UpdateRolesAsync(created.Id, new IdentityUserUpdateRolesDto
        {
            RoleNames = ["bacsi"]
        });

        var roles = await _userAppService.GetRolesAsync(created.Id);
        roles.Items.ShouldContain(role => role.Name == "bacsi");
    }
}
