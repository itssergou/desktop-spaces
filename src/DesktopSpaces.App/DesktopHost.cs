namespace DesktopSpaces.App;

internal sealed class DesktopHost : Form
{
    private readonly bool _demo;
    private readonly Dictionary<Guid,ShellFolderView> _views=new();
    internal static readonly Color TransparentColor=Color.FromArgb(1,2,3);
    internal const uint TransparentColorRef=0x030201;
    private DesktopInputSurface? _input;
    private bool _originalIconsVisible;
    private bool _ownsNativeSurface;
    private bool _focusNativeAfterDrag;
    private readonly Action<bool> _rememberIcons;
    private Size _desktopSize;
    private readonly Label _indicator=new() { AutoSize=true, BackColor=Color.FromArgb(35,40,50), ForeColor=Color.White,
        Padding=new Padding(16,10,16,10), Font=new Font("Segoe UI",12,FontStyle.Bold), Visible=false };
    private readonly System.Windows.Forms.Timer _indicatorTimer=new() { Interval=1200 };
    public IntPtr DesktopParent { get; private set; }
    public ShellFolderView? ActiveView { get; private set; }
    public bool Demo => _demo;
    public bool NativeDesktopActive { get; private set; }
    public bool IsPrepared(Guid id)=>_views.ContainsKey(id);
    public Guid ActiveSpaceId { get; private set; }
    public Func<System.Runtime.InteropServices.ComTypes.IDataObject,System.Runtime.InteropServices.ComTypes.IDataObject>? PrepareDropData { get; set; }

    public DesktopHost(bool demo,Action<bool> rememberIcons)
    {
        _demo=demo;_rememberIcons=rememberIcons;
        Text=demo ? "DesktopSpaces — безопасная демонстрация" : "DesktopSpaces";
        ShowInTaskbar=demo && Environment.GetCommandLineArgs().Contains("--ui-test");
        FormBorderStyle=demo ? FormBorderStyle.Sizable : FormBorderStyle.None;
        if(!demo)TopLevel=false;
        BackColor=demo ? Color.FromArgb(25,30,40) : TransparentColor;
        StartPosition=demo ? FormStartPosition.CenterScreen : FormStartPosition.Manual;
        Size=new Size(1100,740);
        Controls.Add(_indicator);
        _indicatorTimer.Tick+=(_,_)=>{_indicator.Hide();_indicatorTimer.Stop();if(NativeDesktopActive && (Native.GetAsyncKeyState(1)&0x8000)==0)Hide();};
    }


    public void Attach(IntPtr testParent=default)
    {
        if(_demo) { Show(); return; }
        _ownsNativeSurface=testParent==IntPtr.Zero;
        DesktopParent=_ownsNativeSurface?Native.FindDesktopView():testParent;
        if(DesktopParent==IntPtr.Zero)throw new IOException("Не найден рабочий стол Проводника. Перезапустите приложение после запуска Проводника.");
        var handle=Handle;
        Native.SetWindowLongPtr(handle,-16,new IntPtr((Native.GetWindowLongPtr(handle,-16).ToInt64() | 0x40000000L) & ~0x80000000L));
        Marshal.SetLastPInvokeError(0);
        var previous=Native.SetParent(handle,DesktopParent);
        if(previous==IntPtr.Zero && Marshal.GetLastPInvokeError()!=0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        Native.SetWindowLongPtr(handle,-20,new IntPtr(Native.GetWindowLongPtr(handle,-20).ToInt64()|0x80000));
        if(!Native.SetLayeredWindowAttributes(handle,TransparentColorRef,255,1))throw new System.ComponentModel.Win32Exception();
        _originalIconsVisible=_ownsNativeSurface && Native.IsWindowVisible(Native.DesktopList());
        _rememberIcons(_originalIconsVisible);
        _input=new DesktopInputSurface(()=>ActiveView) { PrepareData=data=>PrepareDropData?.Invoke(data) ?? data };
        Native.SetParent(_input.Handle,DesktopParent);
        _input.EnableTransparency();
        FitDesktop();
    }

    public void FitDesktop()
    {
        if(!_demo && Native.IsWindow(DesktopParent) && Native.GetClientRect(DesktopParent,out var rect))
        {
            var reattached=false;
            if(Native.GetParent(Handle)!=DesktopParent)
            {
                reattached=true;
                Native.SetParent(Handle,DesktopParent);
                Native.SetWindowLongPtr(Handle,-20,new IntPtr(Native.GetWindowLongPtr(Handle,-20).ToInt64()|0x80000));
                Native.SetLayeredWindowAttributes(Handle,TransparentColorRef,255,1);
            }
            if(_input is not null && Native.GetParent(_input.Handle)!=DesktopParent)
            { Native.SetParent(_input.Handle,DesktopParent);_input.EnableTransparency();reattached=true; }
            var size=new Size(rect.Right,rect.Bottom);
            if(!reattached && size==_desktopSize)return;
            _desktopSize=size;
            if(_input is not null)Native.SetWindowPos(_input.Handle,IntPtr.Zero,0,0,rect.Right,rect.Bottom,0x0010);
            var order=NativeDesktopActive && (Native.GetAsyncKeyState(1)&0x8000)!=0 ? new IntPtr(1) : IntPtr.Zero;
            Native.SetWindowPos(Handle,order,0,0,rect.Right,rect.Bottom,0x0010);
        }
    }

    public ShellFolderView Prepare(Space space,Action<Exception> error,Action ready)
    {
        if(_views.TryGetValue(space.Id,out var existing)) { if(existing.IsReady)ready();else existing.Ready+=ready;return existing; }
        var view=new ShellFolderView(space,error) { Visible=false,BackgroundFactory=_demo?CreateBackground:null,LiveBackground=!_demo,BackColor=BackColor,DropProxy=_input };
        _views.Add(space.Id,view);
        view.Ready+=ready;
        Controls.Add(view);
        _=view.Handle;
        return view;
    }

    public void ActivateSpace(Guid id,string name,bool showIndicator)
    {
        var next=_views[id];
        _focusNativeAfterDrag=false;
        NativeDesktopActive=false;ActiveSpaceId=id;
        if(!_demo)
        {
            // Hiding Explorer's source HWND during DoDragDrop can cancel its drag.
            // Cover it until the mouse is released, then hide its original icons.
            if(_ownsNativeSurface)
            {
                Native.MaskWindow(Native.DesktopList(),true);
                if((Native.GetAsyncKeyState(1)&0x8000)==0)Native.ShowWindow(Native.DesktopList(),0);
            }
            _input!.Show();
        }
        Show();
        Native.MaskWindow(next.Handle,false);next.Show();next.BringToFront();
        if(!_demo)Native.SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x0040|0x0010|0x0001|0x0002);
        if(ActiveView is not null && ActiveView!=next)
        { Native.MaskWindow(ActiveView.Handle,true);if((Native.GetAsyncKeyState(1)&0x8000)==0)ActiveView.Hide(); }
        ActiveView=next;
        if(showIndicator)
            ShowName(name);
        Text=_demo ? "DesktopSpaces — демонстрация · "+name : "DesktopSpaces";
        if((Native.GetAsyncKeyState(1)&0x8000)==0)next.FocusView();
    }

    public bool ContainsPoint(Point p)
    {
        if(IsDisposed)return false;
        var window=Native.WindowFromPoint(new Native.Point(p.X,p.Y));
        if(NativeDesktopActive)return window==DesktopParent || Native.IsChild(DesktopParent,window);
        if(!Visible)return false;
        return window==Handle || Native.IsChild(Handle,window) || (_input is not null && window==_input.Handle);
    }

    private void ShowName(string name)
    {
        _indicator.Text=name;
        _indicator.Location=new Point(Math.Max(16,ClientSize.Width-_indicator.PreferredWidth-28),24);
        _indicator.Show();_indicator.BringToFront();_indicatorTimer.Stop();_indicatorTimer.Start();
    }

    public void ActivateNativeDesktop(Guid id,string name,bool showIndicator)
    {
        if(ActiveView is not null)Native.MaskWindow(ActiveView.Handle,true);
        ActiveSpaceId=id;NativeDesktopActive=true;ActiveView=null;
        if(!_demo)Native.MaskWindow(Native.DesktopList(),false);
        if(!_demo && _originalIconsVisible)Native.ShowWindow(Native.DesktopList(),5);
        _input?.Hide();
        if((Native.GetAsyncKeyState(1)&0x8000)!=0)
            Native.SetWindowPos(Handle,new IntPtr(1),0,0,0,0,0x0010|0x0001|0x0002);
        else if(showIndicator)
        {
            foreach(var view in _views.Values)view.Hide();
            Show();Native.SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x0010|0x0001|0x0002);
            ShowName(name);
        }
        else { _indicator.Hide();Hide(); }
        _focusNativeAfterDrag=!_demo && (Native.GetAsyncKeyState(1)&0x8000)!=0;
        if(!_demo && !_focusNativeAfterDrag)Native.FocusNativeDesktop();
    }

    public bool IsEmptyPoint(Point p) => NativeDesktopActive ? ShellFolderView.IsEmptyPointAt(DesktopParent,p) : ActiveView?.IsEmptyPoint(p)==true;

    public bool IsDesktopForeground()
    {
        var foreground=Native.GetForegroundWindow();
        return foreground==Handle || Native.IsChild(Handle,foreground) ||
               (!_demo && (Native.GetAncestor(DesktopParent,2)==foreground || foreground==DesktopParent || Native.IsChild(DesktopParent,foreground)));
    }

    public void SaveLayouts() { foreach(var view in _views.Values)view.SaveLayout(); }
    internal uint ExerciseDrop(System.Runtime.InteropServices.ComTypes.IDataObject data,Point screenPoint)
    {
        var target=(IDropTargetNative)(_input ?? throw new InvalidOperationException());
        var p=new Native.Point(screenPoint.X,screenPoint.Y);uint effect=7;
        Native.Check(target.DragEnter(data,1,p,ref effect));
        Native.Check(target.DragOver(1,p,ref effect));
        Native.Check(target.Drop(data,0,p,ref effect));
        return effect;
    }
    public void Forget(Guid id) { if(_views.Remove(id,out var view)){Controls.Remove(view);view.Dispose();} }

    private Bitmap CreateBackground()
    {
        var screen=RectangleToScreen(ClientRectangle);
        return WallpaperRenderer.Render(screen,_demo);
    }

    public void RefreshBackgrounds() { foreach(var view in _views.Values)view.UpdateBackground(); }

    public void MaintainViews()
    {
        ActiveView?.MaintainView();
        if((Native.GetAsyncKeyState(1)&0x8000)!=0)return;
        if(_focusNativeAfterDrag)
        { _focusNativeAfterDrag=false;if(NativeDesktopActive && IsDesktopForeground())Native.FocusNativeDesktop(); }
        if(_ownsNativeSurface && !NativeDesktopActive)Native.ShowWindow(Native.DesktopList(),0);
        if(NativeDesktopActive && !_indicator.Visible)Hide();
        foreach(var view in _views.Values)if(view!=ActiveView)view.Hide();
        // Keep created Shell views warm: rebuilding them makes icons appear late.
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing) {
            if(!_demo)Native.MaskWindow(Native.DesktopList(),false);
            if(!_demo && _originalIconsVisible)Native.ShowWindow(Native.DesktopList(),5);
            _input?.Dispose();_indicatorTimer.Dispose();foreach(var view in _views.Values)view.Dispose();_views.Clear();
        }
        base.Dispose(disposing);
    }
}
