# DesktopSpaces

An MIT-licensed Windows tray utility with independent desktop spaces for
shortcuts, files and folders. The first space is the original Windows desktop;
additional spaces use native Windows Shell folder views.

This is a **0.1.2 preview**, not a fully validated Windows shell replacement.
Double-click empty desktop space to cycle, or use Alt + 1–9. Settings offer
Ctrl + Alt as an alternative, space management, an indicator and opt-in startup.
Exit from the tray to restore the original desktop path.

The portable x64 release includes .NET. Run `DesktopSpaces.exe --demo` to try
isolated folders without redirecting the real desktop. For source builds use
Windows and the .NET 8 SDK:

```powershell
dotnet build DesktopSpaces.sln -c Release
dotnet run --project tests/DesktopSpaces.Tests -c Release
.\scripts\Publish.ps1
```

The Desktop Known Folder follows the active space. Applications caching an
absolute path can continue saving to the previous folder. OneDrive-managed
desktops are blocked in this preview. Public Desktop items and system icons
remain on the first space; additional background menus are Shell folder menus.
Continuous drag transfer, Windows 10 and physical multi-monitor configurations
still need acceptance testing. See [VALIDATION.md](VALIDATION.md).

User data is stored in `%LOCALAPPDATA%\DesktopSpaces`. Removing a space from
the list preserves its folder. A journal and watchdog restore the original
desktop after an app crash. Run `DesktopSpaces.exe --recover` after stopping
the app if manual recovery is necessary. Another program's redirection is
never overwritten automatically. See [the main README](../README.md) and
[CONTRIBUTING.md](../CONTRIBUTING.md) for details.
