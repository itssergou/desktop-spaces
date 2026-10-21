using DesktopSpaces.App;
using DesktopSpaces.Core;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        var root=Path.Combine(Path.GetTempPath(),"DesktopSpaces.Windows.Tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var config=AppConfig.Create(Path.Combine(root,"Desktop"),Path.Combine(root,"Spaces"));
            config.AddSpace("Remove this",Path.Combine(root,"Spaces"));
            using var form=new SettingsForm(config,true,_=>{},_=>{},(_,_)=>{},_=>{},()=>{},_=>{});
            var list=form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<ListView>().Single();
            _=list.Handle;
            list.Items[2].Selected=true;
            var id=config.Spaces[2].Id;
            if(list.SelectedItems.Count!=1 || !Equals(list.SelectedItems[0].Tag,id))throw new Exception("Test selection failed.");
            config.RemoveSpace(id);
            form.RefreshSpaces(); // Regression: selected index 2 no longer exists in the model.
            if(list.Items.Count!=2 || list.Items.Cast<ListViewItem>().Any(i=>Equals(i.Tag,id)))throw new Exception("Removed row still exists.");
            Console.WriteLine("PASS Removing selected final row refreshes without stale-index exception");
            var second=config.Spaces[1];var third=config.AddSpace("Work",Path.Combine(root,"Spaces"));
            form.RefreshSpaces(third.Id);config.MoveSpace(third.Id,1);form.RefreshSpaces();
            if(!Equals(list.SelectedItems[0].Tag,third.Id) || !Equals(list.Items[2].Tag,second.Id))throw new Exception("Selection changed identity after reorder.");
            Console.WriteLine("PASS Selection retains its identifier after drag reorder");
            var command=StartupRegistration.Command(Path.Combine(root,"App with spaces","DesktopSpaces.exe"));
            if(!command.StartsWith('"') || !command.EndsWith("\" --autostart"))throw new Exception("Unquoted startup command.");
            Console.WriteLine("PASS Startup command quotes executable paths containing spaces");
            var original=Path.Combine(root,"Источник с пробелами.txt");var redirected=Path.Combine(root,"Other","Источник с пробелами.txt");
            File.WriteAllText(original,"keep");var currentPath=original;
            var changing=new ChangingFileData(()=>OleFiles.Freeze([currentPath]));
            var frozen=OleFiles.FreezeBeforeChange(changing,()=>currentPath=redirected);
            if(OleFiles.Read(changing).Single()!=redirected || OleFiles.Read(frozen).Single()!=original || !File.Exists(original))throw new Exception("Source path changed after snapshot.");
            Console.WriteLine("PASS File source snapshot survives Desktop-relative path changes");
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
        finally
        {
            var expected=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"DesktopSpaces.Windows.Tests"))+Path.DirectorySeparatorChar;
            if(!Path.GetFullPath(root).StartsWith(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe test cleanup.");
            Directory.Delete(root,true);
        }
    }
}

internal sealed class ChangingFileData(Func<System.Runtime.InteropServices.ComTypes.IDataObject> current):System.Runtime.InteropServices.ComTypes.IDataObject
{
    public void GetData(ref System.Runtime.InteropServices.ComTypes.FORMATETC format,out System.Runtime.InteropServices.ComTypes.STGMEDIUM medium)=>current().GetData(ref format,out medium);
    public void GetDataHere(ref System.Runtime.InteropServices.ComTypes.FORMATETC format,ref System.Runtime.InteropServices.ComTypes.STGMEDIUM medium)=>current().GetDataHere(ref format,ref medium);
    public int QueryGetData(ref System.Runtime.InteropServices.ComTypes.FORMATETC format)=>current().QueryGetData(ref format);
    public int GetCanonicalFormatEtc(ref System.Runtime.InteropServices.ComTypes.FORMATETC format,out System.Runtime.InteropServices.ComTypes.FORMATETC output)=>current().GetCanonicalFormatEtc(ref format,out output);
    public void SetData(ref System.Runtime.InteropServices.ComTypes.FORMATETC format,ref System.Runtime.InteropServices.ComTypes.STGMEDIUM medium,bool release)=>current().SetData(ref format,ref medium,release);
    public System.Runtime.InteropServices.ComTypes.IEnumFORMATETC EnumFormatEtc(System.Runtime.InteropServices.ComTypes.DATADIR direction)=>current().EnumFormatEtc(direction);
    public int DAdvise(ref System.Runtime.InteropServices.ComTypes.FORMATETC format,System.Runtime.InteropServices.ComTypes.ADVF flags,System.Runtime.InteropServices.ComTypes.IAdviseSink sink,out int connection)=>current().DAdvise(ref format,flags,sink,out connection);
    public void DUnadvise(int connection)=>current().DUnadvise(connection);
    public int EnumDAdvise(out System.Runtime.InteropServices.ComTypes.IEnumSTATDATA? enumerator)=>current().EnumDAdvise(out enumerator);
}
