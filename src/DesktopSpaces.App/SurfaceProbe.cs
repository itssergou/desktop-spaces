namespace DesktopSpaces.App;

internal static class SurfaceProbe
{
    public static void Run(string[] args)
    {
        var root=Program.DiagnosticRoot("surface",args);
        if(args.Contains("--redirect-during-drop") && File.Exists(Path.Combine(AppStorage.Root,"recovery.json")))
        {
            JsonStore.Write(Path.Combine(root,"report.json"),new { Success=false,Errors=new[]{"Сначала завершите работающий экземпляр DesktopSpaces."} });
            Environment.ExitCode=1;return;
        }
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
        ShellFolderView? returnView=null;
        DesktopSession? redirectedSession=null;
        var redirectedStarted=false;
        form.Shown+=(_,_)=> {
            if(args.Contains("--redirect-during-drop"))
            {
                var journal=Path.Combine(AppStorage.Root,"recovery.json");
                var desktop=new WindowsDesktopPath();redirectedSession=new DesktopSession(desktop,journal,Native.RestoreDesktopIcons);
                using var process=Process.GetCurrentProcess();redirectedSession.Start(desktop.Get(),process.Id,process.StartTime.ToUniversalTime().Ticks);
                redirectedStarted=true;
                var watchdog=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden };
                watchdog.ArgumentList.Add("--watchdog");watchdog.ArgumentList.Add(journal);Process.Start(watchdog)?.Dispose();
                redirectedSession.Switch(source);
            }
            host.Attach(wallpaper.Handle);
            host.Prepare(space,e=>errors.Add(e.ToString()),()=>host.ActivateSpace(space.Id,space.Name,false));
            if(args.Contains("--round-trip"))returnView=host.Prepare(from.Space,e=>errors.Add(e.ToString()),()=>{});
        };
        using var timer=new System.Windows.Forms.Timer { Interval=1500 };
        var exercised=false;uint? importEffect=null,moveEffect=null;var ticks=0;var imported=false;var returning=false;
        timer.Tick+=(_,_)=> {
            try
            {
                host.MaintainViews();
                if(args.Contains("--exercise-drop") && !exercised && host.ActiveView?.IsReady==true && from.IsReady)
                {
                    exercised=true;
                    moveEffect=DropFrom(host.ActiveView,host,wallpaper.PointToScreen(new Point(300,200)));
                    importEffect=DropFrom(from,host,wallpaper.PointToScreen(new Point(400,300)),()=>redirectedSession?.Switch(target),root);
                }
                host.ActiveView?.SaveLayout();
                var drop=host.ActiveView?.GetDropTarget();var hasDrop=drop is not null;
                if(drop is not null && Marshal.IsComObject(drop))Marshal.ReleaseComObject(drop);
                var transferred=File.Exists(Path.Combine(target,"Перенести сюда.txt"));
                imported|=transferred;
                if(args.Contains("--round-trip") && transferred && space.Layout.ContainsKey("Перенести сюда.txt") && !returning && returnView?.IsReady==true)
                {
                    returning=true;
                    DropFrom(host.ActiveView!,host,wallpaper.PointToScreen(new Point(300,300)),()=> {
                        redirectedSession?.Switch(source);host.ActivateSpace(from.Space.Id,"Обратный перенос",false);
                    },root,"Перенести сюда.txt");
                }
                var returned=returning && !File.Exists(Path.Combine(target,"Перенести сюда.txt")) && File.Exists(Path.Combine(source,"Перенести сюда.txt"));
                var success=errors.Count==0 && imported && (!args.Contains("--round-trip") || returned) && space.Layout.TryGetValue("Передвинуть значок.txt",out var position) && position.X>100 && position.Y>100;
                JsonStore.Write(Path.Combine(root,"report.json"),new {
                    Errors=errors,Ready=host.ActiveView?.IsReady==true,
                    BackgroundKeyed=host.ActiveView is not null,
                    HasDropTarget=hasDrop,
                    ListHandle=host.ActiveView?.ListHandle.ToInt64(),
                    ListColor=host.ActiveView is null?0:Native.SendMessage(host.ActiveView.ListHandle,0x1000,IntPtr.Zero,IntPtr.Zero).ToInt64(),
                    HostStyle=Native.GetWindowLongPtr(host.Handle,-20).ToInt64(),
                    MoveEffect=moveEffect,ImportEffect=importEffect,
                    SourceReady=from.IsReady,Exercised=exercised,Arguments=args,
                    Layout=space.Layout,Transferred=transferred,Imported=imported,Returned=returned,Success=success,
                    Source=source,Target=target
                });
                if(args.Contains("--exercise-drop") && !args.Contains("--keep-open") && (++ticks>=3 && success || ticks>=12))
                { if(!success)Environment.ExitCode=1;form.Close(); }
            }
            catch(Exception ex) { errors.Add(ex.ToString()); }
        };
        form.Shown+=(_,_)=>timer.Start();try{Application.Run(form);}finally{if(redirectedStarted)redirectedSession?.Restore();}
    }
    private static uint DropFrom(ShellFolderView source,DesktopHost target,Point point,Action? beforeDrop=null,string? reportRoot=null,string? fileName=null)
    {
        var view=source.GetFolderView() ?? throw new InvalidOperationException();
        try
        {
            var selected=0;
            if(fileName is not null)
            {
                selected=-1;Native.Check(view.ItemCount(2,out var count));
                for(var i=0;i<count;i++)
                {
                    Native.Check(view.Item(i,out var pidl));
                    try{if(ShellFolderView.ItemName(view,pidl)==fileName){selected=i;break;}}
                    finally{Marshal.FreeCoTaskMem(pidl);}
                }
                if(selected<0)throw new FileNotFoundException("Не найден тестовый файл для обратного переноса.",fileName);
            }
            Native.Check(view.SelectItem(selected,1|4|16));
            var iid=typeof(System.Runtime.InteropServices.ComTypes.IDataObject).GUID;
            Native.Check(view.Items(1,ref iid,out var obj));
            try {
                var data=(System.Runtime.InteropServices.ComTypes.IDataObject)obj;
                var before=OleFiles.Read(data);beforeDrop?.Invoke();var after=OleFiles.Read(data);
                if(reportRoot is not null)JsonStore.Write(Path.Combine(reportRoot,"source-paths.json"),new { Before=before,After=after,AllSourcesExist=before.All(File.Exists) });
                return target.ExerciseDrop(data,point);
            }
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
