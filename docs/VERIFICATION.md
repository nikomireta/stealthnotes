# Verification status — 1.2.0

The user requested no additional runtime tests. The agent did not launch the application, installer, or preflight script, and did not access or migrate the user's live notes database.

| Check | Status | Evidence / scope |
|---|---|---|
| Release win-x64 compilation | PASS | Final local build: 0 errors / 0 warnings |
| Self-contained / framework-dependent packaging | PASS | Package metadata and hashes in artifacts/releases |
| Inno Setup compilation | PASS | Installer build log and StealthNotes-Setup.exe.json; not an installation test |
| PowerShell source parsing / source whitespace checks | PASS | Static inspection only; preflight script was not executed |
| RTF and formatting-only autosave | NOT TESTED | Mixed selection, caret styles, undo/redo, and restart round-trip |
| Clipboard / image import | NOT TESTED | Files, paste, drop, image-only notes, invalid files, limits, and first-frame GIF |
| Image persistence and gallery | NOT TESTED | Add/remove/re-add order, failures, preview races, and shutdown with pending writes |
| Notes list and search | NOT TESTED | Open/close, no duplicate windows, last-open restore, and draft disposal |
| Ghost preview redaction | NOT TESTED | Text, titles, thumbnails, search, and accessible list strings |
| v1 → v2 migration | NOT TESTED | Backup, exact old text, closed defaults, cleanup, rollback, and backup failure |
| Save failure / recovery | NOT TESTED | Not saved indicator, staged image retention, retry, and exit warning |
| Upgrade preflight | NOT TESTED | Installed and portable copies, same/different user, permissions, Retry/Cancel, and silent failure |
| Second-instance detection | NOT TESTED | Same copy activation, different version/path, stale metadata, and unverified process |
| Current ghost / click-through / opacity / hotkeys | NOT TESTED | Includes rich editor, gallery preview, and handle recreation |
| 20 notes / RAM / responsiveness | NOT TESTED | No production memory measurements or smoothness claim |
| DPI 100–200% / resize / keyboard focus | NOT TESTED | Source implementation only |
| Upgrade install / uninstall | NOT TESTED | Stable AppId and data path checked statically; installer not launched |
| GitHub CI / Release for 1.2.0 | NOT RUN | Changes remain local |
| macOS / Linux | UNSUPPORTED | Windows-only |
| Older Windows | NOT TESTED | Installer targets Windows 11 x64 |

## Historical prototype evidence

| Check | Status | Scope |
|---|---|---|
| WinForms 10 interaction prototype | PASS | Earlier 10/10 interaction checks; not current implementation |
| OBS Display Capture DXGI/WGC | PASS | Prototype only; normal comparison note remained visible |
| Existing OBS Window Capture source | FAIL | Prototype retained the pre-ghost image |
| Fresh OBS BitBlt source while ghost was on | FAIL | Prototype old image remained visible |
| Fresh OBS WGC source while ghost was on | PASS | Prototype note was excluded |
| Current OBS / screen sharing / zero leaked frames | NOT TESTED | Prototype results do not verify this release |

Selected summaries/images remain in [evidence](evidence/README.md). Original recordings remain in the parent experiment workspace; default OBS audio was removed without re-encoding video packets.

The user manually installed 1.1.0 previously. Read-only diagnosis confirmed its installed files, but found an older portable process still running; this motivated the 1.2 preflight and instance checks. That observation is not a runtime test of the new upgrade handling.

Build and packaging success do not establish feature, migration, performance, upgrade, or capture correctness.
