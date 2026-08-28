using HCS.Samples;
using Xunit;

namespace HCS.EntityFrameworkCore.Domains;

[Collection(HCSTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<HCSEntityFrameworkCoreTestModule>
{

}
