namespace DesktopSpaces.App;

internal static class PathSmoke
{
    public static void Run()
    {
        var root=Program.DiagnosticRoot("path-smoke");
        var target=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(target);
        var journal=Path.Combine(AppStorage.Root,"recovery.json");
        var desktop=new WindowsDesktopPath();var original=desktop.Get();
        var report=new Dictionary<string,object?> { ["OriginalPath"]=original,["TargetPath"]=target };
        var session=new DesktopSession(desktop,journal);
        try
        {
            using var process=Process.GetCurrentProcess();
            session.Start(original,process.Id,process.StartTime.ToUniversalTime().Ticks);
            var watchdog=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--watchdog",journal} };
            using var guard=Process.Start(watchdog) ?? throw new IOException("Watchdog failed to start");
            session.Switch(target);
            report["NativePathChanged"]=Paths.Equal(desktop.Get(),target);
            var readback=Path.Combine(root,"child-desktop.txt");
            var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--read-desktop",readback} };
            using var child=Process.Start(start) ?? throw new IOException("Readback failed to start");
            if(!child.WaitForExit(10000))throw new TimeoutException("Readback timed out");
            var childPath=File.ReadAllText(readback);report["FreshProcessPath"]=childPath;
            if(!Paths.Equal(childPath,target))throw new IOException("A fresh process did not see the redirected desktop");
            report["Success"]=true;
        }
        catch(Exception ex) { report["Success"]=false;report["Error"]=ex.ToString();Environment.ExitCode=1; }
        finally
        {
            try { session.Restore();report["OriginalRestored"]=Paths.Equal(desktop.Get(),original); }
            catch(Exception ex){report["RestoreError"]=ex.ToString();Environment.ExitCode=1;}
            JsonStore.Write(Path.Combine(root,"report.json"),report);
        }
    }
}
