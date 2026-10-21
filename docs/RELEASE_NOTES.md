# DesktopSpaces 0.1.2 preview

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
