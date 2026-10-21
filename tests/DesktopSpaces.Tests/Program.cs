using DesktopSpaces.Core;

var tests=new (string Name,Action<Fixture> Run)[]
{
    ("Default spaces preserve original desktop",f=>{var c=f.Config();c.Validate();Assert(c.Spaces.Count==2 && Paths.Equal(c.Spaces[0].Folder,f.Original));Assert(c.Spaces[1].Folder.StartsWith(f.Storage));}),
    ("New spaces have unique paths without moving files",f=>{var c=f.Config();var a=c.AddSpace("Games",f.Storage);var b=c.AddSpace("Games",f.Storage);Assert(a.Id!=b.Id && a.Folder!=b.Folder);Assert(Directory.Exists(a.Folder));Assert(File.ReadAllText(f.Sample)=="keep me");}),
    ("Invalid active identifier falls back to first",f=>{var c=f.Config();c.ActiveSpaceId=Guid.NewGuid();c.Validate();Assert(c.ActiveSpaceId==c.Spaces[0].Id);}),
    ("First folder cannot be replaced",f=>{var c=f.Config();c.Spaces.Reverse();Throws<InvalidDataException>(c.Validate);}),
    ("Duplicate identifiers rejected",f=>{var c=f.Config();c.Spaces[1].Id=c.Spaces[0].Id;Throws<InvalidDataException>(c.Validate);}),
    ("Duplicate paths rejected",f=>{var c=f.Config();c.Spaces[1].Folder=f.Original;Throws<InvalidDataException>(c.Validate);}),
    ("Nested space paths rejected",f=>{var c=f.Config();c.Spaces[1].Folder=Path.Combine(f.Original,"Nested");Throws<InvalidDataException>(c.Validate);}),
    ("Path prefix alone is not nesting",f=>{Assert(!Paths.Overlap(Path.Combine(f.Root,"A"),Path.Combine(f.Root,"AB")));}),
    ("Volume root overlaps every folder on that volume",f=>{Assert(Paths.Overlap(Path.GetPathRoot(f.Root)!,f.Original));}),
    ("Names are validated before creating folders",f=>{var c=f.Config();Throws<ArgumentException>(()=>c.AddSpace(" ",f.Storage));Assert(c.Spaces.Count==2);}),
    ("Dragging order preserves identifiers folders and active space",f=>{var c=f.Config();var third=c.AddSpace("Work",f.Storage);var second=c.Spaces[1];c.ActiveSpaceId=third.Id;c.MoveSpace(third.Id,1);Assert(c.Spaces[1]==third && c.Spaces[2]==second && c.ActiveSpaceId==third.Id);c.Validate();}),
    ("Original desktop cannot be reordered",f=>{var c=f.Config();var first=c.Spaces[0];c.MoveSpace(first.Id,1);c.MoveSpace(c.Spaces[1].Id,0);Assert(c.Spaces[0]==first);}),
    ("Removing a space preserves every file",f=>{var c=f.Config();var space=c.AddSpace("Remove",f.Storage);var sample=Path.Combine(space.Folder,"keep.txt");File.WriteAllText(sample,"keep");c.RemoveSpace(space.Id);Assert(c.Spaces.All(s=>s.Id!=space.Id) && File.ReadAllText(sample)=="keep");c.Validate();}),
    ("Removing original or active space is rejected",f=>{var c=f.Config();Throws<InvalidOperationException>(()=>c.RemoveSpace(c.Spaces[0].Id));c.ActiveSpaceId=c.Spaces[1].Id;Throws<InvalidOperationException>(()=>c.RemoveSpace(c.ActiveSpaceId));Assert(c.Spaces.Count==2);}),
    ("Relative folder paths rejected",f=>{var c=f.Config();c.Spaces[1].Folder="relative";Throws<InvalidDataException>(c.Validate);}),
    ("Null serialized entries fail validation",f=>{var c=f.Config();c.Spaces[0]=null!;Throws<InvalidDataException>(c.Validate);}),
    ("Unknown schema rejected",f=>{var c=f.Config();c.Version=99;Throws<InvalidDataException>(c.Validate);}),
    ("Atomic settings save keeps a valid backup",f=>{var p=Path.Combine(f.Root,"settings.json");var c=f.Config();JsonStore.Write(p,c);c.Spaces[1].Name="Games";JsonStore.Write(p,c);Assert(JsonStore.LoadConfig(p).Spaces[1].Name=="Games");Assert(JsonStore.Read<AppConfig>(p+".bak").Spaces[1].Name!="Games");Assert(!Directory.GetFiles(f.Root,"*.tmp").Any());}),
    ("Corrupt primary settings recover from backup",f=>{var p=Path.Combine(f.Root,"settings.json");var c=f.Config();JsonStore.Write(p,c);JsonStore.Write(p,c);File.WriteAllText(p,"{broken");Assert(JsonStore.LoadConfig(p).OriginalDesktop==f.Original);}),
    ("Missing primary settings recover from backup",f=>{var p=Path.Combine(f.Root,"settings.json");JsonStore.Write(p,f.Config());JsonStore.Write(p,f.Config());File.Delete(p);Assert(JsonStore.LoadConfig(p).OriginalDesktop==f.Original);}),
    ("Invalid semantic config recovers from backup",f=>{var p=Path.Combine(f.Root,"settings.json");var c=f.Config();JsonStore.Write(p,c);c.Spaces.Clear();JsonStore.Write(p,c);Assert(JsonStore.LoadConfig(p).Spaces.Count==2);}),
    ("Session journal is written before changing Windows",f=>{var d=f.Desktop();var session=new DesktopSession(d,f.Journal);session.Start(f.Original,123,456);d.BeforeSet=p=>{var r=JsonStore.Read<RecoveryRecord>(f.Journal);Assert(r.OwnedPaths.Any(x=>Paths.Equal(x,p)));};session.Switch(f.Second);Assert(Paths.Equal(d.Current,f.Second));Assert(File.ReadAllText(f.Sample)=="keep me");}),
    ("Switching to same folder does not call Windows",f=>{var d=f.Desktop();var s=f.Session(d);s.Switch(f.Original);Assert(d.Sets==0);}),
    ("Session refuses an externally changed initial desktop",f=>{var d=new FakeDesktop(f.Second);var s=new DesktopSession(d,f.Journal);Throws<InvalidOperationException>(()=>s.Start(f.Original,1,1));Assert(!File.Exists(f.Journal));}),
    ("Missing target folder leaves original untouched",f=>{var d=f.Desktop();var s=f.Session(d);Throws<DirectoryNotFoundException>(()=>s.Switch(Path.Combine(f.Root,"missing")));Assert(d.Sets==0);}),
    ("Failure before path mutation preserves previous desktop",f=>{var d=f.Desktop();var s=f.Session(d);d.BeforeSet=_=>throw new IOException("injected failure");Throws<IOException>(()=>s.Switch(f.Second));Assert(Paths.Equal(d.Current,f.Original));}),
    ("Failure after path mutation rolls back",f=>{var d=f.Desktop();var s=f.Session(d);d.AfterSet=p=>{if(Paths.Equal(p,f.Second))throw new IOException("injected failure");};Throws<IOException>(()=>s.Switch(f.Second));Assert(Paths.Equal(d.Current,f.Original));}),
    ("Unconfirmed Windows change is rejected",f=>{var d=f.Desktop();var s=f.Session(d);d.IgnoreSet=true;Throws<IOException>(()=>s.Switch(f.Second));Assert(Paths.Equal(d.Current,f.Original));}),
    ("Failed rollback retains recoverable journal",f=>{var d=f.Desktop();var s=f.Session(d);d.AfterSet=_=>throw new IOException("after set");Throws<AggregateException>(()=>s.Switch(f.Second));Assert(File.Exists(f.Journal));d.AfterSet=null;DesktopSession.Recover(d,f.Journal);Assert(Paths.Equal(d.Current,f.Original));}),
    ("Crash recovery restores original without touching files",f=>{var d=f.Desktop();var s=f.Session(d);s.Switch(f.Second);Assert(DesktopSession.Recover(d,f.Journal));Assert(Paths.Equal(d.Current,f.Original));Assert(!File.Exists(f.Journal));Assert(File.ReadAllText(f.Sample)=="keep me");}),
    ("Recovery is idempotent",f=>{Assert(!DesktopSession.Recover(f.Desktop(),f.Journal));}),
    ("External path change is not overwritten during recovery",f=>{var d=f.Desktop();var s=f.Session(d);s.Switch(f.Second);d.Current=f.Third;var n=d.Sets;Throws<InvalidOperationException>(()=>DesktopSession.Recover(d,f.Journal));Assert(Paths.Equal(d.Current,f.Third) && d.Sets==n && File.Exists(f.Journal));}),
    ("External path change stops further switching",f=>{var d=f.Desktop();var s=f.Session(d);d.Current=f.Third;Throws<InvalidOperationException>(()=>s.Switch(f.Second));Assert(Paths.Equal(d.Current,f.Third));}),
    ("Failed recovery retains journal",f=>{var d=f.Desktop();var s=f.Session(d);s.Switch(f.Second);d.IgnoreSet=true;Throws<IOException>(()=>DesktopSession.Recover(d,f.Journal));Assert(File.Exists(f.Journal));}),
    ("A second session cannot overwrite a live journal",f=>{var d=f.Desktop();f.Session(d);var s=new DesktopSession(d,f.Journal);Throws<InvalidOperationException>(()=>s.Start(f.Original,2,2));Assert(JsonStore.Read<RecoveryRecord>(f.Journal).ProcessId==1);}),
    ("All intermediate paths remain recoverable",f=>{var d=f.Desktop();var s=f.Session(d);s.Switch(f.Second);s.Switch(f.Third);d.Current=f.Second;DesktopSession.Recover(d,f.Journal);Assert(Paths.Equal(d.Current,f.Original));}),
    ("Restore removes both primary and backup journals",f=>{var d=f.Desktop();var s=f.Session(d);s.Switch(f.Second);Assert(File.Exists(f.Journal+".bak"));s.Restore();Assert(!File.Exists(f.Journal) && !File.Exists(f.Journal+".bak"));}),
    ("Desktop icon visibility is journaled before hiding and recovered",f=>{var d=f.Desktop();var s=f.Session(d);s.RememberDesktopIcons(true);Assert(JsonStore.Read<RecoveryRecord>(f.Journal).RestoreDesktopIcons);var called=false;DesktopSession.Recover(d,f.Journal,restoreSurface:r=>{Assert(r.RestoreDesktopIcons && File.Exists(f.Journal));called=true;});Assert(called && !File.Exists(f.Journal));}),
    ("Surface recovery failure retains the journal for retry",f=>{var d=f.Desktop();var s=f.Session(d);s.RememberDesktopIcons(true);Throws<IOException>(()=>DesktopSession.Recover(d,f.Journal,restoreSurface:_=>throw new IOException("surface")));Assert(File.Exists(f.Journal));Assert(DesktopSession.Recover(d,f.Journal));}),
    ("Old watchdog cannot restore a newer session",f=>{var d=f.Desktop();var s=f.Session(d);s.Switch(f.Second);Assert(!DesktopSession.Recover(d,f.Journal,99,99));Assert(Paths.Equal(d.Current,f.Second) && File.Exists(f.Journal));}),
    ("PID reuse with a different start time does not restore",f=>{var d=f.Desktop();var s=f.Session(d);s.Switch(f.Second);Assert(!DesktopSession.Recover(d,f.Journal,1,2));Assert(Paths.Equal(d.Current,f.Second));Assert(DesktopSession.Recover(d,f.Journal,1,1));})
};
var failures=0;
foreach(var test in tests)
{
    using var fixture=new Fixture();
    try{test.Run(fixture);Console.WriteLine("PASS "+test.Name);}
    catch(Exception ex){failures++;Console.Error.WriteLine("FAIL "+test.Name+Environment.NewLine+ex);}
}
Console.WriteLine($"{tests.Length-failures}/{tests.Length} checks passed");
return failures==0?0:1;

static void Assert(bool condition){if(!condition)throw new Exception("Assertion failed");}
static void Throws<T>(Action action)where T:Exception
{
    try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);
}

sealed class FakeDesktop(string path):IDesktopPath
{
    public string Current=path;
    public int Sets;
    public bool IgnoreSet;
    public Action<string>? BeforeSet,AfterSet;
    public string Get()=>Current;
    public void Set(string value){Sets++;BeforeSet?.Invoke(value);if(!IgnoreSet)Current=value;AfterSet?.Invoke(value);}
}
sealed class Fixture:IDisposable
{
    public string Root{get;}=Path.Combine(Path.GetTempPath(),"DesktopSpaces.Tests",Guid.NewGuid().ToString("N"));
    public string Original=>Path.Combine(Root,"Desktop");
    public string Storage=>Path.Combine(Root,"Spaces");
    public string Second=>Path.Combine(Storage,"second");
    public string Third=>Path.Combine(Storage,"third");
    public string Journal=>Path.Combine(Root,"recovery.json");
    public string Sample=>Path.Combine(Original,"existing.txt");
    public Fixture(){Directory.CreateDirectory(Original);Directory.CreateDirectory(Second);Directory.CreateDirectory(Third);File.WriteAllText(Sample,"keep me");}
    public AppConfig Config()=>AppConfig.Create(Original,Storage);
    public FakeDesktop Desktop()=>new(Original);
    public DesktopSession Session(FakeDesktop d){var s=new DesktopSession(d,Journal);s.Start(Original,1,1);return s;}
    public void Dispose()
    {
        var expected=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"DesktopSpaces.Tests"))+Path.DirectorySeparatorChar;
        if(!Path.GetFullPath(Root).StartsWith(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe test cleanup path");
        Directory.Delete(Root,true);
    }
}
