# DesktopSpaces

An MIT-licensed Windows tray utility with independent desktop spaces for
shortcuts, files and folders. The first space is the original Windows desktop;
additional spaces use native Windows Shell folder views.

This is a **0.1.3 preview**, not a fully validated Windows shell replacement.
[Download 0.1.3](https://github.com/itssergou/desktop-spaces/releases/tag/v0.1.3).
Double-click empty desktop space to cycle, or use Alt + 1–9. Settings offer
Ctrl + Alt as an alternative, space management, an indicator and opt-in startup.
Exit from the tray to show the original Windows desktop.
The application interface is currently in Russian.

The portable x64 release includes .NET. Run `DesktopSpaces.exe --demo` to try
isolated folders without redirecting the real desktop. For source builds use
Windows and the .NET 8 SDK:

```powershell
dotnet build DesktopSpaces.sln -c Release
dotnet run --project tests/DesktopSpaces.Tests -c Release
.\scripts\Publish.ps1
```

The Desktop Known Folder stays on the first space. To save from another
application to an additional space, choose that space's folder. OneDrive-managed
desktops are blocked in this preview. Public Desktop items and system icons
remain on the first space; additional background menus are Shell folder menus.
The user confirmed transfers in both directions and startup, saved state and
wallpaper after reboot on their Windows 11 system. Windows 10 and other DPI
and monitor configurations still need acceptance testing. See [VALIDATION.md](VALIDATION.md).

User data is stored in `%USERPROFILE%\.desktop-spaces`, outside MSIX AppData
virtualization. Legacy data, including package-private LocalCache storage, is
copied on first launch and originals are retained. Removing a space from
the list preserves its folder. A journal and watchdog restore the original
desktop after an app crash. Run `DesktopSpaces.exe --recover` after stopping
the app if manual recovery is necessary. Another program's redirection is
never overwritten automatically. See [the main README](../README.md) and
[CONTRIBUTING.md](../CONTRIBUTING.md) for details.
