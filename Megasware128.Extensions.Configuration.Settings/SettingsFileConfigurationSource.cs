namespace Megasware128.Extensions.Configuration.Settings;

public class SettingsFileConfigurationSource : IniConfigurationSource
{
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new SettingsFileConfigProvider(this);
    }
}
