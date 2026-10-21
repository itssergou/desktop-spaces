using Microsoft.Win32;

namespace DesktopSpaces.App;

internal sealed class DesktopApplication : ApplicationContext
{
    private readonly bool _demo;
    private readonly string _root,_configPath,_journal,_spacesRoot;
    private readonly IDesktopPath _desktop;
    private readonly AppConfig _config;
    private readonly DesktopSession _session;
    private readonly NotifyIcon _tray;
    private readonly Icon _trayIcon;
    private readonly System.Windows.Forms.Timer _health=new() { Interval=1500 };
    private readonly System.Windows.Forms.Timer _saveTimer=new() { Interval=15000 };
    private DesktopHost _host=null!;
    private InputHooks? _hooks;
    private SettingsForm? _settings;
    private bool _switching,_stopping;
    private Guid _active;
    private readonly CommandWindow _commands;

    public DesktopApplication(bool demo)
    {
        _demo=demo;
        _root=demo ? Path.Combine(Path.GetTempPath(),"DesktopSpaces.Demo") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopSpaces");
        _configPath=Path.Combine(_root,"settings.json");_journal=Path.Combine(_root,"recovery.json");_spacesRoot=Path.Combine(_root,"Spaces");
        Directory.CreateDirectory(_root);
        if(demo)
        {
            var first=Path.Combine(_root,"DemoDesktop");Directory.CreateDirectory(first);
            if(!File.Exists(_configPath))
            {
                File.WriteAllText(Path.Combine(first,"Добро пожаловать.txt"),"Это отдельная демонстрация. Настоящий рабочий стол не меняется.");
                Directory.CreateDirectory(Path.Combine(first,"Пример папки"));
            }
            _desktop=new DemoDesktopPath(first);
        }
        else _desktop=new WindowsDesktopPath();
        DesktopSession.Recover(_desktop,_journal);
        if(!demo)CheckDesktopCompatibility(_desktop.Get());
        _config=File.Exists(_configPath) || File.Exists(_configPath+".bak") ? JsonStore.LoadConfig(_configPath) : AppConfig.Create(_desktop.Get(),_spacesRoot);
        foreach(var space in _config.Spaces)
        {
            if(space==_config.Spaces[0] && !Directory.Exists(space.Folder))throw new DirectoryNotFoundException("Исходный рабочий стол недоступен.");
            Directory.CreateDirectory(space.Folder);
        }
        SaveConfig();
        _session=new DesktopSession(_desktop,_journal);
        using var process=Process.GetCurrentProcess();
        _session.Start(_config.OriginalDesktop,process.Id,process.StartTime.ToUniversalTime().Ticks);
        using(var iconStream=typeof(DesktopApplication).Assembly.GetManifestResourceStream("DesktopSpaces.Icon")!)
            _trayIcon=new Icon(iconStream,32,32);
        _tray=new NotifyIcon { Text="DesktopSpaces", Icon=_trayIcon, Visible=true };
        _tray.DoubleClick+=(_,_)=>ShowSettings();
        _tray.MouseClick+=(_,e)=> { if(e.Button==MouseButtons.Left)ShowSettings(); };
        _commands=new CommandWindow(demo,ShowSettings,Stop,Switch,WriteStatus);
        BuildMenu();
        try
        {
            if(!demo)StartWatchdog();
            CreateHost();
            _health.Tick+=(_,_)=>CheckHealth();_health.Start();
            _saveTimer.Tick+=(_,_)=> { try { _host.SaveLayouts();SaveConfig(); } catch(Exception ex){Report(ex);} };_saveTimer.Start();
            Application.ThreadException+=OnThreadException;
            SystemEvents.DisplaySettingsChanged+=OnDisplayChanged;
            SystemEvents.SessionEnding+=OnSessionEnding;
            SystemEvents.UserPreferenceChanged+=OnPreferenceChanged;
            Switch(0);
            if(demo)_host.BeginInvoke((Action)ShowSettings);
        }
        catch { _tray.Dispose();_host?.Dispose();_hooks?.Dispose();_session.Restore();throw; }
    }

    private void CreateHost()
    {
        _host=new DesktopHost(_demo);
        _host.FormClosing+=(_,e)=> { if(!_stopping){e.Cancel=true;Stop();} };
        _host.Attach();
        _hooks=new InputHooks(_host,_config.Preferences,Switch,()=>Switch((_config.Spaces.FindIndex(s=>s.Id==_active)+1)%_config.Spaces.Count));
        if(_hooks.ConflictingNumbers.Count>0)_tray.ShowBalloonTip(5000,"Сочетание занято","Не используются номера: "+string.Join(", ",_hooks.ConflictingNumbers)+". Выберите другой модификатор в настройках.",ToolTipIcon.Warning);
    }

    public void Switch(int index)
    {
        if(_stopping || _switching || index<0 || index>=_config.Spaces.Count)return;
        var space=_config.Spaces[index];
        if(_active==space.Id && (_host.ActiveView is not null || _host.NativeDesktopActive))return;
        _switching=true;
        try
        {
            _host.ActiveView?.SaveLayout();
            if(!_demo && index==0)
            {
                _session.Switch(space.Folder);
                _host.ActivateNativeDesktop(space.Id);
                _active=_config.ActiveSpaceId=space.Id;
                SaveConfig();BuildMenu();_tray.Text="DesktopSpaces · "+space.Name[..Math.Min(space.Name.Length,45)];
                _switching=false;return;
            }
            _host.Prepare(space,ex=>{_switching=false;Report(ex);},()=> {
                if(_stopping)return;
                try
                {
                    _session.Switch(space.Folder);
                    _host.ActivateSpace(space.Id,$"{index+1}  ·  {space.Name}",_config.Preferences.ShowIndicator);
                    _active=_config.ActiveSpaceId=space.Id;
                    SaveConfig();BuildMenu();
                    _tray.Text=("DesktopSpaces · "+space.Name)[..Math.Min(63,("DesktopSpaces · "+space.Name).Length)];
                }
                catch(Exception ex){Report(ex);}
                finally{_switching=false;}
            });
        }
        catch(Exception ex){_switching=false;Report(ex);}
    }

    private void BuildMenu()
    {
        var menu=new ContextMenuStrip();
        for(var i=0;i<_config.Spaces.Count;i++)
        {
            var index=i;
            var shortcut=i<9 && _config.Preferences.HotkeysEnabled ? (_config.Preferences.UseControlAlt ? "Ctrl+Alt+" : "Alt+")+(i+1) : "";
            var item=new ToolStripMenuItem($"{i+1}. {_config.Spaces[i].Name}") { Checked=_active==_config.Spaces[i].Id, ShortcutKeyDisplayString=shortcut };
            item.Click+=(_,_)=>Switch(index);menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Открыть папку пространства",null,(_,_)=>OpenFolder(_config.Spaces.FirstOrDefault(s=>s.Id==_active)?.Folder ?? _config.OriginalDesktop));
        menu.Items.Add("Папки дополнительных пространств",null,(_,_)=>OpenFolder(_spacesRoot));
        menu.Items.Add("Настройки…",null,(_,_)=>ShowSettings());
        menu.Items.Add("Выйти и вернуть первый рабочий стол",null,(_,_)=>Stop());
        var previous=_tray.ContextMenuStrip;_tray.ContextMenuStrip=menu;previous?.Dispose();
    }

    private void ShowSettings()
    {
        if(_settings is {IsDisposed:false}) {_settings.Activate();return;}
        _settings=new SettingsForm(_config,_demo,AddSpace,RemoveSpace,MoveSpace,Switch,SavePreferences,Report);
        _settings.Show();_settings.Activate();
    }
    private void AddSpace(string name) { _config.AddSpace(name,_spacesRoot);SaveConfig();BuildMenu(); }
    private void RemoveSpace(Guid id)
    {
        if(id==_config.Spaces[0].Id)throw new InvalidOperationException("Первое пространство нельзя исключить.");
        if(_switching)throw new InvalidOperationException("Дождитесь завершения переключения.");
        if(_active==id)throw new InvalidOperationException("Сначала переключитесь на другое пространство.");
        var space=_config.Spaces.Single(s=>s.Id==id);
        _host.Forget(id);_config.Spaces.Remove(space);SaveConfig();BuildMenu();
        OpenFolder(space.Folder);
    }
    private void MoveSpace(Guid id,int offset)
    {
        var index=_config.Spaces.FindIndex(s=>s.Id==id);var next=index+offset;
        if(index<=0 || next<=0 || next>=_config.Spaces.Count)return;
        (_config.Spaces[index],_config.Spaces[next])=(_config.Spaces[next],_config.Spaces[index]);SaveConfig();BuildMenu();
    }
    private void SavePreferences()
    {
        if(!_demo)
        {
            using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if(_config.Preferences.AutoStart)key.SetValue("DesktopSpaces",'"'+Environment.ProcessPath+'"'+" --autostart");
            else key.DeleteValue("DesktopSpaces",false);
        }
        SaveConfig();BuildMenu();
        var conflicts=_hooks?.RefreshConflicts();
        if(conflicts?.Count>0)_tray.ShowBalloonTip(5000,"Сочетание занято","Не используются номера: "+string.Join(", ",conflicts)+". Выберите другой модификатор в настройках.",ToolTipIcon.Warning);
    }
    private void SaveConfig() { _config.Validate();JsonStore.Write(_configPath,_config); }
    private void WriteStatus()
    {
        try
        {
            JsonStore.Write(Path.Combine(_root,"status.json"),new {
                ProcessId=Environment.ProcessId,ActiveSpaceId=_active, DesktopPath=_desktop.Get(), Demo=_demo,
                Attached=_demo || Native.GetParent(_host.Handle)==_host.DesktopParent,
                NativeDesktopActive=_host.NativeDesktopActive,
                SurfaceVisible=Native.IsWindowVisible(_host.Handle),
                FolderViewReady=_host.ActiveView?.IsReady==true,
                MonitorCount=Screen.AllScreens.Length
            });
        }
        catch(Exception ex){Report(ex);}
    }
    private static void CheckDesktopCompatibility(string desktop)
    {
        foreach(var name in new[]{"OneDrive","OneDriveConsumer","OneDriveCommercial"})
        {
            var root=Environment.GetEnvironmentVariable(name);
            if(!string.IsNullOrWhiteSpace(root) && (Paths.Equal(root,desktop) || Paths.Normalize(desktop).StartsWith(Paths.Normalize(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))
                throw new NotSupportedException("Рабочий стол управляется OneDrive. Предварительная версия не меняет привязку синхронизируемого рабочего стола. Можно запустить --demo.");
        }
        if(desktop.Split(Path.DirectorySeparatorChar).Any(s=>s.Equals("OneDrive",StringComparison.OrdinalIgnoreCase) || s.StartsWith("OneDrive - ",StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException("Для рабочего стола в OneDrive пока доступна только демонстрация --demo.");
        if(desktop.StartsWith(@"\\",StringComparison.Ordinal))
            throw new NotSupportedException("Сетевой рабочий стол пока не поддерживается. Можно запустить --demo.");
        foreach(var hive in new[]{"HKEY_CURRENT_USER","HKEY_LOCAL_MACHINE"})
        {
            var policy=Registry.GetValue(hive+@"\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer","DisableKnownFolderRedirection",0);
            if(policy is int value && value!=0)throw new NotSupportedException("Политика Windows запрещает перенаправление рабочих папок. Можно запустить --demo.");
        }
    }
    internal static void OpenFolder(string path) => Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList={path},UseShellExecute=true });

    private void StartWatchdog()
    {
        var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("--watchdog");start.ArgumentList.Add(_journal);
        using var watchdog=Process.Start(start) ?? throw new IOException("Не удалось запустить восстановление после сбоя.");
    }
    private void CheckHealth()
    {
        if(_stopping)return;
        try { if(!_host.IsDisposed)_host.MaintainViews(); }catch(Exception ex){Report(ex);}
        if(_demo)return;
        if(!_host.IsDisposed && Native.IsWindow(_host.DesktopParent) && Native.IsWindow(_host.Handle)){_host.FitDesktop();return;}
        if(Native.FindDesktopView()==IntPtr.Zero)return;
        try
        {
            _hooks?.Dispose();_host.Dispose();_switching=false;CreateHost();
            var index=_config.Spaces.FindIndex(s=>s.Id==_active);_active=Guid.Empty;Switch(Math.Max(0,index));
        }
        catch(Exception ex){Report(ex);Stop();}
    }
    private void OnDisplayChanged(object? sender,EventArgs e) { if(!_host.IsDisposed)_host.BeginInvoke((Action)(()=>{_host.FitDesktop();_host.RefreshBackgrounds();})); }
    private void OnPreferenceChanged(object sender,UserPreferenceChangedEventArgs e)
    {
        if(e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.Color or UserPreferenceCategory.General)
            if(!_host.IsDisposed)_host.BeginInvoke((Action)_host.RefreshBackgrounds);
    }
    private void OnThreadException(object sender,System.Threading.ThreadExceptionEventArgs e) { Report(e.Exception);Stop(); }
    private void OnSessionEnding(object sender,SessionEndingEventArgs e)
    {
        try { _session.Restore(); }
        catch(Exception ex) { Log(_root,ex); }
        if(!_host.IsDisposed && _host.IsHandleCreated)_host.BeginInvoke((Action)Stop);
    }

    private void Report(Exception ex)
    {
        Log(_root,ex);
        if(!_stopping)_tray.ShowBalloonTip(5000,"DesktopSpaces",ex.Message,ToolTipIcon.Warning);
    }
    internal static void Log(string root,Exception ex)
    {
        try { var path=Path.Combine(root,"last-error.log");File.WriteAllText(path,DateTimeOffset.Now+Environment.NewLine+ex); }
        catch(IOException) { }
        catch(UnauthorizedAccessException) { }
    }
    private void Stop()
    {
        if(_stopping)return;_stopping=true;
        _health.Stop();_saveTimer.Stop();_hooks?.Dispose();
        try { _host.SaveLayouts(); }catch(Exception ex){Log(_root,ex);}
        try { _session.Restore(); }
        catch(Exception ex)
        {
            Log(_root,ex);
            MessageBox.Show("Не удалось вернуть исходный путь рабочего стола.\n"+ex.Message+"\nЖурнал сохранён: "+_journal,"DesktopSpaces — восстановление",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
        _config.ActiveSpaceId=_config.Spaces[0].Id;
        try{SaveConfig();}catch(Exception ex){Log(_root,ex);}
        _settings?.Dispose();_host.Dispose();_tray.Visible=false;_tray.Dispose();_trayIcon.Dispose();
        Application.ThreadException-=OnThreadException;SystemEvents.DisplaySettingsChanged-=OnDisplayChanged;SystemEvents.SessionEnding-=OnSessionEnding;SystemEvents.UserPreferenceChanged-=OnPreferenceChanged;
        _commands.Dispose();_health.Dispose();_saveTimer.Dispose();ExitThread();
    }
}
