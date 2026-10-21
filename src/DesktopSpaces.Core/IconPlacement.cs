namespace DesktopSpaces.Core;

public static class IconPlacement
{
    public static IconPosition[]? FindFreeGroup(IReadOnlyList<IconPosition> offsets,IReadOnlyList<IconPosition> occupied,
        IconPosition requested,int spacingX,int spacingY,int width,int height)
    {
        if(spacingX<=0 || spacingY<=0 || offsets.Count==0)throw new ArgumentOutOfRangeException(nameof(spacingX));
        var columns=Math.Max(1,(width-13)/spacingX);var rows=Math.Max(1,(height-2)/spacingY);
        var cells=from x in Enumerable.Range(0,columns) from y in Enumerable.Range(0,rows)
                  let p=new IconPosition(13+x*spacingX,2+y*spacingY)
                  orderby (long)(p.X-requested.X)*(p.X-requested.X)+(long)(p.Y-requested.Y)*(p.Y-requested.Y),x,y
                  select p;
        foreach(var anchor in cells)
        {
            var result=offsets.Select(o=>new IconPosition(anchor.X+o.X,anchor.Y+o.Y)).ToArray();
            if(result.Any(p=>p.X<0 || p.Y<0 || p.X+spacingX>width || p.Y+spacingY>height))continue;
            if(result.Any(p=>occupied.Any(q=>Math.Abs((long)p.X-q.X)<spacingX && Math.Abs((long)p.Y-q.Y)<spacingY)))continue;
            return result;
        }
        return null;
    }
}
