using Microsoft.Win32;
namespace DesktopSpaces.App;

internal static class StartupProbe
{
    public static void Run(string[] args)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);
        var original=key.GetValue("DesktopSpaces");
        var kind=original is null?RegistryValueKind.String:key.GetValueKind("DesktopSpaces");
        var disabled=false;var enabled=false;var restored=false;
        try
        {
            StartupRegistration.SetEnabled(false);disabled=key.GetValue("DesktopSpaces") is null;
            StartupRegistration.SetEnabled(true);
            enabled=(key.GetValue("DesktopSpaces") as string)==StartupRegistration.Command(Environment.ProcessPath!) && StartupRegistration.IsEnabled();
        }
        finally
        {
            if(original is null)key.DeleteValue("DesktopSpaces",false);else key.SetValue("DesktopSpaces",original,kind);
            restored=Equals(original,key.GetValue("DesktopSpaces"));
            JsonStore.Write(Path.Combine(Program.DiagnosticRoot("startup",args),"report.json"),new { DisableVerified=disabled,EnableVerified=enabled,OriginalRestored=restored,RebootTestPending=true });
        }
        if(!disabled || !enabled || !restored)throw new IOException("Проверка автозапуска не пройдена.");
    }
}
