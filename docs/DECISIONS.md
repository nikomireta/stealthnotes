# Implementation decisions — 1.2.0

**C# WinForms, .NET 10 LTS, Microsoft.Data.Sqlite 10.0.12. Windows 11 x64.**

The 1.2 implementation builds on the existing 1.1 changes. All application source, scripts, installer, and release documentation live in the standalone `stealthnotes/` repository. Parent workspace analysis and prototypes are supporting material.

## Stack and evidence

WinForms provides native text controls and direct HWND interop with one language for state, storage, tray, and hotkeys. C++ had lower prototype RAM and package size, but more manual lifetime and interop work. Tauri's WebView overhead was not justified for this desktop editor.

Historical prototype observations, **not measurements of this release**:

| Candidate | Observed private working set |
|---|---|
| WinForms 8, 10 notes | About 8.7 MiB in completed runs |
| WPF 8, 10 notes | About 92 MiB |
| Tauri 2, 10 notes | About 241–247 MiB, including the app and 15 WebView2 processes |
| C++ Win32, 10 notes | About 1.2–1.4 MiB |
| WinForms 10 | About 7.68 MiB for one completed one-note normal run; 10–11 MiB in a 10-note smoke measurement |

The benchmark was stopped at the user's request after 37 of 54 runs. These are not final protocol-wide medians. Smoke measurements do not follow the full 30-second warmup and 60-second sampling protocol repeated three times. Rich text, images, list, and SQLite add work that has not been benchmarked.

The prototype WinForms 10 self-contained distribution was 47.55 MiB compressed and 117.25 MiB installed files. Current package sizes and hashes are in `artifacts/releases/*.json`.

WinForms and WPF are Windows-only. .NET 8 support ends November 10, 2026; .NET 10 LTS support ends November 14, 2028. Patches still require maintenance.
[.NET policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [WinForms](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/).

Tauri requires Rust, C++ Build Tools, and WebView2 on Windows. Its JavaScript API is named `setContentProtected`, not `setContentProtection`; mouse handling uses `setIgnoreCursorEvents`.
[Tauri API](https://v2.tauri.app/reference/javascript/api/namespacewindow/).

## Documents, windows, and list

AppState is the source of truth. Actions change immutable note records. WindowManager creates at most one window per open note and renders only changed notes or global changes. Notes list is created lazily by the explicit list command. A single owner-painted scrollable control renders variable-height, theme-colored cards, a first-image preview, and at most five lines of text. It creates no editor or image control per note and avoids the native ListBox item-height limit at high DPI. Selection and the first visible note ID plus pixel offset survive updates and reordering. Search uses the complete plain-text document; painted and accessible previews are bounded. Unchanged cards are not rebuilt during note moves or resizes.

A RichTextBox stores plain text and RTF. Plain text drives search and previews; RTF stores formatting. The toolbar and local shortcuts handle bold, italic, underline, strike, and bullets. Single-style native masks preserve other character styles in mixed selections. Formatting-only changes dispatch UpdateDocument. The current editor does not reload its own RTF acknowledgement, preserving caret, selection, and native undo history.

Theme text color changes use the Rich Edit Text Object Model to suspend/resume undo, verified against Windows SDK `tom.h`. They do not replace document fonts or reset history. Native undo/redo applies to text and formatting during the lifetime of the open editor, not image operations.
[RichTextBox](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.richtextbox?view=windowsdesktop-10.0), [character formatting](https://learn.microsoft.com/en-us/windows/win32/controls/em-setcharformat), [TOM](https://learn.microsoft.com/en-us/windows/win32/api/tom/nn-tom-itextdocument).

External clipboard text pastes as Unicode plain text. Copying within this process stores a generated RTF string in memory and puts only a random lookup token plus plain text on the clipboard. The token must match the current trusted copy; arbitrary external RTF/OLE is not accepted. Images and file drops route to the gallery instead of embedding objects.

HasContent means non-whitespace text or at least one image reference. Formatting alone is a draft. Closing a populated note persists `is_open=false`; closing a draft discards it. Quit preserves open flags. Hide all is session-only. Startup, tray double-click, and a matching second-instance activation share one path: restore existing open notes (including hidden or minimized ones), otherwise open the most recently updated note, otherwise create one empty note. They do not create or show Notes list. Other closed notes stay closed. Explicit Show all still opens all notes, while the visibility hotkey never reopens closed notes. Process identity checks, ghost settings, opacity, and stored placement are unchanged.

Ghost list rows contain “Ghost note”, an empty preview, and no image reference. Search matches only that placeholder for a ghost row, never its content. Accessibility strings use the same redacted row values. Global ghost redacts all rows. The list remains clickable to recover normal input.

The 1.1 visual foundation remains: 8-DIP window regions, 16-DIP paper padding, 36-DIP header, pastel themes, buffering, and original vector controls. The formatting toolbar is 36 DIP. No separate shadow or image-viewer windows, opacity animations, blur, or WebView are added.
[Window corner limitations](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-rounded-corners).

## Storage and images

Schema v2 adds `content_rtf` and `is_open` to notes, and `note_images` with ID, note ID, order, MIME, dimensions, image data, and thumbnail. Foreign keys cascade image deletion. Notes, documents, image additions/removals, and preferences commit through one serialized writer with transactions. Autosave remains 500 ms with a shutdown flush and unsaved-exit confirmation.

Before v1 migration, the SQLite backup API creates `backups/before-1.2.0.sqlite3`; the temporary database is closed before receiving its final name. Backup failure stops migration. Migration rolls back on failure. Old text is preserved exactly, RTF remains null until edited, and old notes default to closed because previous open state was not recorded. Legacy whitespace-only drafts and `all_hidden` are removed inside the migration transaction. Fresh databases start at v2; newer or unknown schemas are rejected instead of repaired destructively.
[SQLite backup](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup).

Image import supports PNG/JPEG/BMP/GIF, at most 10 MiB and 20 megapixels. File signatures/dimensions are inspected before decoding; decoded dimensions are checked too. GIF frame zero and BMP are normalized to PNG, with the resulting byte limit checked. Clipboard images are encoded as PNG.

ImageStore owns staged immutable bytes outside AppState. Persistence leases staged IDs before queuing a snapshot; failed writes retain live imports, and committed imports can be released. Deleting a pending image cannot release bytes still needed by a queued writer. Original image BLOBs and thumbnails are queried using separate read-only connections. All decoding is serialized in the background.

A shared UI-thread LRU owns at most 32 MiB of display previews (width x height x four bytes). Preview widths are bucketed in 128-pixel increments; each returned bitmap is capped at two megapixels without upscaling. Original sources retain the existing 20-megapixel import limit and are decoded transiently on the serialized background decoder before scaling. Only visible cards/images request previews; consumer sessions cancel obsolete queued work on scrolling, hiding, resizing, removal, or disposal. Results are checked again on UI delivery, and cancelled results and evicted bitmaps are disposed. Paint handlers borrow cached bitmaps without retaining them. Original BLOBs, stored thumbnails, attachment order, persistence leases, and schema v2 are unchanged.

The note body has one AutoScroll viewport painting a virtual vertical image strip above the existing RichTextBox. Only the viewport is buffered; there is no giant child control or off-screen buffer covering every image. Images preserve their aspect ratio without cropping; there are no navigation buttons, counters, or expanded-view mode. Header and formatting toolbar stay outside the viewport. Native EN_REQUESTRESIZE measurements size the editor to its formatted document without reloading RTF. Wheel and Page Up/Down scroll the outer viewport without changing the caret or selection; Shift + Page Up/Down extends text selection, and selection/caret changes reveal the active insertion point. Layout and scrolling do not assign Text/RTF or clear undo. An image's context menu requests removal using the existing confirmation. File selection, paste, and drop still use the same import limits. These are source-level mechanisms, not verified runtime smoothness, editing, memory, or capture guarantees. Images remain in the note HWND, so its existing ghost affinity and click-through continue to apply.

Persistence failure displays “Not saved” and a tray warning; a successful retry clears the indicator. User data is plaintext local SQLite with no application networking.

## Upgrade and process identity

Installer AppId and LocalAppData paths stay unchanged. PrepareToInstall invokes an embedded, read-only Windows PowerShell/CIM script to inspect StealthNotes processes and owner SIDs, including portable paths. Running copies prompt Quit → Retry; script, ownership, path, execution, or result-file failures block installation. Silent setup returns an error. The installer never force-kills a process. The existing mutex is retained as another guard.

The app atomically writes local instance metadata containing PID, process start time, executable path, and version. Second launch verifies owner, PID/start time, path, and version before signalling the existing instance. A different or unverifiable copy produces an explanation with the running location instead of silently activating it. Metadata is removed on normal shutdown; stale records are checked against the live process.

The installer requires inbox Windows PowerShell and CIM access for preflight. If local policy blocks inspection, it fails closed; this is not an auto-update mechanism. No account, cloud, telemetry, ink, or network updater is included.

## Capture and release limits

WDA_EXCLUDEFROMCAPTURE requires Windows 10 version 2004 or later, an owned top-level HWND, and DWM composition. Layered click-through is applied with affinity/style readback before first Show. Failed configuration attempts restore normal input and report an error.
[Display affinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity), [layered windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows).

Earlier WinForms/C++ prototype Display Capture DXGI/WGC passed. Existing Window Capture sources retained the pre-ghost image; a fresh BitBlt source also failed. A fresh WGC source after ghost activation passed. That historical prototype capture gate remains FAIL. Separately, the user reports that ghost notes were not visible in manual OBS, Recordly, screenshot, Google Meet, Zoom, and Microsoft Teams checks. The exact build and capture/share modes were not recorded with the report; see [verification](VERIFICATION.md#user-reported-manual-capture-checks). The historical evidence remains unchanged, and the report does not imply every recorder/source mode has been validated.

macOS/Linux are unsupported. Tao does not provide Linux content protection; NSWindowSharingNone does not protect against modern ScreenCaptureKit. No cross-platform ghost claim is made.
[Tao](https://docs.rs/tao/latest/tao/window/struct.Window.html#method.set_content_protection), [ScreenCaptureKit limitation](https://www.electronjs.org/docs/latest/api/browser-window#winsetcontentprotectionenable-macos-windows).

Release source is MIT with dependency notices. The default version comes from the project file. Inno Setup packages the self-contained .NET runtime; framework-dependent ZIPs require a separately installed desktop runtime. Publishing source does not rebuild or replace release artifacts. Local compilation and packaging evidence are recorded separately from remote CI results.

The user explicitly prohibited additional runtime tests by the agent. Agent verification is limited to compilation, static checks, and packaging where requested. The separate user-reported capture checks are documented above. Other UI behavior, data migration, installer preflight/upgrade, and current memory use remain NOT TESTED. No production certification or universal exclusion is claimed.

Architecture is a **reconstruction from static analysis** used as inspiration. No Microsoft Sticky Notes code, assets, strings, or schema were copied. Raw historical evidence is preserved unchanged.
