using Microsoft.Extensions.FileProviders;

namespace Megasware128.Extensions.Configuration.Settings;

public static class SettingsConfigurationExtensions
{
    public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, string path)
    {
        return AddSettingsFile(builder, provider: null, path: path, optional: false, reloadOnChange: false);
    }

    public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, string path, bool optional)
    {
        return AddSettingsFile(builder, provider: null, path: path, optional: optional, reloadOnChange: false);
    }

    public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, string path, bool optional, bool reloadOnChange)
    {
        return AddSettingsFile(builder, provider: null, path: path, optional: optional, reloadOnChange: reloadOnChange);
    }

    public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, IFileProvider? provider, string path, bool optional, bool reloadOnChange)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        return builder.AddIniFile(s =>
        {
            s.FileProvider = provider;
            s.Path = path;
            s.Optional = optional;
            s.ReloadOnChange = reloadOnChange;
            s.ResolveFileProvider();
        });
    }

    public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, Action<IniConfigurationSource> configureSource)
        => builder.Add(configureSource);
}
