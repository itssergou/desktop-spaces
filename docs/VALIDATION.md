# Version 0.1.3 validation

Date: October 21, 2026. Windows 11 x64, two monitors.

- Release build with warnings treated as errors: passed, zero warnings.
- Core checks: 45/45. Windows checks: 6/6. Coverage includes finding free icon
  cells, group placement, screen boundaries, a full grid, and migration from
  legacy MSIX LocalCache storage while retaining original files.
- The real application ran outside MSIX, in the tray, on the user's desktop.
  Status confirmed `Demo=false`, `PackagedProcess=false`, attachment to Explorer,
  and the shared physical storage root `%USERPROFILE%\.desktop-spaces`.
- The user confirmed transfers between the first and second spaces, including
  the reverse direction, after the physical storage fix.
- Icon overlap when transferring from the first space to the second was
  reproduced again on the real desktop. A successful placement HRESULT alone
  was insufficient: the final position could differ from the requested one.
- After the fix, the user repeated a held drag with Alt + 2 and dropped directly
  over another file, confirming that the icons no longer overlapped. The 23:19
  trace showed the test icon moving from (40,217) to (13,296), while the adjacent
  icon stayed at (13,198). Verification covered asynchronous Shell placement.
- After another 2 -> 1 -> 2 switch, the real instance retained (13,296) and
  (13,198), with no renewed overlap. No separate demo window was used for this
  acceptance check.
- The user confirmed the final post-reboot workflow: tray startup; preserved
  spaces, settings, files and icon positions; switching; transfers in both
  directions; and wallpaper behavior on their Windows 11 system. This is manual
  user acceptance, not an automatically collected reboot report.

Windows 10 and other DPI/monitor configurations remain unvalidated.
Wallpaper Engine acceptance applies to the user's configuration. Historical
results below describe earlier versions and do not replace current manual
acceptance testing.

On October 25, the first GitHub Actions build exposed a hidden-extension issue
in the OLE check. The Shell returned friendly names without `.txt`, preventing
placement code from finding items by their actual filenames. Layout and transfer
keys now use `SIGDN_PARENTRELATIVEPARSING`. The existing `Test-Surface.ps1` check
covers the runner's default Explorer settings and prints its detailed report
on failure.

After the fix, [GitHub Actions #40215652797](https://github.com/itssergou/desktop-spaces/actions/runs/40215652797)
successfully completed the Release build, 45 core checks, 6 Windows checks,
Shell and OLE checks, portable packaging and artifact upload.

## Version 0.1.2 validation history

Date: October 21, 2026. Windows 11 x64 (26200), .NET SDK 8.0.424.
Passing a component check does not establish acceptance on every Windows version.

| Check | Result |
|---|---|
| Release build, warnings treated as errors | Passed, zero warnings |
| Model, JSON, removal, ordering and recovery | 41/41 |
| Windows: removing the selected final row, selection by ID after reorder, startup paths with spaces, stable transfer sources | 4/4 |
| Native Shell: two entries and reading positions | `Test-Shell.ps1` passed |
| OLE: moving an icon within its folder | `Test-Surface.ps1`: the position changed and persisted after a file operation |
| OLE: transferring from another Shell view onto the transparent background | The file appeared in the target folder and its contents were readable; real Shell IDataObject/IDropTarget interfaces were used |
| Transparency and absence of a white border | Visually checked against a moving test background; static wallpaper was no longer substituted |
| Settings: creating and centering the dialog | Checked with the mouse |
| Renaming with F2 and a slow second click | Both methods opened name editing |
| Mouse reordering and persistence after app restart | Checked |
| Question-mark tooltip and transfer instructions | Checked |
| Real first desktop: empty-space hit testing | 162 empty points accepted; 6 occupied points rejected; the center of a real icon was not treated as empty |
| Switching and the system Desktop path | `Test-DesktopIntegration.ps1` passed |
| Reopening a prepared space | Internal timing observed at 19-73 ms; this is not a measurement of frame latency |
| Normal exit and forced termination | The original path and icon visibility were restored, and the journal was cleared |
| Taskbar application window | Absent in real desktop mode |
| Startup registration | The Run entry was enabled and removed, the quoted command was checked by reading it back, and the original entry was restored after the test |
| Persistence across app restart | Names and order confirmed; layouts covered by separate checks |
| Continuous held drag with Alt + number | Source preservation implemented; real-desktop manual acceptance still pending at that version |
| Wallpaper Engine | User acceptance with active wallpaper still pending at that version; transparency checked against a custom moving background |
| Windows 10, different DPI, monitor disconnection and Explorer restart | Physical-system testing pending |

Automated mouse control did not confirm a continuous native Shell drag: it
also failed to complete in an ordinary opaque view. That was not counted as a
successful manual test. The system OLE receiving path was checked separately
with real Shell data objects, file checks and position checks.

## Follow-up checks after 0.1.1 feedback

- Reproduced ambiguous Desktop-relative PIDLs: redirecting the root caused an
  incorrect source path. Binding through This PC/the volume removed that
  ambiguity.
- `scripts/Test-Transfer.ps1` confirmed round-trip transfer while actually
  changing the Known Folder path, stable source addresses and exact file
  contents.
- A separate Windows test checked that a source snapshot remains stable when
  the original IDataObject changes.
- Removed repeated timer-driven resize/Z-order operations, redundant background
  redraws and repeated COM name lookups. Periodic JSON flushing moved off the
  UI thread.
- The circular question-mark button was checked visually. Mouse responsiveness
  remained part of manual acceptance; the absence of every possible stall on
  the user's machine was not claimed.

## Post-reboot checklist

The user confirmed the main workflow after the final 0.1.3 fix. This checklist
is retained for validating future releases.

- [x] Reboot and sign in with startup enabled.
- [x] Confirm that DesktopSpaces appears in the tray without a taskbar button.
- [x] Check names, order, settings, files, folders and icon positions in every
      space. Startup opens the first desktop.
- [x] Check switching and transfers in both directions.
- [x] Check wallpaper in the first and additional spaces.
- [ ] Disable startup, reboot again and confirm that the app does not start.

The final item, a separate reboot with startup disabled, was not explicitly
confirmed. Earlier checks cover changes to the Run registry entry.

Local reports in `artifacts` contain machine paths and are excluded from Git
and release archives. OneDrive Known Folder Move remains blocked.
[GitHub Actions builds](https://github.com/itssergou/desktop-spaces/actions).
