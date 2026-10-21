namespace DesktopSpaces.App;

internal sealed class DesktopHost : Form
{
    private readonly bool _demo;
    private readonly Dictionary<Guid,ShellFolderView> _views=new();
    private readonly Label _indicator=new() { AutoSize=true, BackColor=Color.FromArgb(35,40,50), ForeColor=Color.White,
        Padding=new Padding(16,10,16,10), Font=new Font("Segoe UI",12,FontStyle.Bold), Visible=false };
    private readonly System.Windows.Forms.Timer _indicatorTimer=new() { Interval=1200 };
    public IntPtr DesktopParent { get; private set; }
    public ShellFolderView? ActiveView { get; private set; }
    public bool Demo => _demo;
    public bool NativeDesktopActive { get; private set; }
    public Guid ActiveSpaceId { get; private set; }

    public DesktopHost(bool demo)
    {
        _demo=demo;
        Text=demo ? "DesktopSpaces — безопасная демонстрация" : "DesktopSpaces";
        ShowInTaskbar=demo && Environment.GetCommandLineArgs().Contains("--ui-test");
        FormBorderStyle=demo ? FormBorderStyle.Sizable : FormBorderStyle.None;
        if(!demo)TopLevel=false;
        BackColor=Color.FromArgb(25,30,40);
        StartPosition=demo ? FormStartPosition.CenterScreen : FormStartPosition.Manual;
        Size=new Size(1100,740);
        Controls.Add(_indicator);
        _indicatorTimer.Tick+=(_,_)=>{_indicator.Hide();_indicatorTimer.Stop();};
    }

    public void Attach()
    {
        if(_demo) { Show(); return; }
        DesktopParent=Native.FindDesktopView();
        if(DesktopParent==IntPtr.Zero)throw new IOException("Не найден рабочий стол Проводника. Перезапустите приложение после запуска Проводника.");
        var handle=Handle;
        Native.SetWindowLongPtr(handle,-16,new IntPtr((Native.GetWindowLongPtr(handle,-16).ToInt64() | 0x40000000L) & ~0x80000000L));
        Marshal.SetLastPInvokeError(0);
        var previous=Native.SetParent(handle,DesktopParent);
        if(previous==IntPtr.Zero && Marshal.GetLastPInvokeError()!=0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        FitDesktop();
    }

    public void FitDesktop()
    {
        if(!_demo && Native.IsWindow(DesktopParent) && Native.GetClientRect(DesktopParent,out var rect))
            Native.SetWindowPos(Handle,IntPtr.Zero,0,0,rect.Right,rect.Bottom,0x0010);
    }

    public ShellFolderView Prepare(Space space,Action<Exception> error,Action ready)
    {
        if(_views.TryGetValue(space.Id,out var existing)) { if(existing.IsReady)ready();else existing.Ready+=ready;return existing; }
        var view=new ShellFolderView(space,error) { Visible=false,BackgroundFactory=CreateBackground };
        _views.Add(space.Id,view);
        view.Ready+=ready;
        Controls.Add(view);
        _=view.Handle;
        return view;
    }

    public void ActivateSpace(Guid id,string name,bool showIndicator)
    {
        var next=_views[id];
        NativeDesktopActive=false;ActiveSpaceId=id;
        Show();
        next.Show();next.BringToFront();
        if(!_demo)Native.SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x0040|0x0010|0x0001|0x0002);
        if(ActiveView is not null && ActiveView!=next && (Native.GetAsyncKeyState(1)&0x8000)==0)ActiveView.Hide();
        ActiveView=next;
        if(showIndicator)
        {
            _indicator.Text=name;
            _indicator.Location=new Point(Math.Max(16,ClientSize.Width-_indicator.PreferredWidth-28),24);
            _indicator.Show();_indicator.BringToFront();_indicatorTimer.Stop();_indicatorTimer.Start();
        }
        Text=_demo ? "DesktopSpaces — демонстрация · "+name : "DesktopSpaces";
        if((Native.GetAsyncKeyState(1)&0x8000)==0)next.FocusView();
    }

    public bool ContainsPoint(Point p)
    {
        if(IsDisposed)return false;
        var window=Native.WindowFromPoint(new Native.Point(p.X,p.Y));
        if(NativeDesktopActive)return window==DesktopParent || Native.IsChild(DesktopParent,window);
        if(!Visible)return false;
        return window==Handle || Native.IsChild(Handle,window);
    }

    public void ActivateNativeDesktop(Guid id)
    {
        ActiveSpaceId=id;NativeDesktopActive=true;ActiveView=null;
        if((Native.GetAsyncKeyState(1)&0x8000)!=0)
            Native.SetWindowPos(Handle,new IntPtr(1),0,0,0,0,0x0010|0x0001|0x0002);
        else Hide();
    }

    public bool IsEmptyPoint(Point p) => NativeDesktopActive ? ShellFolderView.IsEmptyPointAt(DesktopParent,p) : ActiveView?.IsEmptyPoint(p)==true;

    public bool IsDesktopForeground()
    {
        var foreground=Native.GetForegroundWindow();
        return foreground==Handle || Native.IsChild(Handle,foreground) ||
               (!_demo && Native.GetAncestor(Handle,2)==foreground);
    }

    public void SaveLayouts() { foreach(var view in _views.Values)view.SaveLayout(); }
    public void Forget(Guid id) { if(_views.Remove(id,out var view)){Controls.Remove(view);view.Dispose();} }

    private Bitmap CreateBackground()
    {
        var screen=RectangleToScreen(ClientRectangle);
        return WallpaperRenderer.Render(screen,_demo);
    }

    public void RefreshBackgrounds() { foreach(var view in _views.Values)view.UpdateBackground(); }

    public void MaintainViews()
    {
        if((Native.GetAsyncKeyState(1)&0x8000)!=0)return;
        if(NativeDesktopActive)Hide();
        foreach(var view in _views.Values)if(view!=ActiveView)view.Hide();
        foreach(var view in _views.Values.Where(v=>v!=ActiveView).Take(Math.Max(0,_views.Count-3)).ToArray())
        { view.SaveLayout();Forget(view.Space.Id); }
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing) { _indicatorTimer.Dispose();foreach(var view in _views.Values)view.Dispose();_views.Clear(); }
        base.Dispose(disposing);
    }
}
