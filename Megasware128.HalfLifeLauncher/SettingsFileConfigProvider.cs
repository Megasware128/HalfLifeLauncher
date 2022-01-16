using System.Text;
using Microsoft.Extensions.Configuration.Ini;

class SettingsFileConfigProvider : IniConfigurationProvider
{
    private FileStream? _stream;

    public SettingsFileConfigProvider(IniConfigurationSource source) : base(source)
    {
    }

    public override void Set(string key, string value)
    {
        base.Set(key, value);

        if (_stream is null)
        {
            var path = Source.FileProvider.GetFileInfo(Source.Path).PhysicalPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _stream = File.OpenWrite(path);
            _stream.SetLength(0);
            _stream.Seek(0, SeekOrigin.Begin);
        }

        _stream.Write(Encoding.UTF8.GetBytes($"{key}={value}\n"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _stream?.Dispose();
    }
}
