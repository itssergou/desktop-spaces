namespace DesktopSpaces.App;

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class ShellFolderView : Panel, IExplorerBrowserEvents, ICommDlgBrowser, IServiceProviderNative,IMessageFilter
{
    private IExplorerBrowser? _browser;
    private uint _cookie;
    private bool _ready;
    private readonly Space _space;
    private readonly Action<Exception> _error;
    public event Action? Ready;
    public Space Space => _space;
    public bool IsReady => _ready;
    public Func<Bitmap>? BackgroundFactory { get; set; }

    public ShellFolderView(Space space, Action<Exception> error)
    {
        _space=space; _error=error;
        Dock=DockStyle.Fill;
        BackColor=Color.FromArgb(28,32,40);
        Application.AddMessageFilter(this);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            _browser=(IExplorerBrowser)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("71F96385-DDD6-48D3-A0C1-AE06E8B055FB"),true)!)!;
            Native.Check(_browser.SetOptions(0x4 | 0x8)); // no travel log, no border
            Native.Check(_browser.SetPropertyBag("DesktopSpaces."+_space.Id.ToString("N")));
            Native.Check(Native.IUnknown_SetSite(_browser,this));
            var rect=new Native.Rect(ClientRectangle);
            var settings=new Native.FolderSettings { ViewMode=1, Flags=0x20 | 0x100 | 0x800 | 0x100000 | 0x10000000 };
            Native.Check(_browser.Initialize(Handle,ref rect,ref settings));
            Native.Check(_browser.Advise(this,out _cookie));
            Native.Check(_browser.SetEmptyText("Дважды нажмите по пустому месту, чтобы сменить пространство"));
            Native.Check(Native.SHParseDisplayName(_space.Folder,IntPtr.Zero,out var pidl,0,out _));
            try { Native.Check(_browser.BrowseToIDList(pidl,0)); }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }
        catch(Exception ex) { _error(ex); }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if(_browser is null)return;
        var rect=new Native.Rect(ClientRectangle);
        _browser.SetRect(IntPtr.Zero,ref rect);
    }

    public IFolderView? GetFolderView()
    {
        if(_browser is null || !_ready)return null;
        var iid=typeof(IFolderView).GUID;
        return _browser.GetCurrentView(ref iid,out var obj)>=0 ? (IFolderView)obj : null;
    }

    private static string? ItemName(IFolderView view, IntPtr pidl)
    {
        var folderId=new Guid("000214E6-0000-0000-C000-000000000046");
        Native.Check(view.GetFolder(ref folderId,out var folder));
        try
        {
            var iid=typeof(IShellItem).GUID;
            Native.Check(Native.SHCreateItemWithParent(IntPtr.Zero,folder,pidl,ref iid,out var obj));
            try
            {
                var item=(IShellItem)obj;
                if(item.GetDisplayName(0x80080001,out var name)<0)return null;
                try { return Marshal.PtrToStringUni(name); }
                finally { Marshal.FreeCoTaskMem(name); }
            }
            finally { Marshal.ReleaseComObject(obj); }
        }
        finally { Marshal.ReleaseComObject(folder); }
    }

    public void SaveLayout()
    {
        var view=GetFolderView(); if(view is null)return;
        try
        {
            Native.Check(view.ItemCount(2,out var count));
            var positions=new Dictionary<string,IconPosition>(StringComparer.OrdinalIgnoreCase);
            for(var i=0;i<count;i++)
            {
                if(view.Item(i,out var pidl)<0)continue;
                try
                {
                    var name=ItemName(view,pidl);
                    if(name is not null && view.GetItemPosition(pidl,out var point)>=0)
                        positions[name]=new IconPosition(point.X,point.Y);
                }
                finally { Marshal.FreeCoTaskMem(pidl); }
            }
            _space.Layout=positions;
        }
        finally { Marshal.ReleaseComObject(view); }
    }

    public void RestoreLayout()
    {
        var view=GetFolderView(); if(view is null)return;
        var pidls=new List<IntPtr>(); var points=new List<Native.Point>();
        try
        {
            Native.Check(view.ItemCount(2,out var count));
            for(var i=0;i<count;i++)
            {
                if(view.Item(i,out var pidl)<0)continue;
                var retained=false;
                try
                {
                    var name=ItemName(view,pidl);
                    if(name is not null && _space.Layout.TryGetValue(name,out var p))
                    { pidls.Add(pidl); points.Add(new Native.Point(p.X,p.Y)); retained=true; }
                }
                finally { if(!retained)Marshal.FreeCoTaskMem(pidl); }
            }
            if(pidls.Count>0)Native.Check(view.SelectAndPositionItems((uint)pidls.Count,pidls.ToArray(),points.ToArray(),0x80));
        }
        finally { foreach(var p in pidls)Marshal.FreeCoTaskMem(p); Marshal.ReleaseComObject(view); }
    }

    public bool IsEmptyPoint(System.Drawing.Point screenPoint)
        => IsEmptyPointAt(Handle,screenPoint);

    public static bool IsEmptyPointAt(IntPtr root,System.Drawing.Point screenPoint)
    {
        // FWF_DESKTOP normally exposes a classic list view. Only send pointer-bearing
        // messages to a control owned by THIS process, never to Explorer's list view.
        var list=IntPtr.Zero;
        Native.EnumChildWindows(root,(hwnd,_) => { if(Native.ClassName(hwnd)=="SysListView32" && Native.IsWindowVisible(hwnd)){list=hwnd;return false;}return true;},IntPtr.Zero);
        if(list!=IntPtr.Zero)
        {
            Native.GetWindowThreadProcessId(list,out var pid);
            if(pid==(uint)Environment.ProcessId)
            {
                var p=new Native.Point(screenPoint.X,screenPoint.Y); Native.ScreenToClient(list,ref p);
                var hit=new Native.HitTest { Point=p };
                return Native.SendMessage(list,0x1012,IntPtr.Zero,ref hit).ToInt64()<0;
            }
        }
        try
        {
            var element=System.Windows.Automation.AutomationElement.FromPoint(new System.Windows.Point(screenPoint.X,screenPoint.Y));
            for(var depth=0;element is not null && depth<12;depth++)
            {
                var type=element.Current.ControlType;
                if(type==System.Windows.Automation.ControlType.ListItem || type==System.Windows.Automation.ControlType.DataItem ||
                   type==System.Windows.Automation.ControlType.MenuItem || type==System.Windows.Automation.ControlType.Button)return false;
                if(element.Current.NativeWindowHandle==root.ToInt64())return true;
                if(type==System.Windows.Automation.ControlType.List && element.Current.NativeWindowHandle!=0 &&
                   Native.IsChild(root,new IntPtr(element.Current.NativeWindowHandle)))return true;
                element=System.Windows.Automation.TreeWalker.ControlViewWalker.GetParent(element);
            }
        }
        catch(Exception ex) when(ex is System.Windows.Automation.ElementNotAvailableException or InvalidOperationException) { return false; }
        return false;
    }

    public int OnNavigationPending(IntPtr pidl) => _ready ? unchecked((int)0x80004004) : 0;
    public int OnViewCreated(object view) => 0;
    public int OnNavigationFailed(IntPtr pidl) { _error(new IOException("Windows не смогла открыть папку пространства.")); return 0; }
    public int OnNavigationComplete(IntPtr pidl)
    {
        _ready=true;
        BeginInvoke((Action)(() => { try { UpdateBackground();RestoreLayout();Ready?.Invoke(); } catch(Exception ex) { _error(ex); } }));
        return 0;
    }
    public int OnStateChange(IntPtr view,uint change) => 0;
    public int IncludeObject(IntPtr view,IntPtr pidl) => 0;
    public int OnDefaultCommand(IntPtr shellView)
    {
        try
        {
            var view=GetFolderView(); if(view is null)return 1;
            try
            {
                var iid=typeof(IShellItemArray).GUID;
                if(view.Items(1,ref iid,out var obj)<0)return 1;
                try
                {
                    var array=(IShellItemArray)obj; Native.Check(array.GetCount(out var count));
                    for(uint i=0;i<count;i++)
                    {
                        Native.Check(array.GetItemAt(i,out var item));
                        try
                        {
                            if(item.GetDisplayName(0x80058000,out var name)<0)continue;
                            try
                            {
                                var path=Marshal.PtrToStringUni(name);
                                if(path is not null)Process.Start(new ProcessStartInfo(path) { UseShellExecute=true });
                            }
                            finally { Marshal.FreeCoTaskMem(name); }
                        }
                        finally { Marshal.ReleaseComObject(item); }
                    }
                }
                finally { Marshal.ReleaseComObject(obj); }
            }
            finally { Marshal.ReleaseComObject(view); }
            return 0;
        }
        catch(Exception ex) { _error(ex); return 0; }
    }
    public int QueryService(ref Guid service,ref Guid iid,out IntPtr result)
    {
        result=IntPtr.Zero;
        if(iid!=typeof(ICommDlgBrowser).GUID)return unchecked((int)0x80004002);
        result=Marshal.GetComInterfaceForObject(this,typeof(ICommDlgBrowser)); return 0;
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing)Application.RemoveMessageFilter(this);
        if(_browser is not null)
        {
            _browser.Unadvise(_cookie); _browser.Destroy();
            Native.IUnknown_SetSite(_browser,null);
            Marshal.ReleaseComObject(_browser); _browser=null;
        }
        base.Dispose(disposing);
    }

    public bool PreFilterMessage(ref Message message)
    {
        if(_browser is null || !_ready || !Visible || message.Msg is not (0x100 or 0x104) ||
           !Native.IsChild(Handle,message.HWnd))return false;
        var iid=typeof(IShellView).GUID;
        if(_browser.GetCurrentView(ref iid,out var obj)<0)return false;
        try
        {
            var msg=new Native.Msg { Hwnd=message.HWnd,Message=(uint)message.Msg,WParam=message.WParam,LParam=message.LParam,Time=Native.GetMessageTime() };
            return ((IShellView)obj).TranslateAccelerator(ref msg)==0;
        }
        finally { Marshal.ReleaseComObject(obj); }
    }

    public void UpdateBackground()
    {
        if(!_ready || BackgroundFactory is null)return;
        var list=IntPtr.Zero;
        Native.EnumChildWindows(Handle,(h,_)=>{if(Native.ClassName(h)=="SysListView32"){list=h;return false;}return true;},IntPtr.Zero);
        if(list==IntPtr.Zero)return;
        using var image=BackgroundFactory();
        var native=new Native.BackgroundImage { Flags=1,Bitmap=image.GetHbitmap() };
        if(Native.SendMessage(list,0x108A,IntPtr.Zero,ref native)==IntPtr.Zero)Native.DeleteObject(native.Bitmap);
        Native.SendMessage(list,0x1026,IntPtr.Zero,new IntPtr(-1)); // transparent label background
    }

    public void FocusView()
    {
        if(_browser is null || !_ready)return;
        var iid=typeof(IShellView).GUID;
        if(_browser.GetCurrentView(ref iid,out var obj)<0)return;
        try
        {
            var view=(IShellView)obj;view.UIActivate(1);
            if(view.GetWindow(out var hwnd)>=0)
            {
                var list=Native.FindWindowEx(hwnd,IntPtr.Zero,"SysListView32",null);
                Native.SetFocus(list==IntPtr.Zero?hwnd:list);
            }
        }
        finally { Marshal.ReleaseComObject(obj); }
    }
}
