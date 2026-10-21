namespace DesktopSpaces.App;

internal sealed class WindowsDesktopPath : IDesktopPath
{
    public string Get()
    {
        Native.Check(Native.SHGetKnownFolderPath(ref Native.DesktopId,0,IntPtr.Zero,out var path));
        try { return Marshal.PtrToStringUni(path) ?? throw new IOException("Не удалось определить рабочий стол."); }
        finally { Marshal.FreeCoTaskMem(path); }
    }

    public void Set(string path)
    {
        Native.Check(Native.SHSetKnownFolderPath(ref Native.DesktopId,0,IntPtr.Zero,path));
        Native.SHChangeNotify(0x00001000,0x0005,path,null);
    }
}

internal sealed class DemoDesktopPath(string path) : IDesktopPath
{
    public string Get() => path;
    public void Set(string value) => path=value;
}
