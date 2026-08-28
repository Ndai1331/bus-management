using Xunit;

namespace HCS.EntityFrameworkCore;

[CollectionDefinition(HCSTestConsts.CollectionDefinitionName)]
public class HCSEntityFrameworkCoreCollection : ICollectionFixture<HCSEntityFrameworkCoreFixture>
{

}
