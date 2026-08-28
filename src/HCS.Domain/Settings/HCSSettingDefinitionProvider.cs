using Volo.Abp.Settings;

namespace HCS.Settings;

public class HCSSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(HCSSettings.MySetting1));
    }
}
