namespace DesktopSpaces.Core;

public interface IDesktopPath
{
    string Get();
    void Set(string path);
}

public sealed class RecoveryRecord
{
    public string OriginalPath { get; set; } = "";
    public List<string> OwnedPaths { get; set; } = [];
    public int ProcessId { get; set; }
    public long ProcessStartTicks { get; set; }
    public bool RestoreDesktopIcons { get; set; }
}

public sealed class DesktopSession(IDesktopPath desktop, string journal, Action<RecoveryRecord>? restoreSurface=null)
{
    private RecoveryRecord? _record;
    private readonly object _sync=new();

    public void RememberDesktopIcons(bool visible)
    {
        lock(_sync)
        {
            var record=_record ?? throw new InvalidOperationException("Сеанс не запущен.");
            if(!visible || record.RestoreDesktopIcons)return;
            record.RestoreDesktopIcons=true;
            JsonStore.Write(journal,record);
        }
    }

    public void Start(string original, int processId, long processStartTicks)
    { lock(_sync) StartCore(original,processId,processStartTicks); }

    private void StartCore(string original, int processId, long processStartTicks)
    {
        if (File.Exists(journal)) throw new InvalidOperationException("Сначала необходимо восстановить предыдущий сеанс.");
        if (!Paths.Equal(desktop.Get(), original))
            throw new InvalidOperationException("Путь рабочего стола изменён другой программой. Переключение остановлено.");
        var record = new RecoveryRecord { OriginalPath = original, OwnedPaths = [original],
            ProcessId = processId, ProcessStartTicks = processStartTicks };
        JsonStore.Write(journal, record);
        _record = record;
    }

    public void Switch(string target)
    { lock(_sync) SwitchCore(target); }

    private void SwitchCore(string target)
    {
        var record = _record ?? throw new InvalidOperationException("Сеанс не запущен.");
        target = Path.GetFullPath(target);
        if (!Directory.Exists(target)) throw new DirectoryNotFoundException(target);
        var previous = desktop.Get();
        if (!record.OwnedPaths.Any(p => Paths.Equal(p, previous)))
            throw new InvalidOperationException("Путь рабочего стола изменён извне. Переключение остановлено.");
        if (Paths.Equal(previous, target)) return;
        // Persist BOTH possible paths before mutating Windows. A crash at either side is recoverable.
        if (!record.OwnedPaths.Any(p => Paths.Equal(p, target))) record.OwnedPaths.Add(target);
        JsonStore.Write(journal, record);
        try
        {
            desktop.Set(target);
            if (!Paths.Equal(desktop.Get(), target)) throw new IOException("Windows не подтвердила новый путь рабочего стола.");
        }
        catch (Exception switchError)
        {
            try
            {
                var actual = desktop.Get();
                if (Paths.Equal(actual, target)) desktop.Set(previous);
                else if (!Paths.Equal(actual, previous))
                    throw new IOException("Путь изменён другой программой во время переключения.");
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException("Не удалось переключить и восстановить путь. Используйте восстановление.", switchError, rollbackError);
            }
            throw;
        }
    }

    public void Restore()
    { lock(_sync) RestoreCore(); }

    private void RestoreCore()
    {
        if (!File.Exists(journal)) { _record = null; return; }
        Recover(desktop, journal,restoreSurface:restoreSurface);
        _record = null;
    }

    public static bool Recover(IDesktopPath desktop, string journal, int? expectedProcessId=null, long? expectedStartTicks=null, Action<RecoveryRecord>? restoreSurface=null)
    {
        // The watchdog and a newly launched app may recover the same journal together.
        // Serialize them and never let an old watchdog restore a newer session.
        var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Paths.Normalize(journal).ToUpperInvariant())));
        using var mutex=new Mutex(false,"DesktopSpaces.Recovery."+hash);
        var acquired=false;
        try
        {
            try { acquired=mutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch(AbandonedMutexException) { acquired=true; }
            if(!acquired)throw new TimeoutException("Восстановление уже выполняется другим процессом.");
            return RecoverCore(desktop,journal,expectedProcessId,expectedStartTicks,restoreSurface);
        }
        finally { if(acquired)mutex.ReleaseMutex(); }
    }

    private static bool RecoverCore(IDesktopPath desktop,string journal,int? expectedProcessId,long? expectedStartTicks,Action<RecoveryRecord>? restoreSurface)
    {
        if (!File.Exists(journal)) return false;
        var record = JsonStore.Read<RecoveryRecord>(journal);
        if(expectedProcessId.HasValue && (record.ProcessId!=expectedProcessId || record.ProcessStartTicks!=expectedStartTicks))return false;
        if (string.IsNullOrWhiteSpace(record.OriginalPath) || !Directory.Exists(record.OriginalPath) ||
            record.OwnedPaths is null || !record.OwnedPaths.Any(p => Paths.Equal(p, record.OriginalPath)))
            throw new InvalidDataException("Журнал восстановления некорректен. Автоматическое восстановление отменено.");
        var current = desktop.Get();
        if (!record.OwnedPaths.Any(p => Paths.Equal(p, current)))
            throw new InvalidOperationException("Рабочий стол изменён другой программой. Его путь не был перезаписан.");
        if (!Paths.Equal(current, record.OriginalPath)) desktop.Set(record.OriginalPath);
        if (!Paths.Equal(desktop.Get(), record.OriginalPath)) throw new IOException("Не удалось восстановить исходный рабочий стол.");
        restoreSurface?.Invoke(record);
        File.Delete(journal);
        if (File.Exists(journal + ".bak")) File.Delete(journal + ".bak");
        return true;
    }
}
