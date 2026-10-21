namespace DesktopSpaces.App;

internal static class DesktopDiagnostics
{
    public static object CheckHitTesting()
    {
        var root=Native.FindDesktopView();var list=Native.DesktopList();
        var empty=0;var occupied=0;var iconRejected=false;
        if(root==IntPtr.Zero || list==IntPtr.Zero || !Native.IsWindowVisible(list))return new { EmptySamples=0,OccupiedSamples=0,IconRejected=false };
        Native.GetWindowRect(list,out var rectangle);
        for(var x=rectangle.Left+40;x<rectangle.Right;x+=160)
        for(var y=rectangle.Top+40;y<rectangle.Bottom;y+=160)
        {
            if(ShellFolderView.IsEmptyPointAt(root,new Point(x,y)))empty++;else occupied++;
        }
        object? accessible=null;
        try
        {
            var iid=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
            if(Native.AccessibleObjectFromWindow(list,0xFFFFFFFC,ref iid,out accessible)>=0)
            {
                var a=(Accessibility.IAccessible)accessible;
                if(a.accChildCount>0)
                {
                    a.accLocation(out var left,out var top,out var width,out var height,1);
                    iconRejected=!ShellFolderView.IsEmptyPointAt(root,new Point(left+width/2,top+height/2));
                }
            }
        }
        finally { if(accessible is not null && Marshal.IsComObject(accessible))Marshal.ReleaseComObject(accessible); }
        return new { EmptySamples=empty,OccupiedSamples=occupied,IconRejected=iconRejected };
    }
}
