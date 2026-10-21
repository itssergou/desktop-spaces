namespace DesktopSpaces.App;

internal static class OleFiles
{
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
