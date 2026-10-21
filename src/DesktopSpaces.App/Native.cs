using System.Text;

namespace DesktopSpaces.App;

internal static class Native
{
    internal static Guid DesktopId = new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
    internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);
    internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X=x; Y=y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public Rect(Rectangle r) { Left=r.Left; Top=r.Top; Right=r.Right; Bottom=r.Bottom; } }
    [StructLayout(LayoutKind.Sequential)] internal struct FolderSettings { public uint ViewMode, Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseHook { public Point Point; public uint MouseData, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardHook { public uint Key, ScanCode, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct HitTest { public Point Point; public uint Flags; public int Item, SubItem, Group; }
    [StructLayout(LayoutKind.Sequential)] internal struct Msg { public IntPtr Hwnd; public uint Message; public IntPtr WParam,LParam; public uint Time; public Point Point; public uint Private; }
    [StructLayout(LayoutKind.Sequential)] internal struct BackgroundImage { public uint Flags; public IntPtr Bitmap,Url; public uint UrlLength; public int X,Y; }

    [DllImport("shell32.dll")] internal static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] internal static extern int SHSetKnownFolderPath(ref Guid id, uint flags, IntPtr token, string path);
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] internal static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint mask, out uint attributes);
    [DllImport("shell32.dll")] internal static extern int SHCreateItemWithParent(IntPtr parentPidl, [MarshalAs(UnmanagedType.Interface)] object parent, IntPtr child, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object item);
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] internal static extern void SHChangeNotify(uint eventId, uint flags, string? item1, string? item2);
    [DllImport("shlwapi.dll")] internal static extern int IUnknown_SetSite([MarshalAs(UnmanagedType.IUnknown)] object obj, [MarshalAs(UnmanagedType.IUnknown)] object? site);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string? className, string? title);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? title);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll", SetLastError=true)] internal static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("ole32.dll")] internal static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")] internal static extern void OleUninitialize();
    [DllImport("ole32.dll")] internal static extern void ReleaseStgMedium(ref System.Runtime.InteropServices.ComTypes.STGMEDIUM medium);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] internal static extern uint DragQueryFile(IntPtr drop,uint index,StringBuilder? name,uint length);
    [DllImport("ole32.dll")] internal static extern int RegisterDragDrop(IntPtr hwnd, [MarshalAs(UnmanagedType.Interface)] IDropTargetNative target);
    [DllImport("ole32.dll")] internal static extern int RevokeDragDrop(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hwnd,int command);
    [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd,uint color,byte alpha,uint flags);
    [DllImport("oleacc.dll")] internal static extern int AccessibleObjectFromWindow(IntPtr hwnd,uint objectId,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object accessible);
    [DllImport("user32.dll")] internal static extern int MapWindowPoints(IntPtr from,IntPtr to,ref Point point,uint count);
    [DllImport("user32.dll")] internal static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(IntPtr hwnd, ref Point point);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] internal static extern uint GetMessageTime();
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", SetLastError=true)] internal static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, ref HitTest hit);
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, ref BackgroundImage image);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] internal static extern IntPtr SetFocus(IntPtr hwnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern bool SystemParametersInfo(uint action, uint size, StringBuilder value, uint flags);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string? module);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    internal static string ClassName(IntPtr hwnd) { var s=new StringBuilder(256); GetClassName(hwnd,s,s.Capacity); return s.ToString(); }
    internal static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    internal static IntPtr DesktopList() => FindWindowEx(FindDesktopView(),IntPtr.Zero,"SysListView32",null);
    internal static void RestoreDesktopIcons(RecoveryRecord record)
    {
        if(record.RestoreDesktopIcons) { var list=DesktopList();if(list!=IntPtr.Zero)ShowWindow(list,5); }
    }
    internal static IntPtr FindDesktopView()
    {
        var found=IntPtr.Zero;
        EnumWindows((hwnd, _) => { var child=FindWindowEx(hwnd,IntPtr.Zero,"SHELLDLL_DefView",null); if(child==IntPtr.Zero)return true; found=child; return false; },IntPtr.Zero);
        return found;
    }
}

[ComImport, Guid("DFD3B6B5-C10C-4BE9-85F6-A66969F402F6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IExplorerBrowser
{
    [PreserveSig] int Initialize(IntPtr parent, ref Native.Rect rect, ref Native.FolderSettings settings);
    [PreserveSig] int Destroy();
    [PreserveSig] int SetRect(IntPtr defer, ref Native.Rect rect);
    [PreserveSig] int SetPropertyBag([MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int SetEmptyText([MarshalAs(UnmanagedType.LPWStr)] string text);
    [PreserveSig] int SetFolderSettings(ref Native.FolderSettings settings);
    [PreserveSig] int Advise([MarshalAs(UnmanagedType.Interface)] IExplorerBrowserEvents events, out uint cookie);
    [PreserveSig] int Unadvise(uint cookie);
    [PreserveSig] int SetOptions(uint options);
    [PreserveSig] int GetOptions(out uint options);
    [PreserveSig] int BrowseToIDList(IntPtr pidl, uint flags);
    [PreserveSig] int BrowseToObject([MarshalAs(UnmanagedType.IUnknown)] object obj, uint flags);
    [PreserveSig] int FillFromObject([MarshalAs(UnmanagedType.IUnknown)] object obj, int flags);
    [PreserveSig] int RemoveAll();
    [PreserveSig] int GetCurrentView(ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object view);
}

[ComVisible(true), Guid("361BBDC7-E6EE-4E13-BE58-58E2240C810F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IExplorerBrowserEvents
{
    [PreserveSig] int OnNavigationPending(IntPtr pidl);
    [PreserveSig] int OnViewCreated([MarshalAs(UnmanagedType.IUnknown)] object view);
    [PreserveSig] int OnNavigationComplete(IntPtr pidl);
    [PreserveSig] int OnNavigationFailed(IntPtr pidl);
}

[ComVisible(true), Guid("000214F1-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICommDlgBrowser
{
    [PreserveSig] int OnDefaultCommand(IntPtr shellView);
    [PreserveSig] int OnStateChange(IntPtr shellView, uint change);
    [PreserveSig] int IncludeObject(IntPtr shellView, IntPtr pidl);
}

[ComVisible(true), Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IServiceProviderNative
{
    [PreserveSig] int QueryService(ref Guid service, ref Guid iid, out IntPtr result);
}

[ComImport, Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFolderView
{
    [PreserveSig] int GetCurrentViewMode(out uint mode);
    [PreserveSig] int SetCurrentViewMode(uint mode);
    [PreserveSig] int GetFolder(ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object folder);
    [PreserveSig] int Item(int index, out IntPtr pidl);
    [PreserveSig] int ItemCount(uint flags, out int count);
    [PreserveSig] int Items(uint flags, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object items);
    [PreserveSig] int GetSelectionMarkedItem(out int index);
    [PreserveSig] int GetFocusedItem(out int index);
    [PreserveSig] int GetItemPosition(IntPtr pidl, out Native.Point point);
    [PreserveSig] int GetSpacing(out Native.Point point);
    [PreserveSig] int GetDefaultSpacing(out Native.Point point);
    [PreserveSig] int GetAutoArrange();
    [PreserveSig] int SelectItem(int index, uint flags);
    [PreserveSig] int SelectAndPositionItems(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] IntPtr[] pidls,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] Native.Point[] points, uint flags);
}

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    [PreserveSig] int BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr obj);
    [PreserveSig] int GetParent(out IShellItem item);
    [PreserveSig] int GetDisplayName(uint sigdn, out IntPtr name);
    [PreserveSig] int GetAttributes(uint mask, out uint attrs);
    [PreserveSig] int Compare(IShellItem item, uint hint, out int order);
}

[ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
    [PreserveSig] int BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr obj);
    [PreserveSig] int GetPropertyStore(int flags, ref Guid iid, out IntPtr obj);
    [PreserveSig] int GetPropertyDescriptionList(IntPtr key, ref Guid iid, out IntPtr obj);
    [PreserveSig] int GetAttributes(uint flags, uint mask, out uint attrs);
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetItemAt(uint index, out IShellItem item);
    [PreserveSig] int EnumItems(out IntPtr enumerator);
}

[ComImport,Guid("000214E3-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellView
{
    [PreserveSig] int GetWindow(out IntPtr hwnd);
    [PreserveSig] int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enter);
    [PreserveSig] int TranslateAccelerator(ref Native.Msg message);
    [PreserveSig] int EnableModeless([MarshalAs(UnmanagedType.Bool)] bool enable);
    [PreserveSig] int UIActivate(uint state);
    [PreserveSig] int Refresh();
    [PreserveSig] int CreateViewWindow(IntPtr previous,ref Native.FolderSettings settings,IntPtr browser,ref Native.Rect rect,out IntPtr hwnd);
    [PreserveSig] int DestroyViewWindow();
    [PreserveSig] int GetCurrentInfo(ref Native.FolderSettings settings);
    [PreserveSig] int AddPropertySheetPages(uint reserved,IntPtr callback,IntPtr parameter);
    [PreserveSig] int SaveViewState();
    [PreserveSig] int SelectItem(IntPtr pidl,uint flags);
    [PreserveSig] int GetItemObject(uint item,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object obj);
}

[ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    [PreserveSig] int SetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string monitor,[MarshalAs(UnmanagedType.LPWStr)]string wallpaper);
    [PreserveSig] int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string? monitor,[MarshalAs(UnmanagedType.LPWStr)]out string wallpaper);
    [PreserveSig] int GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)]out string monitor);
    [PreserveSig] int GetMonitorDevicePathCount(out uint count);
    [PreserveSig] int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)]string monitor,out Native.Rect rect);
    [PreserveSig] int SetBackgroundColor(uint color);
    [PreserveSig] int GetBackgroundColor(out uint color);
    [PreserveSig] int SetPosition(uint position);
    [PreserveSig] int GetPosition(out uint position);
}

[ComVisible(true), Guid("00000122-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDropTargetNative
{
    [PreserveSig] int DragEnter([MarshalAs(UnmanagedType.Interface)]System.Runtime.InteropServices.ComTypes.IDataObject data,uint keys,Native.Point point,ref uint effect);
    [PreserveSig] int DragOver(uint keys,Native.Point point,ref uint effect);
    [PreserveSig] int DragLeave();
    [PreserveSig] int Drop([MarshalAs(UnmanagedType.Interface)]System.Runtime.InteropServices.ComTypes.IDataObject data,uint keys,Native.Point point,ref uint effect);
}

[ComImport,Guid("1AF3A467-214F-4298-908E-06B03E0B39F9"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFolderView2
{
    [PreserveSig] int GetCurrentViewMode(out uint mode);
    [PreserveSig] int SetCurrentViewMode(uint mode);
    [PreserveSig] int GetFolder(ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object folder);
    [PreserveSig] int Item(int index,out IntPtr pidl);
    [PreserveSig] int ItemCount(uint flags,out int count);
    [PreserveSig] int Items(uint flags,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object items);
    [PreserveSig] int GetSelectionMarkedItem(out int index);
    [PreserveSig] int GetFocusedItem(out int index);
    [PreserveSig] int GetItemPosition(IntPtr pidl,out Native.Point point);
    [PreserveSig] int GetSpacing(out Native.Point point);
    [PreserveSig] int GetDefaultSpacing(out Native.Point point);
    [PreserveSig] int GetAutoArrange();
    [PreserveSig] int SelectItem(int index,uint flags);
    [PreserveSig] int SelectAndPositionItems(uint count,IntPtr pidls,IntPtr points,uint flags);
    [PreserveSig] int SetGroupBy(IntPtr key,[MarshalAs(UnmanagedType.Bool)]bool ascending);
    [PreserveSig] int GetGroupBy(IntPtr key,out bool ascending);
    [PreserveSig] int SetViewProperty(IntPtr pidl,IntPtr key,IntPtr value);
    [PreserveSig] int GetViewProperty(IntPtr pidl,IntPtr key,IntPtr value);
    [PreserveSig] int SetTileViewProperties(IntPtr pidl,[MarshalAs(UnmanagedType.LPWStr)]string properties);
    [PreserveSig] int SetExtendedTileViewProperties(IntPtr pidl,[MarshalAs(UnmanagedType.LPWStr)]string properties);
    [PreserveSig] int SetText(uint type,[MarshalAs(UnmanagedType.LPWStr)]string text);
    [PreserveSig] int SetCurrentFolderFlags(uint mask,uint flags);
    [PreserveSig] int GetCurrentFolderFlags(out uint flags);
}
