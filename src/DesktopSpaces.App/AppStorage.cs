namespace DesktopSpaces.App;

internal static class AppStorage
{
    // A child of an MSIX host can inherit AppData virtualization. A path under
    // UserProfile is shared with Explorer and does not change with the launcher.
    internal static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".desktop-spaces");

    internal static IEnumerable<string> LegacyRoots()
    {
        var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local,"DesktopSpaces");
        var packages=Path.Combine(local,"Packages");
        if(!Directory.Exists(packages))yield break;
        foreach(var package in Directory.EnumerateDirectories(packages))
            yield return Path.Combine(package,"LocalCache","Local","DesktopSpaces");
    }

    internal static bool ImportLegacy(string root,IEnumerable<string> candidates,string? virtualRoot=null)
    {
        var configPath=Path.Combine(root,"settings.json");
        if(File.Exists(configPath) || File.Exists(configPath+".bak"))return false;
        var source=candidates.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p=>new {Root=p,Config=new FileInfo(Path.Combine(p,"settings.json"))})
            .Where(p=>p.Config.Exists).OrderByDescending(p=>p.Config.LastWriteTimeUtc).FirstOrDefault();
        if(source is null)return false;
        var config=JsonStore.LoadConfig(source.Config.FullName);
        virtualRoot??=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopSpaces");
        foreach(var space in config.Spaces.Skip(1))
        {
            var folder=space.Folder;
            if(folder.StartsWith(virtualRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            {
                var physical=Path.Combine(source.Root,Path.GetRelativePath(virtualRoot,folder));
                if(Directory.Exists(physical))folder=physical;
            }
            if(!Directory.Exists(folder))throw new DirectoryNotFoundException("Не найдена исходная папка пространства: "+folder);
            var target=Path.Combine(root,"Spaces",space.Id.ToString("N"));
            if(Directory.Exists(target))target=Path.Combine(root,"Spaces",Guid.NewGuid().ToString("N"));
            CopyFolder(new DirectoryInfo(folder),target);space.Folder=target;
        }
        // Commit after copying every file. Interrupted copies never replace the
        // original settings or data; a later attempt uses another empty folder.
        config.Validate();JsonStore.Write(configPath,config);
        return true;
    }

    private static void CopyFolder(DirectoryInfo source,string destination)
    {
        if((source.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Перенос папки со ссылкой требует отдельной миграции: "+source.FullName);
        Directory.CreateDirectory(destination);
        foreach(var entry in source.EnumerateFileSystemInfos())
        {
            if((entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Перенос ссылки требует отдельной миграции: "+entry.FullName);
            if(entry is DirectoryInfo folder)CopyFolder(folder,Path.Combine(destination,entry.Name));
            else ((FileInfo)entry).CopyTo(Path.Combine(destination,entry.Name),false);
        }
    }
}
