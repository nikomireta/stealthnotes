# Verification status — 1.2.0

The user requested no additional runtime tests. The agent did not launch the application, installer, or preflight script, and did not access or migrate the user's live notes database.

## User-reported manual capture checks

The user reports that notes in ghost mode were not visible in the following manual checks. These are user-reported runtime results, separate from the agent's compilation/static checks and the historical prototype evidence below.

| Tool / operation | Status | Reported result |
|---|---|---|
| OBS | PASS (user-reported) | Ghost notes not visible |
| Recordly | PASS (user-reported) | Ghost notes not visible |
| Screenshot | PASS (user-reported) | Ghost notes not visible |
| Google Meet | PASS (user-reported) | Ghost notes not visible |
| Zoom | PASS (user-reported) | Ghost notes not visible |
| Microsoft Teams | PASS (user-reported) | Ghost notes not visible |

The exact StealthNotes build, tool versions, screenshot utility, capture/share source modes, and recordings were not supplied with the report. Results apply to the user's tested configuration; they do not establish coverage of every source mode or a zero-leaked-frame guarantee. No additional runtime tests were performed by the agent.

## Current notes/cards update

Scope: lazy Notes list activation, variable-height owner-painted cards, a shared image/text viewport, and asynchronous display-preview caching. The existing package/installer evidence below predates these changes; no new installer or package is produced by this update.

Release win-x64 build: **PASS**, 0 warnings and 0 errors, using `scripts/build.ps1 -Dotnet ../.tools/dotnet-10.0/dotnet.exe` (locked restore followed by Release compilation). Source whitespace checks also pass. This is compilation evidence only.

Static review covers empty/closed/open/hidden startup paths, second-instance and tray routing, ghost preview/search/accessibility redaction, selection/scroll anchoring, attachment order and failures, resize requests, cancellation/stale result checks, bitmap ownership, and unchanged RTF acknowledgement/undo and schema v2. No runtime result is implied.

Runtime checks remain **NOT TESTED**: initial and repeat launch, hidden/minimized notes, context menus and keyboard/card search, multiple/missing/deleted images, scroll/caret/selection/undo, resize/DPI, memory responsiveness, and compatibility with an existing database. Ghost/capture has the user-reported results above; coverage of the latest notes/cards build is not established. No application, installer, capture, or benchmark was launched by the agent.

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
| Ghost capture exclusion | PASS (user-reported) | OBS, Recordly, screenshot, Google Meet, Zoom, and Teams; exact build and source modes unspecified |
| Click-through / opacity / hotkeys / ghost handle recreation | NOT TESTED | Not covered by the manual capture report |
| 20 notes / RAM / responsiveness | NOT TESTED | No production memory measurements or smoothness claim |
| DPI 100–200% / resize / keyboard focus | NOT TESTED | Source implementation only |
| Upgrade install / uninstall | NOT TESTED | Stable AppId and data path checked statically; installer not launched |
| GitHub CI for the current source update | NOT VERIFIED | Local Release build passed; remote workflow result not checked |
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
| Exhaustive source modes / zero leaked frames | NOT TESTED | Historical results and the user report do not establish exhaustive or frame-by-frame coverage |

Selected summaries/images remain in [evidence](evidence/README.md). Original recordings remain in the parent experiment workspace; default OBS audio was removed without re-encoding video packets.

The user manually installed 1.1.0 previously. Read-only diagnosis confirmed its installed files, but found an older portable process still running; this motivated the 1.2 preflight and instance checks. That observation is not a runtime test of the new upgrade handling.

Build and packaging success do not establish feature, migration, performance, upgrade, or capture correctness.
