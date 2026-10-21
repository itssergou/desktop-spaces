namespace DesktopSpaces.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length==2 && args[0]=="--read-desktop") { File.WriteAllText(args[1],new WindowsDesktopPath().Get());return; }
        if(args.Length==2 && args[0]=="--watchdog") { Watchdog.Run(args[1]); return; }
        if(args.Contains("--probe")) { RunProbe(args); return; }
        var demo=args.Contains("--demo");
        using var mutex=new System.Threading.Mutex(true,"Local\\DesktopSpaces"+(demo?".Demo":""),out var ownsMutex);
        if(!ownsMutex)
        {
            var hwnd=Native.FindWindowEx(new IntPtr(-3),IntPtr.Zero,null,CommandWindow.Title(demo));
            var switchArgument=args.FirstOrDefault(a=>a.StartsWith("--switch=",StringComparison.Ordinal));
            if(args.Contains("--exit"))Native.PostMessage(hwnd,CommandWindow.ExitMessage,IntPtr.Zero,IntPtr.Zero);
            else if(args.Contains("--status"))Native.PostMessage(hwnd,CommandWindow.StatusMessage,IntPtr.Zero,IntPtr.Zero);
            else if(switchArgument is not null && int.TryParse(switchArgument[9..],out var number))Native.PostMessage(hwnd,CommandWindow.SwitchMessage,new IntPtr(number-1),IntPtr.Zero);
            else Native.PostMessage(hwnd,CommandWindow.ShowSettingsMessage,IntPtr.Zero,IntPtr.Zero);
            return;
        }
        try
        {
            if(args.Contains("--exit") || args.Contains("--status"))return;
            if(args.Contains("--path-smoke")) { PathSmoke.Run();return; }
            if(args.Contains("--recover"))
            {
                var journal=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopSpaces","recovery.json");
                DesktopSession.Recover(new WindowsDesktopPath(),journal);return;
            }
            using var context=new DesktopApplication(demo);
            Application.Run(context);
        }
        catch(Exception ex)
        {
            var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopSpaces");
            DesktopApplication.Log(root,ex);
            MessageBox.Show(ex.Message,"DesktopSpaces — запуск остановлен",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        finally { mutex.ReleaseMutex(); }
    }

    private static void RunProbe(string[] args)
    {
        var root=DiagnosticRoot("probe",args);
        var data=Path.Combine(root,"data",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data,"Проверка.txt"),"Native Windows Shell view test");
        Directory.CreateDirectory(Path.Combine(data,"Тестовая папка"));
        var errors=new List<string>();
        using var form=new Form { Text="DesktopSpaces — проверка Windows Shell", Size=new Size(1080,720), StartPosition=FormStartPosition.CenterScreen };
        var view=new ShellFolderView(new Space { Name="Проверка",Folder=data },ex=>errors.Add(ex.ToString()));
        form.Controls.Add(view);
        using var timer=new System.Windows.Forms.Timer { Interval=3500 };
        timer.Tick+=(_,_) => {
            timer.Stop();
            var report=new Dictionary<string,object?> { ["Errors"]=errors };
            var folder=view.GetFolderView();
            if(folder is not null) { report["CountResult"]=folder.ItemCount(2,out var count);report["ItemCount"]=count;Marshal.ReleaseComObject(folder); }
            try { view.SaveLayout(); report["LayoutCount"]=view.Space.Layout.Count; }catch(Exception ex){errors.Add(ex.ToString());}
            var classes=new List<string>();
            Native.EnumChildWindows(form.Handle,(hwnd,_)=>{classes.Add(Native.ClassName(hwnd));return true;},IntPtr.Zero);
            report["WindowClasses"]=classes;
            report["Success"]=errors.Count==0 && (int?)report.GetValueOrDefault("ItemCount")==2 && view.Space.Layout.Count==2;
            if(report["Success"] is not true)Environment.ExitCode=1;
            JsonStore.Write(Path.Combine(root,"report.json"),report);
            if(!args.Contains("--keep-open"))form.Close();
        };
        form.Shown+=(_,_)=>timer.Start();
        Application.Run(form);
    }

    internal static string DiagnosticRoot(string category,string[]? args=null)
    {
        args??=Environment.GetCommandLineArgs();
        var option=args.FirstOrDefault(a=>a.StartsWith("--report-dir=",StringComparison.Ordinal));
        var root=option is null?Path.Combine(Path.GetTempPath(),"DesktopSpaces.Diagnostics",category):Path.GetFullPath(option[13..]);
        Directory.CreateDirectory(root);return root;
    }
}
