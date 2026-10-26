# Windows desktop spaces application plan

Status: the 0.1.3 preview is implemented. The user confirmed the main workflow
on Windows 11, including a reboot. Windows 10 validation remains pending.
Original plan date: October 21, 2026.
Project name: DesktopSpaces. Public repository:
https://github.com/itssergou/desktop-spaces.
The source was published on October 25, 2026.
Current validation results: [docs/VALIDATION.md](docs/VALIDATION.md). License: MIT.

This document preserves the original requirements and implementation plan.
The implemented behavior and confirmed limitations of the first version are
described in [README.md](README.md).

## Purpose

Extend the Windows desktop with separate spaces for shortcuts, files and
folders. Users organize each space as they prefer. The first space is the
user's existing desktop. It does not need to start empty, and the app must not
automatically clear it.

## Agreed requirements

- Support Windows 10 and Windows 11.
- Run in the system tray, without an application taskbar button.
- Open settings from the tray icon menu.
- Provide two spaces by default, with the second initially empty.
- Let users change the number of spaces, their names and their order.
- Switch spaces by double-clicking empty desktop space.
- Cycle through all spaces when more than one exists.
- Do not switch when the user double-clicks an item.
- Switch all monitors together.
- Keep separate contents and icon positions for each space.
- Preserve familiar Windows operations: opening, selection, renaming, creation,
  copying, pasting, deletion, context menus and drag and drop.
- Originally requested: saves to Desktop from other applications should target
  the space active when the destination path is resolved. The first version's
  agreed fallback keeps the system Desktop location on the first space; see
  the README for saving to other spaces.
- Do not switch or close application windows when changing spaces.
- Return to the first space on a normal full exit.
- Keep other spaces' contents accessible as ordinary folders.
- Prepare the source for a public open-source GitHub repository.

## Keyboard switching

- Alt + 1-9 selects the corresponding space.
- Use the number row; do not assign the numeric keypad by default.
- Do not limit the number of spaces to nine. Other spaces remain available
  through the tray menu.
- Selecting the active space or a nonexistent space has no effect.
- Holding a shortcut must not trigger repeated switches.
- Handle shortcuts in the desktop context and during supported drag transfers.
  Preserve normal input in other applications.
- A permanent global registration that consumes Alt + number keys in other
  applications does not meet this requirement.
- Allow users to disable or change the shortcuts.
- On a detected conflict, do not take the shortcut from another application.
  Show a clear status and keep tray-based switching available.

### Availability check performed on October 21, 2026

On the development computer, Alt + 1-9 was successfully registered using
`RegisterHotKey` with `MOD_ALT | MOD_NOREPEAT` and immediately released with
`UnregisterHotKey`. Registration and release succeeded for all nine shortcuts.

The official Windows shortcut list does not assign Alt + number keys to the
desktop. Neither that list nor the registration test proves the absence of
local key handlers, third-party hooks or conflicts on another computer.
Handling must therefore respect the current context and input mechanism.

## Moving items between spaces

Target workflow:

1. The user starts dragging a file, folder or shortcut.
2. While holding the left mouse button, they press Alt + the target space number.
3. The space switches and the drag operation continues.
4. The user releases Alt, chooses a location and releases the left mouse button.
5. The item is transferred. If the drag is canceled, it remains at the source.

Switching alone must not move contents before the mouse button is released.
Test multiple items, repeated switching during one drag, Escape cancellation,
name conflicts and normal copy/shortcut modifiers. Account for Alt's effect on
the action performed when the user drops an item. Also support cut and paste.

## Technical approach: prototype first

The initial proposed stack was C# and .NET, with WPF for settings and
Win32/Windows Shell integration. Select the .NET version against the supported
Windows 10 and Windows 11 builds. Choose the final display and save-routing
approach based on prototype results. The implemented settings UI uses Windows
Forms; current architecture is documented separately.

Each space needs stable storage. Switching must not require copying entire
folders or large files. A custom Windows Shell folder view is a candidate to
validate, rather than proof of full desktop compatibility.

Prototype acceptance criteria:

- Two spaces, with both switching methods working across all monitors.
- Familiar context menus, selection, creation and dragging.
- A drag survives switching, with its source data remaining available.
- Test saves to Desktop from external applications, including an already open
  save dialog, a stored absolute path and an application that caches the Desktop
  path. Merely changing the displayed folder does not solve these cases.
- Correct Win + D behavior, window order and focus.
- Checks for the user desktop, Public Desktop, system icons and OneDrive.
- Exiting restores the first desktop, with all spaces' files still accessible.

If requirements cannot be met, record the specific limitations and discuss the
tradeoff before implementing the main version. Do not describe partial
Windows desktop compatibility as full compatibility.

## Stages

1. Prototype the integration and check the criteria above.
2. Implement storage, stable identifiers and saved layouts.
3. Implement switching, file transfers, error handling and name conflicts.
4. Add the tray, settings, optional startup and space management.
5. Handle app/Explorer crashes, reboot and monitor changes.
6. Validate Windows 10 and Windows 11, different scaling and multiple monitors.
7. Prepare documentation, release packages and the public repository.

Removing a space containing files must preserve those files or offer a way to
move them. Applications targeted by shortcuts must not be affected.

## Public open-source project

- Open source with reproducible builds from the public repository.
- An open-source license is required. MIT was selected for publication.
- A README covering purpose, compatibility, installation, builds and limitations.
- Contribution guidance and issue templates.
- Automated builds and relevant checks in GitHub Actions.
- Built packages published through GitHub Releases.
- Dependency license checks and required notices.
- No user files, local settings, logs or secrets in the source repository.
  The application itself requires no account or cloud service.

## Technical references

- [Windows keyboard shortcuts](https://support.microsoft.com/en-au/windows/keyboard-shortcuts-in-windows-dcc61a57-8ff0-cffe-9796-cb9706c75eec)
- [Shortcut registration](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [OLE drag and drop](https://learn.microsoft.com/en-us/windows/win32/com/drag-and-drop)
- [Shell folder views](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-iexplorerbrowser)
- [Desktop Known Folder paths](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shgetknownfolderpath)
- [MIT license](https://opensource.org/license/mit)
