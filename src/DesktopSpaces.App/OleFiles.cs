namespace DesktopSpaces.App;

internal static class OleFiles
{
    private static uint? _clipboardSequence;
    public static System.Runtime.InteropServices.ComTypes.IDataObject Freeze(string[] files,uint effect=2)
    {
        var data=new DataObject();var paths=new System.Collections.Specialized.StringCollection();paths.AddRange(files);
        data.SetFileDropList(paths);
        data.SetData("Preferred DropEffect",new MemoryStream(BitConverter.GetBytes(effect)));
        return (System.Runtime.InteropServices.ComTypes.IDataObject)data;
    }

    public static System.Runtime.InteropServices.ComTypes.IDataObject FreezeBeforeChange(System.Runtime.InteropServices.ComTypes.IDataObject data,Action change)
    {
        var paths=Read(data);
        if(paths.Length==0)throw new IOException("Проводник не передал пути переносимых файлов.");
        var frozen=Freeze(paths);change();return frozen;
    }

    public static void StabilizeDesktopClipboard(string folder)
    {
        var sequence=Native.GetClipboardSequenceNumber();
        if(sequence==_clipboardSequence)return;
        if(!Clipboard.ContainsFileDropList()){_clipboardSequence=sequence;return;}
        var files=Clipboard.GetFileDropList().Cast<string>().ToArray();
        if(files.Length==0 || !files.Any(f=>Paths.Equal(Path.GetDirectoryName(f)!,folder))){_clipboardSequence=sequence;return;}
        var effect=Clipboard.GetData("Preferred DropEffect") is MemoryStream stream && stream.Length>=4
            ? BitConverter.ToUInt32(stream.ToArray(),0) : 1u;
        Clipboard.SetDataObject(Freeze(files,effect),true);
        _clipboardSequence=Native.GetClipboardSequenceNumber();
    }

    public static string[] Read(System.Runtime.InteropServices.ComTypes.IDataObject data)
    {
        var format=new System.Runtime.InteropServices.ComTypes.FORMATETC { cfFormat=15,dwAspect=System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL };
        if(data.QueryGetData(ref format)!=0)return [];
        data.GetData(ref format,out var medium);
        try
        {
            var count=Native.DragQueryFile(medium.unionmember,uint.MaxValue,null,0);var paths=new List<string>();
            for(uint i=0;i<count;i++)
            {
                var length=Native.DragQueryFile(medium.unionmember,i,null,0);
                var name=new System.Text.StringBuilder((int)length+1);
                Native.DragQueryFile(medium.unionmember,i,name,(uint)name.Capacity);paths.Add(name.ToString());
            }
            return paths.ToArray();
        }
        finally { Native.ReleaseStgMedium(ref medium); }
    }
}
