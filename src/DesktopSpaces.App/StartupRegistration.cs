using Microsoft.Win32;

namespace DesktopSpaces.App;

internal static class StartupRegistration
{
    private const string RunPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovalPath=@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    internal static string Command(string executable)=>"\""+Path.GetFullPath(executable)+"\" --autostart";
    internal static bool IsEnabled()
    {
        using var key=Registry.CurrentUser.OpenSubKey(RunPath);
        return key?.GetValue("DesktopSpaces") is string command && command.EndsWith(" --autostart",StringComparison.Ordinal) && !IsDisabledByWindows();
    }
    private static bool IsDisabledByWindows()
    {
        using var key=Registry.CurrentUser.OpenSubKey(ApprovalPath);
        return key?.GetValue("DesktopSpaces") is byte[] state && state.Length>=4 && (state[0]==3 || state[0]==7);
    }
    public static void SetEnabled(bool enabled)
    {
        if(enabled && IsDisabledByWindows())
            throw new InvalidOperationException("Windows отключила DesktopSpaces в автозагрузке. Включите приложение в «Параметры Windows → Приложения → Автозагрузка», затем повторите.");
        using var key=Registry.CurrentUser.CreateSubKey(RunPath,true);
        if(enabled)
        {
            var executable=Environment.ProcessPath ?? throw new IOException("Не найден путь приложения.");
            if(!File.Exists(executable))throw new FileNotFoundException("Файл приложения не найден.",executable);
            var expected=Command(executable);
            key.SetValue("DesktopSpaces",expected,RegistryValueKind.String);
            if(key.GetValue("DesktopSpaces") as string!=expected)throw new IOException("Windows не сохранила запись автозагрузки.");
        }
        else
        {
            key.DeleteValue("DesktopSpaces",false);
            if(key.GetValue("DesktopSpaces") is not null)throw new IOException("Не удалось отключить автозагрузку.");
        }
    }
}
