# DesktopSpaces 0.1.3 preview

- Store settings and space files in `%USERPROFILE%\.desktop-spaces`. Processes
  launched by an MSIX host can inherit AppData virtualization: their files were
  visible inside the app but absent at the same path in ordinary Explorer.
  Import the newest legacy configuration and copy its real files, including
  package-private LocalCache storage. Preserve original copies for rollback.
- Keep Desktop Known Folder on the first space. Save to other spaces by choosing
  their folders; switching no longer mutates global folder paths or file data.
- Restore keyboard focus to Explorer's first desktop when returning there.
- Mask inactive icon surfaces during a held drag without destroying the source
  window. This prevents overlapping icons through the live-wallpaper surface.
- Do not hide a held drag's source when the name indicator expires; pause view
  preloading while the mouse button is held.
- Make reorder insertion follow the shown insertion marker; refresh the tray
  name immediately when the active space is renamed.
- Place incoming icons in a nearby free cell, verify the actual Shell coordinates
  and recheck through asynchronous drop completion. Repair collisions after Shell
  notifications and when loading a space. Capturing the result no longer invokes
  view maintenance that could restore an earlier layout over the new position.
- Identify Shell items by their filesystem parsing names, including extensions.
  Moving icons and restoring their positions now also work when Explorer hides
  known file extensions.
- Add opt-in local transfer diagnostics (`trace-transfers.enabled` in the app's
  local data folder). Logs remain local and are excluded from the release archive.

The user confirmed real desktop transfers in both directions, collision-free
placement and startup with saved state after reboot on Windows 11. Windows 10
and other DPI/monitor configurations remain pending. Detailed validation is
tracked in `docs/VALIDATION.md`.

## 0.1.2

Fixes for follow-up testing:

- Bind additional folders through the explicit This PC / volume namespace. Shell PIDLs must not
  change their meaning when the Desktop Known Folder changes during a transfer.
- Capture source paths before changing Desktop during a drag from the original desktop, and
  retain that stable data object through Drop. Stabilize file clipboard data before switching away.
- Forward drops over icons as well as empty background through the same handler.
- Avoid repeated desktop-size and Z-order changes on the health timer. Keep valid native list
  handles and item names cached, enable native list double buffering, and avoid redundant theme redraws.
- Move periodic settings flushes off the UI thread. Revision checks prevent an old background
  snapshot from overwriting newer settings. Skip layout collection while dragging.
- Replace the square question-mark button with a small antialiased circular control.

Validation includes a new opt-in Test-Transfer.ps1: transfer a fixture out and back while changing
Desktop Known Folder, verify source paths and file content, and restore the original desktop.
This check is kept out of CI because it temporarily redirects the current user's Desktop.
Physical drag responsiveness, Wallpaper Engine and reboot/login still require user acceptance.
