namespace DesktopSpaces.App;

internal static class WallpaperRenderer
{
    public static Bitmap Render(Rectangle canvas,bool demo)
    {
        var bitmap=new Bitmap(Math.Max(1,canvas.Width),Math.Max(1,canvas.Height));
        using var g=Graphics.FromImage(bitmap);
        g.Clear(SystemColors.Desktop);
        IDesktopWallpaper? service=null;
        try
        {
            service=(IDesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"),true)!)!;
            if(service.GetBackgroundColor(out var color)>=0)g.Clear(Color.FromArgb((int)(color&255),(int)((color>>8)&255),(int)((color>>16)&255)));
            Native.Check(service.GetPosition(out var position));
            Native.Check(service.GetMonitorDevicePathCount(out var count));
            for(uint i=0;i<count;i++)
            {
                Native.Check(service.GetMonitorDevicePathAt(i,out var monitor));
                Native.Check(service.GetMonitorRECT(monitor,out var r));
                Native.Check(service.GetWallpaper(monitor,out var path));
                if(string.IsNullOrWhiteSpace(path) || !File.Exists(path))continue;
                using var image=Image.FromFile(path);
                var target=demo ? new Rectangle(Point.Empty,canvas.Size) : Rectangle.FromLTRB(r.Left-canvas.Left,r.Top-canvas.Top,r.Right-canvas.Left,r.Bottom-canvas.Top);
                if(position==5) { var all=SystemInformation.VirtualScreen;target=new Rectangle(all.Left-canvas.Left,all.Top-canvas.Top,all.Width,all.Height); }
                var saved=g.Save();g.SetClip(target);
                Draw(g,image,target,position);
                g.Restore(saved);
                if(demo || position==5)break;
            }
        }
        catch(Exception ex) when(ex is COMException or IOException or ArgumentException or OutOfMemoryException)
        { /* A missing/cloud-only wallpaper must not prevent access to files. */ }
        finally { if(service is not null)Marshal.ReleaseComObject(service); }
        return bitmap;
    }

    private static void Draw(Graphics g,Image image,Rectangle target,uint position)
    {
        if(position==1)
        {
            using var brush=new TextureBrush(image);brush.TranslateTransform(target.Left,target.Top);g.FillRectangle(brush,target);return;
        }
        if(position==2) { g.DrawImage(image,target);return; }
        float scale=position switch { 0=>1,3=>Math.Min((float)target.Width/image.Width,(float)target.Height/image.Height),_=>Math.Max((float)target.Width/image.Width,(float)target.Height/image.Height) };
        var width=(int)(image.Width*scale);var height=(int)(image.Height*scale);
        g.DrawImage(image,target.Left+(target.Width-width)/2,target.Top+(target.Height-height)/2,width,height);
    }
}
