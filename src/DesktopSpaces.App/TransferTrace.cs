namespace DesktopSpaces.App;

// Opt-in local diagnostics. Never included in release archives.
internal static class TransferTrace
{
    private static string Root=>AppStorage.Root;
    internal static bool Enabled=>File.Exists(Path.Combine(Root,"trace-transfers.enabled"));
    internal static void Write(string stage,object? details=null)
    {
        if(!Enabled)return;
        try
        {
            var path=Path.Combine(Root,"transfers.jsonl");
            if(File.Exists(path) && new FileInfo(path).Length>1024*1024)File.Move(path,path+".old",true);
            File.AppendAllText(path,System.Text.Json.JsonSerializer.Serialize(new {Time=DateTimeOffset.Now,Process=Environment.ProcessId,Stage=stage,Details=details})+Environment.NewLine);
        }
        catch(IOException) { }
        catch(UnauthorizedAccessException) { }
    }
    internal static object Files(string[] paths)=>paths.Select(p=>new {Path=p,Exists=File.Exists(p)||Directory.Exists(p)}).ToArray();
}
