# Architecture

## Layers

- `DesktopSpaces.Core`: space model, path validation, atomic JSON storage,
  desktop redirection transactions and recovery. No Windows UI dependencies.
- `DesktopSpaces.App`: Windows Forms, tray integration, Win32 hooks, COM Windows
  Shell integration, wallpaper and attachment to the desktop surface.
- `DesktopSpaces.Tests`: independent model and failure-recovery checks. The
  runner returns a nonzero exit code on failure.

The first space uses Explorer's original desktop. Additional spaces use
`IExplorerBrowser` and `IFolderView` within a child desktop surface. Icon
positions are read and restored through `IFolderView`, without sending pointers
into Explorer's process. The application does not inject a DLL into Explorer.

Attachment to `SHELLDLL_DefView` depends on the Windows Shell implementation;
it is not a stable public hosting contract for third-party desktops. The app
checks that the surface exists and attempts to reattach after Explorer restarts.
Specific Windows builds still require testing.

## Files and the Desktop path

The first space's folder is not renamed. New spaces receive GUIDs and permanent
folders. Switching the displayed space preserves the first space's system
Known Folder path. Windows Shell performs operations on folder contents.
Folders are not combined through junctions: replacing a path during an OLE drag
could change the meaning of the source's absolute path.

To save from another application into another space, select that space's folder.
The system Desktop location always corresponds to the first space.

## Recovery

The session journal records the original path, process ID and start time before
the app changes desktop visibility. A watchdog monitors that exact process
instance. Legacy redirection transactions, retained for old journals and
diagnostic modes, follow these steps:

1. Validate the original path and write the process identity to the journal.
2. Start the watchdog for that process instance.
3. Before redirecting, journal the previous and allowed destination paths.
4. Call the Windows API and verify the new path.
5. On failure, try to restore the previous path and retain the journal if
   rollback also fails.
6. On exit, session ending, crash or the next launch, restore the original path
   only if the current path belongs to that session's recorded paths.

An unrelated external redirection is left intact and reported as an error.
A lock protects session operations from races with Windows session-ending
handlers. Settings are flushed to a temporary file, then atomically replace the
primary file while retaining its previous version as a backup.

## Input and transfers

Low-level hooks do not permanently register Alt + number shortcuts globally.
They handle input in the desktop/desktop-transfer context. Callbacks perform a
short check and dispatch work to the UI thread. AltGr, Shift and the Windows
key are excluded. The app first checks for another application's global
registration; this cannot detect every third-party hook.

Hit testing within the app's own ListViews can use local pointer-bearing
messages. The original desktop uses MSAA (`IAccessible.accHitTest`), with a UI
Automation fallback. Local pointers must never be sent into another process.
If the app cannot identify the hit element, a double-click does not switch
spaces.

During an OLE drag, the source view is retained. The new view is placed above
it. When returning to the first desktop, the child surface moves below the real
icon list. Extra views are hidden after the mouse button is released. Created
views stay in memory, and the first nine spaces are gradually preloaded while
idle. Continuous dragging also requires manual acceptance testing.

The icon surface uses `WS_EX_LAYERED` with a color key. In its transparent areas,
a second child window with alpha 1/255 receives mouse and OLE events and forwards
them to the active Shell view. Before hiding the original icon list, its
visibility is journaled for watchdog recovery. During a held drag, the source
window remains alive; the 0.1.3 masking behavior is described below. Static
wallpaper rendering is used only in demo mode.

`OleInitialize` runs before creating Shell views. The receiving handler is
queried from the current view (`GetCurrentView`, `IID_IDropTarget`), rather than
`SVGIO_BACKGROUND`. Within the same folder, `IFolderView` updates icon positions
while preserving the relative positions of grouped icons. Windows Shell handles
external file operations. Recreated lists and reset colors are repaired without
rebuilding the entire browser.

`Test-Surface.ps1` exercises real OLE `IDataObject`/`IDropTarget` interfaces using
temporary folders and a moving background. `Test-DesktopIntegration.ps1`
separately checks the real Desktop Known Folder, absence of a taskbar window,
icon visibility, and recovery after normal exit or forced termination. Checks
that change the user's actual system Desktop path are excluded from regular CI.

## 0.1.2 changes

`BrowseToIDList` receives a PIDL with an explicit This PC/volume prefix so the
file's parent does not depend on the current Desktop Known Folder. Otherwise,
the Shell can bind a normal folder through a Desktop-relative PIDL, and later
redirection can make an existing data object point to a different path.

In the earlier redirecting mode, changing the system path during a drag from
the original desktop was delayed until `IDataObject` became available. Source
paths were materialized as absolute `CF_HDROP` entries before switching the
path. The same stable data object was used for Drop, including drops over icons.
File clipboard data from the original desktop was stabilized before switching,
while preserving `Preferred DropEffect`.

`FitDesktop` changes size and window order only after an actual size change or
reattachment. ListViews use double buffering. Valid HWNDs and names keyed by
PIDL are cached. Layout collection is skipped while the left mouse button is
held. Periodic JSON saves use an immutable snapshot on a background thread, a
shared write gate and a revision number, preventing an older save from
replacing a newer one. Redirection journals are still synchronously flushed
before changing Windows state.

## 0.1.3 changes

The launching process could pass MSIX AppData virtualization to the app. Inside
the app, `%LOCALAPPDATA%\DesktopSpaces` contained private files, while ordinary
Explorer saw a different folder at the same path. In-process checks missed that
difference. `AppStorage.Root` now uses `%USERPROFILE%\.desktop-spaces`, outside
virtualized AppData. First-run migration selects the newest configuration,
resolves its paths against the package's physical `LocalCache`, and copies files
before atomically writing the new configuration. Originals are retained and a
successful migration is not repeated on subsequent launches.

Switching no longer changes the system folder path or clipboard. Older
transaction code remains for recovering previous journals and for diagnostic
probes. Returning to the first space restores focus to Explorer's real icon
list, so a hidden folder view does not receive Ctrl + V instead of the current
desktop.

While the mouse button is held, an inactive source window is retained with an
empty window region. This prevents its icons from showing through the
transparent surface. Returning, exiting or watchdog recovery resets the real
list's window region. The indicator timer does not hide a held drag's source,
and view preloading pauses until the mouse button is released.

Dropping over a file selects the nearest free cell. After the item appears,
`GetItemPosition` verifies its actual position; a successful HRESULT alone is
not proof of the final layout. Checks continue through asynchronous Shell Drop
completion, and view maintenance repairs remaining collisions. Reading the
placement result does not invoke `MaintainView`, which could restore an earlier
layout over the new position.

Layout and transfer identities use `SIGDN_PARENTRELATIVEPARSING` names, including
file extensions. This avoids relying on friendly display names that can omit
extensions when Explorer hides them.
