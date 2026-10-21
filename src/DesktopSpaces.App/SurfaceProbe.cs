namespace DesktopSpaces.App;

internal static class SurfaceProbe
{
    public static void Run(string[] args)
    {
        var root=Program.DiagnosticRoot("surface",args);
        var data=Path.Combine(root,"data",Guid.NewGuid().ToString("N"));
        var source=Path.Combine(data,"source");var target=Path.Combine(data,"target");
        Directory.CreateDirectory(source);Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source,"Перенести сюда.txt"),"OLE drag and drop check");
        File.WriteAllText(Path.Combine(target,"Передвинуть значок.txt"),"Icon placement check");
        var space=new Space { Folder=target,Name="Проверка прозрачного рабочего стола" };
        using var form=new Form { Text="DesktopSpaces — проверка прозрачности и переноса",ClientSize=new Size(1120,720),StartPosition=FormStartPosition.CenterScreen };
        var grid=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(12) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute,45));grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));form.Controls.Add(grid);
        grid.Controls.Add(new Label { Text="Источник · перетащите файл вправо",Dock=DockStyle.Fill },0,0);
        grid.Controls.Add(new Label { Text="Прозрачная поверхность · фон должен двигаться",Dock=DockStyle.Fill },1,0);
        var errors=new List<string>();
        var from=new ShellFolderView(new Space { Folder=source },e=>errors.Add(e.ToString()));grid.Controls.Add(from,0,1);
        var wallpaper=new AnimatedWallpaper { Dock=DockStyle.Fill };grid.Controls.Add(wallpaper,1,1);
        using var host=new DesktopHost(false,_=>{});
        form.Shown+=(_,_)=> {
            host.Attach(wallpaper.Handle);
            host.Prepare(space,e=>errors.Add(e.ToString()),()=>host.ActivateSpace(space.Id,space.Name,false));
        };
        using var timer=new System.Windows.Forms.Timer { Interval=1500 };
        var exercised=false;uint? importEffect=null,moveEffect=null;var ticks=0;
        timer.Tick+=(_,_)=> {
            try
            {
                host.MaintainViews();
                if(args.Contains("--exercise-drop") && !exercised && host.ActiveView?.IsReady==true && from.IsReady)
                {
                    exercised=true;
                    moveEffect=DropFrom(host.ActiveView,host,wallpaper.PointToScreen(new Point(300,200)));
                    importEffect=DropFrom(from,host,wallpaper.PointToScreen(new Point(400,300)));
                }
                host.ActiveView?.SaveLayout();
                var drop=host.ActiveView?.GetDropTarget();var hasDrop=drop is not null;
                if(drop is not null && Marshal.IsComObject(drop))Marshal.ReleaseComObject(drop);
                var transferred=File.Exists(Path.Combine(target,"Перенести сюда.txt"));
                var success=errors.Count==0 && transferred && space.Layout.TryGetValue("Передвинуть значок.txt",out var position) && position.X>100 && position.Y>100;
                JsonStore.Write(Path.Combine(root,"report.json"),new {
                    Errors=errors,Ready=host.ActiveView?.IsReady==true,
                    BackgroundKeyed=host.ActiveView is not null,
                    HasDropTarget=hasDrop,
                    ListHandle=host.ActiveView?.ListHandle.ToInt64(),
                    ListColor=host.ActiveView is null?0:Native.SendMessage(host.ActiveView.ListHandle,0x1000,IntPtr.Zero,IntPtr.Zero).ToInt64(),
                    HostStyle=Native.GetWindowLongPtr(host.Handle,-20).ToInt64(),
                    MoveEffect=moveEffect,ImportEffect=importEffect,
                    SourceReady=from.IsReady,Exercised=exercised,Arguments=args,
                    Layout=space.Layout,Transferred=transferred,Success=success,
                    Source=source,Target=target
                });
                if(args.Contains("--exercise-drop") && !args.Contains("--keep-open") && (++ticks>=3 && success || ticks>=12))
                { if(!success)Environment.ExitCode=1;form.Close(); }
            }
            catch(Exception ex) { errors.Add(ex.ToString()); }
        };
        form.Shown+=(_,_)=>timer.Start();Application.Run(form);
    }
    private static uint DropFrom(ShellFolderView source,DesktopHost target,Point point)
    {
        var view=source.GetFolderView() ?? throw new InvalidOperationException();
        try
        {
            Native.Check(view.SelectItem(0,1|4|16));
            var iid=typeof(System.Runtime.InteropServices.ComTypes.IDataObject).GUID;
            Native.Check(view.Items(1,ref iid,out var obj));
            try { return target.ExerciseDrop((System.Runtime.InteropServices.ComTypes.IDataObject)obj,point); }
            finally { Marshal.ReleaseComObject(obj); }
        }
        finally { Marshal.ReleaseComObject(view); }
    }
    private sealed class AnimatedWallpaper : Panel
    {
        private readonly System.Windows.Forms.Timer _timer=new() { Interval=60 };
        private int _phase;
        public AnimatedWallpaper()
        {
            DoubleBuffered=true;
            _timer.Tick+=(_,_)=>{_phase=(_phase+3)%160;Invalidate();};_timer.Start();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(24,40,70));
            using var brush=new SolidBrush(Color.FromArgb(55,90,140));
            for(var x=-160+_phase;x<Width;x+=160)e.Graphics.FillRectangle(brush,x,0,80,Height);
            using var text=new SolidBrush(Color.White);
            using var font=new Font("Segoe UI",18);
            e.Graphics.DrawString("Живой фон / Live wallpaper",font,text,150,Height/2);
            base.OnPaint(e);
        }
        protected override void Dispose(bool disposing) { if(disposing)_timer.Dispose();base.Dispose(disposing); }
    }
}
