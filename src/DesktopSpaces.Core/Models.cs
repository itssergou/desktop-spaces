namespace DesktopSpaces.Core;

public sealed class Space
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Пространство";
    public string Folder { get; set; } = "";
    public Dictionary<string, IconPosition> Layout { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record IconPosition(int X, int Y);

public sealed class Preferences
{
    public bool DoubleClickSwitch { get; set; } = true;
    public bool HotkeysEnabled { get; set; } = true;
    public bool UseControlAlt { get; set; }
    public bool ShowIndicator { get; set; } = true;
    public bool AutoStart { get; set; }
}

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public string OriginalDesktop { get; set; } = "";
    public List<Space> Spaces { get; set; } = [];
    public Guid ActiveSpaceId { get; set; }
    public Preferences Preferences { get; set; } = new();

    public static AppConfig Create(string desktop, string storage)
    {
        var first = new Space { Name = "Рабочий стол", Folder = Path.GetFullPath(desktop) };
        var second = new Space { Name = "Пространство 2" };
        second.Folder = Path.Combine(storage, second.Id.ToString("N"));
        return new AppConfig { OriginalDesktop = first.Folder, Spaces = [first, second], ActiveSpaceId = first.Id };
    }

    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("Неподдерживаемая версия настроек.");
        if (Spaces is null || Spaces.Count is < 1 or > 99)
            throw new InvalidDataException("Допустимо от 1 до 99 пространств.");
        if (Preferences is null) throw new InvalidDataException("Отсутствуют настройки приложения.");
        if (string.IsNullOrWhiteSpace(OriginalDesktop) || !Path.IsPathFullyQualified(OriginalDesktop)) throw new InvalidDataException("Не задан исходный рабочий стол.");
        if (Spaces.Any(s => s is null || s.Id == Guid.Empty || string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 80 ||
                            string.IsNullOrWhiteSpace(s.Folder) || !Path.IsPathFullyQualified(s.Folder) || s.Layout is null))
            throw new InvalidDataException("Некорректное пространство.");
        if (!Paths.Equal(Spaces[0].Folder, OriginalDesktop))
            throw new InvalidDataException("Первое пространство должно быть исходным рабочим столом.");
        if (Spaces.Select(s => s.Id).Distinct().Count() != Spaces.Count)
            throw new InvalidDataException("Повторяющиеся идентификаторы пространств.");
        for (var i = 0; i < Spaces.Count; i++)
        for (var j = i + 1; j < Spaces.Count; j++)
            if (Paths.Overlap(Spaces[i].Folder, Spaces[j].Folder))
                throw new InvalidDataException("Папки пространств не должны совпадать или быть вложенными.");
        if (Spaces.All(s => s.Id != ActiveSpaceId)) ActiveSpaceId = Spaces[0].Id;
    }

    public Space AddSpace(string name, string storage)
    {
        if (Spaces.Count >= 99) throw new InvalidOperationException("Достигнут предел: 99 пространств.");
        name = name.Trim();
        if (name.Length is < 1 or > 80) throw new ArgumentException("Название должно содержать от 1 до 80 символов.");
        var space = new Space { Name = name };
        space.Folder = Path.Combine(storage, space.Id.ToString("N"));
        Directory.CreateDirectory(space.Folder);
        Spaces.Add(space);
        return space;
    }
}

public static class Paths
{
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    public static bool Equal(string a, string b) => StringComparer.OrdinalIgnoreCase.Equals(Normalize(a), Normalize(b));
    public static bool Overlap(string a, string b)
    {
        a = Normalize(a); b = Normalize(b);
        var aPrefix=Path.EndsInDirectorySeparator(a)?a:a+Path.DirectorySeparatorChar;
        var bPrefix=Path.EndsInDirectorySeparator(b)?b:b+Path.DirectorySeparatorChar;
        return Equal(a, b) || a.StartsWith(bPrefix, StringComparison.OrdinalIgnoreCase) ||
               b.StartsWith(aPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
