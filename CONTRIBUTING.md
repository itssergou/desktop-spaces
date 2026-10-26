# Contributing to DesktopSpaces

Create a separate branch for changes. In a pull request, describe the problem,
the resulting behavior and the checks you performed. Changes to Shell
integration, system folder paths or recovery require tests using temporary
folders and an updated compatibility matrix.

Before submitting, run a Release build and the checks in
`tests/DesktopSpaces.Tests`. Compiler warnings are treated as errors.
Regular CI does not change the system Desktop folder path. Run `--path-smoke`
only in a dedicated Windows test profile, with a recovery journal.

Keep these boundaries:

- Do not delete files when removing a space from the list.
- Do not copy space contents when switching spaces.
- Do not change another program's folder path if it is not owned by the current
  session.
- Do not publish user settings, logs or folder contents.
- Discuss cloud dependencies or data collection before adding them.
- Do not destroy the source of an OLE drag before the operation completes.

Keep the C# code straightforward. Document Win32 and COM calls where their
contracts are not obvious. Check licenses and required notices before adding a
dependency. Contributions are distributed under this project's MIT license.
