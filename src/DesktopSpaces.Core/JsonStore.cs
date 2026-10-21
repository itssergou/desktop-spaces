using System.Text.Json;

namespace DesktopSpaces.Core;

public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static T Read<T>(string path) where T : class
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        return JsonSerializer.Deserialize<T>(stream, Options) ?? throw new InvalidDataException("Пустой файл настроек.");
    }

    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, Options);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static AppConfig LoadConfig(string path)
    {
        try
        {
            var config = Read<AppConfig>(path);
            config.Validate();
            return config;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or FileNotFoundException)
        {
            // Never silently create a fresh config: it could forget the original desktop.
            var backup = Read<AppConfig>(path + ".bak");
            backup.Validate();
            return backup;
        }
    }
}
