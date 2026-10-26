# DesktopSpaces

A Windows tray utility that gives your desktop separate spaces for shortcuts,
files and folders. Open source under the MIT license.

**Status: 0.1.3 preview for testing on real systems.** The full Windows 10/11
and multi-monitor compatibility matrix is still incomplete. See
[validation results and remaining checks](docs/VALIDATION.md).

[Download 0.1.3](https://github.com/itssergou/desktop-spaces/releases/tag/v0.1.3)
· [Report a bug](https://github.com/itssergou/desktop-spaces/issues/new/choose)

## Features

- The first space is your original Windows desktop.
- Two spaces by default; add more in settings.
- Double-click empty desktop space to cycle through spaces.
- Select a space with Alt + 1-9, or choose Ctrl + Alt + 1-9 in settings.
- All monitors switch together.
- Additional spaces use Windows Shell folder views, with native icons, context
  menus, file creation, renaming, clipboard operations and drag and drop.
- Space names, order and icon positions are saved. The first space cannot be
  removed or moved to another number.
- Drag rows in settings to reorder spaces. Press F2 or slowly click an already
  selected name again to rename it.
- Additional spaces let the real desktop wallpaper show through. The user
  confirmed Wallpaper Engine on their Windows 11 configuration.
- Exiting the app shows the original Windows desktop.
- Space contents remain accessible as ordinary folders. Removing a space from
  the list does not delete its contents.
- No account, cloud service or administrator privileges are required.

## Getting started

Extract `DesktopSpaces-0.1.3-win-x64.zip` into a permanent folder and run
`DesktopSpaces.exe`. The portable build includes the .NET runtime, so a separate
.NET installation is not required. The app has no taskbar button. Its tray icon
may appear in the hidden icons area. The application interface is currently in
Russian.

Left-click the tray icon to open settings. Right-click for spaces, their folders
and Exit. Startup at Windows sign-in is optional and can be enabled in settings.
When enabled, starting the app from a new folder updates the startup entry to
point to the current executable.

To try the app without changing your desktop:

```powershell
.\DesktopSpaces.exe --demo
```

Demo mode uses separate folders in `%TEMP%\DesktopSpaces.Demo`. Its window
simulates a desktop area and does not change the system Desktop folder path.

## Moving items between spaces

Start dragging an item while holding the left mouse button. Press Alt + the
space number, release Alt, then drop the item. The source view stays available
for the duration of the operation. Continuous drag transfer still needs
acceptance testing across all supported Windows versions.

Cut and paste also works: Ctrl + X, switch spaces, then Ctrl + V.

Use the number row above the letters. Right Alt (AltGr) is not intercepted.
Space shortcuts should not consume input in other applications. At startup and
when settings change, the app checks for conflicts with registered global
shortcuts. Other keyboard hooks may require choosing Ctrl + Alt or disabling
space shortcuts.

## Saving files

The system **Desktop** location always refers to the first space. Switching
changes the visible folder while preserving the system folder path. Files are
not moved when you switch spaces. To save from another application into an
additional space, choose that space's folder; you can open it from the tray menu
or settings. An open document continues to save to its existing path.

Compatibility is not guaranteed with OneDrive Known Folder Move, corporate
folder redirection, Explorer replacements or other desktop organizers. This
preview blocks OneDrive-managed desktops to avoid changing their sync binding.
Full support requires separate testing and a compatible design.

System icons and Public Desktop shortcuts remain on the first space. Additional
spaces show their own folders. Their background menus are Windows Shell folder
menus and can differ from the real desktop menu. On Windows 11, they may use the
classic menu style.

## Data and recovery

Settings and space folders are stored in `%USERPROFILE%\.desktop-spaces`:

- `settings.json` and its `.bak` file: settings and icon positions.
- `Spaces\<id>`: additional space contents.
- `recovery.json`: the journal for an unfinished session.
- `last-error.log`: the most recent local error.

Storage outside AppData is visible at the same physical path to Explorer and
the app, including launches from an MSIX application. On first launch, legacy
data from AppData and package-private `LocalCache` folders is copied into the
new storage location. Originals are retained. After a successful migration,
the app uses only the new storage location.

The journal and watchdog restore the visibility and window region of the
original desktop icons after a crash. Recovery of folder redirections left by
older versions is retained. Another program's change to the system folder path
is not overwritten.

For manual recovery after a crash, stop the running instance and run:

```powershell
.\DesktopSpaces.exe --recover
```

A power loss can stop both processes before recovery. Restarting the app or
running `--recover` processes the saved journal. Do not delete the data folder
until the desktop has been restored and any files you need have been moved to
another location.

## Building from source

You need Windows and the .NET 8 SDK with Windows Desktop support. The targets
are Windows 10 x64 and Windows 11 x64. ARM64 and x86 have not been validated.

```powershell
dotnet build DesktopSpaces.sln -c Release
dotnet run --project tests/DesktopSpaces.Tests -c Release
.\scripts\Publish.ps1
```

The release archive, executable and checksums are written to `artifacts`.
Use `scripts/GenerateIcon.ps1` to regenerate the icon from `assets/icon.svg`.
The ICO asset is included in the repository.

The application has no external NuGet library dependencies. .NET and standard
Windows components are platform dependencies. To update the runtime bundled
with the portable build, rebuild it using an up-to-date SDK. Migration to
.NET 10 LTS is planned before .NET 8 reaches the end of support.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md), the
[architecture](docs/ARCHITECTURE.md) and the [project plan](PLAN.md).
Review attachments before posting user files, settings or logs containing
personal paths in public issues.

A shorter overview is available in [docs/README.en.md](docs/README.en.md).
