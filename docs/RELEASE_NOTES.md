# DesktopSpaces 0.1.1 preview

- Fix background hit testing on the original Windows desktop with cross-process MSAA.
- Keep Shell views warm and preload nearby spaces; defer layout/config persistence after switching.
- Replace static wallpaper bitmaps with a transparent icon surface and an OLE-enabled background input surface.
- Correct ExplorerBrowser's no-border flag, initialize OLE explicitly, and keep auto-arrange disabled.
- Handle same-folder icon moves and forward external drops to the Shell view's IDropTarget.
- Restore original icon visibility as well as the Desktop path after exit or a crash.
- Add settings drag reorder, F2 and Explorer-style delayed click rename, centred dialog buttons,
  a short indicator tooltip, and explicit transfer instructions.
- Replace “Remove from list” with “Delete”, fix stale row indexes and retain the deleted space's files.
- Verify startup registry writes, quote executable paths and refresh an enabled startup entry after app relocation.

Validation: 41 core checks, 3 Windows regression checks, native Shell/OLE probes,
normal-exit and crash-recovery integration checks. See VALIDATION.md for remaining
physical Windows 10/multi-monitor, continuous Alt-drag and Wallpaper Engine acceptance tests.
A computer restart/login test is explicitly recorded as pending.
