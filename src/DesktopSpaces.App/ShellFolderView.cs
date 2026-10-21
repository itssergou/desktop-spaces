namespace DesktopSpaces.App;

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class ShellFolderView : Panel, IExplorerBrowserEvents, ICommDlgBrowser, IServiceProviderNative,IMessageFilter
{
    private IExplorerBrowser? _browser;
    private uint _cookie;
    private bool _ready;
    private IntPtr _configuredList;
    private IntPtr _listHandle;
    private IntPtr _proxyList;
    public IDropTargetNative? DropProxy { get; set; }
    private readonly Space _space;
    private readonly Action<Exception> _error;
    private readonly Dictionary<string,string?> _itemNames=new();
    private readonly System.Windows.Forms.Timer _placementTimer=new() {Interval=75};
    private readonly Queue<(HashSet<string> Names,Native.Point Point,DateTime Until)> _placements=new();
    public event Action? Ready;
    public Space Space => _space;
    public bool IsReady => _ready;
    public Func<Bitmap>? BackgroundFactory { get; set; }
    public bool LiveBackground { get; set; }
    public IntPtr ListHandle
    {
        get
        {
            if(_listHandle!=IntPtr.Zero && Native.IsWindow(_listHandle))return _listHandle;
            var list=IntPtr.Zero;
            if(IsHandleCreated)Native.EnumChildWindows(Handle,(h,_)=>{if(Native.ClassName(h)=="SysListView32"){list=h;return false;}return true;},IntPtr.Zero);
            return _listHandle=list;
        }
    }

    public ShellFolderView(Space space, Action<Exception> error)
    {
        _space=space; _error=error;
        Dock=DockStyle.Fill;
        BackColor=Color.FromArgb(28,32,40);
        Application.AddMessageFilter(this);
        _placementTimer.Tick+=(_,_)=> {
            if(_placements.Count==0){_placementTimer.Stop();return;}
            var next=_placements.Peek();
            // Shell can finish its own asynchronous placement after the file first
            // appears. Keep checking through that completion, rather than treating
            // the first successful HRESULT as the final icon position.
            try { PositionItems(next.Names,next.Point);if(DateTime.UtcNow>=next.Until)_placements.Dequeue(); }
            catch(Exception ex) { _placements.Dequeue();_error(ex); }
            if(_placements.Count==0)_placementTimer.Stop();
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            _browser=(IExplorerBrowser)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("71F96385-DDD6-48D3-A0C1-AE06E8B055FB"),true)!)!;
            Native.Check(_browser.SetOptions(0x1 | 0x8 | 0x40 | 0x80)); // navigate once, no travel log/border/persisted browser state
            Native.Check(_browser.SetPropertyBag("DesktopSpaces."+_space.Id.ToString("N")));
            Native.Check(Native.IUnknown_SetSite(_browser,this));
            var rect=new Native.Rect(ClientRectangle);
            var settings=new Native.FolderSettings { ViewMode=1, Flags=0x20 | 0x800 | 0x100000 | 0x10000000 };
            Native.Check(_browser.Initialize(Handle,ref rect,ref settings));
            Native.Check(_browser.Advise(this,out _cookie));
            Native.Check(_browser.SetEmptyText("Дважды нажмите по пустому месту, чтобы сменить пространство"));
            // Force the volume / This PC namespace. A Desktop-relative PIDL changes
            // meaning when SHSetKnownFolderPath redirects Desktop during a drag.
            var parsingName=_space.Folder.Length>2 && _space.Folder[1]==':'
                ? "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}\\"+_space.Folder : _space.Folder;
            Native.Check(Native.SHParseDisplayName(parsingName,IntPtr.Zero,out var pidl,0,out _));
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

    internal static string? ItemName(IFolderView view, IntPtr pidl)
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
        MaintainView();
        CaptureLayout();
    }

    private void CaptureLayout()
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
                    var name=CachedItemName(view,pidl);
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
                    var name=CachedItemName(view,pidl);
                    if(name is not null && _space.Layout.TryGetValue(name,out var p))
                    { pidls.Add(pidl); points.Add(new Native.Point(p.X,p.Y)); retained=true; }
                }
                finally { if(!retained)Marshal.FreeCoTaskMem(pidl); }
            }
            if(pidls.Count>0)Native.Check(view.SelectAndPositionItems((uint)pidls.Count,pidls.ToArray(),points.ToArray(),0x80));
        }
        finally { foreach(var p in pidls)Marshal.FreeCoTaskMem(p); Marshal.ReleaseComObject(view); }
    }

    private string? CachedItemName(IFolderView view,IntPtr pidl)
    {
        var size=checked((int)Native.ILGetSize(pidl));var bytes=new byte[size];Marshal.Copy(pidl,bytes,0,size);
        var key=Convert.ToHexString(bytes);
        if(_itemNames.TryGetValue(key,out var name))return name;
        if(_itemNames.Count>=4096)_itemNames.Clear();
        return _itemNames[key]=ItemName(view,pidl);
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
            // MSAA crosses process boundaries safely and reports CHILDID_SELF (0)
            // for the real desktop background. UIA's ControlView can skip DefView.
            object? accessible=null;
            try
            {
                var iid=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
                if(Native.AccessibleObjectFromWindow(list,0xFFFFFFFC,ref iid,out accessible)>=0)
                {
                    var hit=((Accessibility.IAccessible)accessible).accHitTest(screenPoint.X,screenPoint.Y);
                    return hit is null || hit is int child && child==0;
                }
            }
            catch(COMException) { }
            finally { if(accessible is not null && Marshal.IsComObject(accessible))Marshal.ReleaseComObject(accessible); }
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
        BeginInvoke((Action)(() => {
            if(IsDisposed)return;
            try {
                var iid=typeof(IFolderView2).GUID;
                if(_browser!.GetCurrentView(ref iid,out var obj)>=0)
                {
                    try { Native.Check(((IFolderView2)obj).SetCurrentFolderFlags(1,0)); }
                    finally { Marshal.ReleaseComObject(obj); }
                }
                _ready=true;UpdateBackground();InstallDropProxy();RestoreLayout();RepairOverlaps();Ready?.Invoke();Ready=null;
            } catch(Exception ex) { _error(ex); }
        }));
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
        if(disposing)_placementTimer.Dispose();
        if(_browser is not null)
        {
            if(_proxyList!=IntPtr.Zero && Native.IsWindow(_proxyList))Native.RevokeDragDrop(_proxyList);
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
        if(!_ready)return;
        var list=ListHandle;
        if(list==IntPtr.Zero)return;
        _configuredList=list;
        if(LiveBackground)
        {
            // Theme notifications also arrive when the Desktop path changes.
            // Do not invalidate a whole layered desktop when its colour is intact.
            if(Native.SendMessage(list,0x1000,IntPtr.Zero,IntPtr.Zero).ToInt64()==DesktopHost.TransparentColorRef)return;
            BackColor=DesktopHost.TransparentColor;
            var clear=new Native.BackgroundImage();
            Native.SendMessage(list,0x108A,IntPtr.Zero,ref clear);
            Native.SendMessage(list,0x1001,IntPtr.Zero,new IntPtr(DesktopHost.TransparentColorRef));
            Native.SendMessage(list,0x1026,IntPtr.Zero,new IntPtr(-1));
            Native.SendMessage(list,0x1036,new IntPtr(0x10000),new IntPtr(0x10000)); // LVS_EX_DOUBLEBUFFER
            return;
        }
        if(BackgroundFactory is null)return;
        using var image=BackgroundFactory();
        var native=new Native.BackgroundImage { Flags=1,Bitmap=image.GetHbitmap() };
        if(Native.SendMessage(list,0x108A,IntPtr.Zero,ref native)==IntPtr.Zero)Native.DeleteObject(native.Bitmap);
        Native.SendMessage(list,0x1026,IntPtr.Zero,new IntPtr(-1)); // transparent label background
    }

    public void MaintainView()
    {
        if(!_ready || IsDisposed)return;
        var list=ListHandle;if(list==IntPtr.Zero)return;
        InstallDropProxy();
        var autoArrange=(Native.GetWindowLongPtr(list,-16).ToInt64()&0x100)!=0; // LVS_AUTOARRANGE
        if(_configuredList!=list || autoArrange)
        {
            var iid=typeof(IFolderView2).GUID;
            if(_browser!.GetCurrentView(ref iid,out var obj)>=0)
            {
                try { Native.Check(((IFolderView2)obj).SetCurrentFolderFlags(1,0)); }
                finally { Marshal.ReleaseComObject(obj); }
            }
            UpdateBackground();RestoreLayout();
        }
        else if(LiveBackground && Native.SendMessage(list,0x1000,IntPtr.Zero,IntPtr.Zero).ToInt64()!=DesktopHost.TransparentColorRef)
            UpdateBackground();
        RepairOverlaps();
    }

    private void RepairOverlaps()
    {
        if((Native.GetAsyncKeyState(1)&0x8000)!=0)return;
        CaptureLayout();
        var view=GetFolderView();if(view is null)return;
        Native.Point spacing;
        try { Native.Check(view.GetSpacing(out spacing)); }
        finally { Marshal.ReleaseComObject(view); }
        var sx=spacing.X>=16?spacing.X:SystemInformation.IconSpacingSize.Width;
        var sy=spacing.Y>=16?spacing.Y:SystemInformation.IconSpacingSize.Height;
        var occupied=new List<IconPosition>();
        foreach(var (name,p) in _space.Layout.ToArray())
        {
            if(occupied.Any(q=>Math.Abs((long)p.X-q.X)<sx && Math.Abs((long)p.Y-q.Y)<sy))
            {
                var point=new Native.Point(p.X+40,p.Y+24);
                Native.MapWindowPoints(ListHandle,IntPtr.Zero,ref point,1);
                PositionItems(new HashSet<string>(StringComparer.OrdinalIgnoreCase){name},point);
                occupied.Add(_space.Layout.GetValueOrDefault(name,p));
            }
            else occupied.Add(p);
        }
    }

    private void InstallDropProxy()
    {
        var list=ListHandle;
        if(DropProxy is null || list==IntPtr.Zero || list==_proxyList)return;
        Native.RevokeDragDrop(list);
        Native.Check(Native.RegisterDragDrop(list,DropProxy));_proxyList=list;
    }

    public IDropTargetNative? GetDropTarget()
    {
        if(_browser is null || !_ready)return null;
        var iid=typeof(IDropTargetNative).GUID;
        if(_browser.GetCurrentView(ref iid,out var direct)>=0)return (IDropTargetNative)direct;
        iid=typeof(IShellView).GUID;
        if(_browser.GetCurrentView(ref iid,out var obj)<0)return null;
        try
        {
            iid=typeof(IDropTargetNative).GUID;
            return ((IShellView)obj).GetItemObject(0,ref iid,out var target)>=0 ? (IDropTargetNative)target : null;
        }
        finally { Marshal.ReleaseComObject(obj); }
    }

    public bool ContainsFiles(string[] files)=>files.Length>0 && files.All(path=>Paths.Equal(Path.GetDirectoryName(path)!,_space.Folder));

    public bool IsFolderTarget(Native.Point screenPoint)
    {
        var list=ListHandle;if(list==IntPtr.Zero)return false;
        Native.ScreenToClient(list,ref screenPoint);var hit=new Native.HitTest {Point=screenPoint};
        var index=(int)Native.SendMessage(list,0x1012,IntPtr.Zero,ref hit);
        if(index<0)return false;
        var view=GetFolderView();if(view is null)return false;
        try
        {
            if(view.Item(index,out var pidl)<0)return false;
            try { var name=CachedItemName(view,pidl);return name is not null && Directory.Exists(Path.Combine(_space.Folder,name)); }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }
        finally { Marshal.ReleaseComObject(view); }
    }

    public bool MoveIcons(string[] files,Native.Point screenPoint)
    {
        if(!ContainsFiles(files))return false;
        return PositionItems(files.Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase),screenPoint);
    }

    public Action? PrepareIncomingPlacement(string[] files,Native.Point point)
    {
        var names=files.Select(Path.GetFileName).OfType<string>()
            .Where(n=>!File.Exists(Path.Combine(_space.Folder,n)) && !Directory.Exists(Path.Combine(_space.Folder,n)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(names.Count==0)return null;
        return ()=> { _placements.Enqueue((names,point,DateTime.UtcNow.AddSeconds(5)));_placementTimer.Start(); };
    }

    public Native.Point FreeDropPoint(string[] files,Native.Point point)
    {
        if(files.Length==0 || IsFolderTarget(point))return point;
        var view=GetFolderView();if(view is null)return point;
        try
        {
            Native.Check(view.ItemCount(2,out var count));var occupied=new List<IconPosition>();
            for(var i=0;i<count;i++)
            {
                Native.Check(view.Item(i,out var pidl));
                try { if(view.GetItemPosition(pidl,out var p)>=0 && Math.Abs((long)p.X)<1000000 && Math.Abs((long)p.Y)<1000000)occupied.Add(new IconPosition(p.X,p.Y)); }
                finally { Marshal.FreeCoTaskMem(pidl); }
            }
            Native.Check(view.GetSpacing(out var spacing));
            var sx=spacing.X>=16?spacing.X:SystemInformation.IconSpacingSize.Width;
            var sy=spacing.Y>=16?spacing.Y:SystemInformation.IconSpacingSize.Height;
            var client=point;Native.ScreenToClient(ListHandle,ref client);Native.GetClientRect(ListHandle,out var bounds);
            var placement=IconPlacement.FindFreeGroup([new IconPosition(0,0)],occupied,new IconPosition(client.X-40,client.Y-24),sx,sy,bounds.Right,bounds.Bottom);
            if(placement is null)return point;
            var result=new Native.Point(placement[0].X+40,placement[0].Y+24);
            Native.MapWindowPoints(ListHandle,IntPtr.Zero,ref result,1);
            TransferTrace.Write("drop-position",new {Original=new {point.X,point.Y},Target=new {result.X,result.Y},SpacingX=sx,SpacingY=sy});
            return result;
        }
        finally { Marshal.ReleaseComObject(view); }
    }

    private bool PositionItems(HashSet<string> names,Native.Point screenPoint)
    {
        if((Native.GetAsyncKeyState(1)&0x8000)!=0)return false;
        var view=GetFolderView();if(view is null)return false;
        var pidls=new List<IntPtr>();var positions=new List<Native.Point>();
        var occupied=new List<IconPosition>();
        try
        {
            Native.Check(view.ItemCount(2,out var count));
            for(var i=0;i<count;i++)
            {
                Native.Check(view.Item(i,out var pidl));var keep=false;
                try {
                    if(CachedItemName(view,pidl) is string name && names.Contains(name) && view.GetItemPosition(pidl,out var position)>=0)
                    { pidls.Add(pidl);positions.Add(position);keep=true; }
                    else if(view.GetItemPosition(pidl,out var other)>=0 && Math.Abs((long)other.X)<1000000 && Math.Abs((long)other.Y)<1000000)
                        occupied.Add(new IconPosition(other.X,other.Y));
                }
                finally { if(!keep)Marshal.FreeCoTaskMem(pidl); }
            }
            if(pidls.Count!=names.Count)return false;
            Native.ScreenToClient(ListHandle,ref screenPoint);
            view.GetSpacing(out var spacing);
            var origin=positions[0];
            Native.GetClientRect(ListHandle,out var bounds);
            var offsets=positions.Select(p=>new IconPosition(p.X-origin.X,p.Y-origin.Y)).ToArray();
            var placement=IconPlacement.FindFreeGroup(offsets,occupied,new IconPosition(screenPoint.X-40,screenPoint.Y-24),spacing.X>=16?spacing.X:SystemInformation.IconSpacingSize.Width,spacing.Y>=16?spacing.Y:SystemInformation.IconSpacingSize.Height,bounds.Right,bounds.Bottom);
            if(placement is null)return false;
            if(positions.Select(p=>new IconPosition(p.X,p.Y)).SequenceEqual(placement))return true;
            var points=placement.Select(p=>new Native.Point(p.X,p.Y)).ToArray();
            Native.Check(view.SelectAndPositionItems((uint)pidls.Count,pidls.ToArray(),points,0x80|1|4));
            var actual=pidls.Select(p=> {Native.Check(view.GetItemPosition(p,out var value));return new IconPosition(value.X,value.Y);}).ToArray();
            TransferTrace.Write("icons-positioned",new {Names=names,Before=positions.Select(p=>new IconPosition(p.X,p.Y)).ToArray(),Requested=placement,Actual=actual});
            // Capturing must not call MaintainView/RestoreLayout and undo this move.
            CaptureLayout();return actual.SequenceEqual(placement);
        }
        finally { foreach(var pidl in pidls)Marshal.FreeCoTaskMem(pidl);Marshal.ReleaseComObject(view); }
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
