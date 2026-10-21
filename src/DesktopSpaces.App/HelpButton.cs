namespace DesktopSpaces.App;

internal sealed class HelpButton : Button
{
    private bool _hover;
    public HelpButton()
    {
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        Size=new Size(24,24);Cursor=Cursors.Hand;
        SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
    }
    protected override void OnMouseEnter(EventArgs e) { _hover=true;Invalidate();base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover=false;Invalidate();base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var scale=DeviceDpi/96f;var diameter=Math.Min(ClientSize.Width,ClientSize.Height)-6*scale;
        var circle=new RectangleF((ClientSize.Width-diameter)/2,(ClientSize.Height-diameter)/2,diameter,diameter);
        using var fill=new SolidBrush(_hover?Color.FromArgb(224,235,249):Color.FromArgb(239,243,249));
        using var pen=new Pen(_hover?Color.FromArgb(55,110,180):Color.FromArgb(145,160,180),scale);
        e.Graphics.FillEllipse(fill,circle);e.Graphics.DrawEllipse(pen,circle);
        using var font=new Font("Segoe UI",9,FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics,"?",font,ClientRectangle,_hover?Color.FromArgb(35,90,160):Color.FromArgb(85,105,130),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
        if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-1,-1));
    }
}
