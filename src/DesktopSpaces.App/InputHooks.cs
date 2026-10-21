namespace DesktopSpaces.App;

internal sealed class InputHooks : IDisposable
{
    private readonly DesktopHost _host;
    private readonly Preferences _settings;
    private readonly Action<int> _select;
    private readonly Action _next;
    private readonly Native.HookProc _keyboardProc;
    private readonly Native.HookProc _mouseProc;
    private IntPtr _keyboard,_mouse;
    private readonly HashSet<uint> _heldDigits=new();
    private readonly HashSet<uint> _blocked=new();
    private bool _down,_dragged;
    private Point _downAt,_lastClick;
    private uint _lastClickTime;
    private Guid? _lastClickSpace;
    public IReadOnlyList<int> ConflictingNumbers => _blocked.Select(k=>(int)k-0x30).ToArray();

    public InputHooks(DesktopHost host,Preferences settings,Action<int> select,Action next)
    {
        _host=host;_settings=settings;_select=select;_next=next;
        // Exceptions must never cross a native hook callback, including when Explorer
        // destroys a host window while a queued input notification is being processed.
        _keyboardProc=(code,message,data)=> { try{return Keyboard(code,message,data);}catch{return Native.CallNextHookEx(_keyboard,code,message,data);} };
        _mouseProc=(code,message,data)=> { try{return Mouse(code,message,data);}catch{return Native.CallNextHookEx(_mouse,code,message,data);} };
        _keyboard=Native.SetWindowsHookEx(13,_keyboardProc,Native.GetModuleHandle(null),0);
        _mouse=Native.SetWindowsHookEx(14,_mouseProc,Native.GetModuleHandle(null),0);
        if(_keyboard==IntPtr.Zero || _mouse==IntPtr.Zero) { Dispose();throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
        RefreshConflicts();
    }

    private IntPtr Keyboard(int code,IntPtr message,IntPtr data)
    {
        if(code>=0)
        {
            var key=Marshal.PtrToStructure<Native.KeyboardHook>(data);
            var up=message.ToInt64() is 0x101 or 0x105;
            if(up && _heldDigits.Remove(key.Key))return new IntPtr(1);
            if(!up && key.Key is >=0x31 and <=0x39 && _settings.HotkeysEnabled && !_blocked.Contains(key.Key))
            {
                var alt=(key.Flags&0x20)!=0;
                var control=(Native.GetAsyncKeyState(0x11)&0x8000)!=0;
                var shift=(Native.GetAsyncKeyState(0x10)&0x8000)!=0;
                var windows=(Native.GetAsyncKeyState(0x5B)&0x8000)!=0 || (Native.GetAsyncKeyState(0x5C)&0x8000)!=0;
                var rightAlt=(Native.GetAsyncKeyState(0xA5)&0x8000)!=0;
                var context=_host.IsDesktopForeground() || (_down && _dragged && _host.ContainsPoint(Cursor.Position));
                if(alt && control==_settings.UseControlAlt && !shift && !windows && !rightAlt && context)
                {
                    if(_heldDigits.Add(key.Key))_host.BeginInvoke((Action)(()=>_select((int)key.Key-0x31)));
                    return new IntPtr(1);
                }
            }
        }
        return Native.CallNextHookEx(_keyboard,code,message,data);
    }

    private IntPtr Mouse(int code,IntPtr message,IntPtr data)
    {
        if(code>=0)
        {
            var mouse=Marshal.PtrToStructure<Native.MouseHook>(data);
            var point=new Point(mouse.Point.X,mouse.Point.Y);
            switch(message.ToInt64())
            {
                case 0x201: _down=true;_dragged=false;_downAt=point;break;
                case 0x200:
                    if(_down && (Math.Abs(point.X-_downAt.X)>SystemInformation.DragSize.Width/2 || Math.Abs(point.Y-_downAt.Y)>SystemInformation.DragSize.Height/2))_dragged=true;
                    break;
                case 0x202:
                    var dragged=_dragged;_down=false;_dragged=false;
                    if(!dragged && _host.ContainsPoint(point))
                        _host.BeginInvoke((Action)(()=>Click(point,mouse.Time)));
                    break;
            }
        }
        return Native.CallNextHookEx(_mouse,code,message,data);
    }

    private void Click(Point point,uint time)
    {
        if(!_settings.DoubleClickSwitch || !_host.IsEmptyPoint(point))
        { _lastClickTime=0;_lastClickSpace=null;return; }
        var id=_host.ActiveSpaceId;
        if(_lastClickSpace==id && unchecked(time-_lastClickTime)<=Native.GetDoubleClickTime() &&
           Math.Abs(point.X-_lastClick.X)<=SystemInformation.DoubleClickSize.Width/2 &&
           Math.Abs(point.Y-_lastClick.Y)<=SystemInformation.DoubleClickSize.Height/2)
        { _lastClickTime=0;_lastClickSpace=null;_next(); }
        else { _lastClick=point;_lastClickTime=time;_lastClickSpace=id; }
    }

    public void Dispose()
    {
        if(_keyboard!=IntPtr.Zero)Native.UnhookWindowsHookEx(_keyboard);
        if(_mouse!=IntPtr.Zero)Native.UnhookWindowsHookEx(_mouse);
        _keyboard=_mouse=IntPtr.Zero;
        GC.KeepAlive(_keyboardProc);GC.KeepAlive(_mouseProc);
    }

    public IReadOnlyList<int> RefreshConflicts()
    {
        _blocked.Clear();
        if(!_settings.HotkeysEnabled)return [];
        for(uint key=0x31;key<=0x39;key++)
        {
            var id=0x5320+(int)key;
            if(Native.RegisterHotKey(IntPtr.Zero,id,0x4001u|(_settings.UseControlAlt?0x2u:0),key))Native.UnregisterHotKey(IntPtr.Zero,id);
            else _blocked.Add(key);
        }
        return _blocked.Select(k=>(int)k-0x30).ToArray();
    }
}
