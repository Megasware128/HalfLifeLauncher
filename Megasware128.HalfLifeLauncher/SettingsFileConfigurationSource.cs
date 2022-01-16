using Microsoft.Extensions.Configuration.Ini;

class SettingsFileConfigurationSource : IniConfigurationSource
{
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new SettingsFileConfigProvider(this);
    }
}
