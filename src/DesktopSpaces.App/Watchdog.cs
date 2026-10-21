namespace DesktopSpaces.App;

internal static class Watchdog
{
    public static void Run(string journal)
    {
        var expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopSpaces","recovery.json");
        if(!Paths.Equal(journal,expected))return;
        try
        {
            var record=JsonStore.Read<RecoveryRecord>(journal);
            try
            {
                using var parent=Process.GetProcessById(record.ProcessId);
                if(parent.StartTime.ToUniversalTime().Ticks==record.ProcessStartTicks)
                {
                    while(!parent.WaitForExit(1000))if(!File.Exists(journal))return;
                }
            }
            catch(ArgumentException) { }
            DesktopSession.Recover(new WindowsDesktopPath(),journal,record.ProcessId,record.ProcessStartTicks);
        }
        catch(Exception ex) { DesktopApplication.Log(Path.GetDirectoryName(journal)!,ex); }
    }
}

internal sealed class CommandWindow : NativeWindow,IDisposable
{
    public const uint ShowSettingsMessage=0x8000+11;
    public const uint ExitMessage=0x8000+12;
    public const uint SwitchMessage=0x8000+13;
    public const uint StatusMessage=0x8000+14;
    private readonly Action _show;
    private readonly Action _exit;
    private readonly Action<int> _switch;
    private readonly Action _status;
    public static string Title(bool demo)=>"DesktopSpaces.CommandWindow"+(demo?".Demo":"");
    public CommandWindow(bool demo,Action show,Action exit,Action<int> select,Action status)
    {
        _show=show;_exit=exit;_switch=select;_status=status;CreateHandle(new CreateParams { Caption=Title(demo),Parent=new IntPtr(-3) });
    }
    protected override void WndProc(ref Message message)
    {
        if((uint)message.Msg==ShowSettingsMessage){_show();return;}
        if((uint)message.Msg==ExitMessage){_exit();return;}
        if((uint)message.Msg==SwitchMessage){_switch(message.WParam.ToInt32());return;}
        if((uint)message.Msg==StatusMessage){_status();return;}
        base.WndProc(ref message);
    }
    public void Dispose()=>DestroyHandle();
}
