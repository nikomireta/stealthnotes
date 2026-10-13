# Contributing

Use Windows and the .NET SDK pinned in `global.json`.

```powershell
./scripts/build.ps1
./scripts/publish.ps1
./scripts/setup-inno.ps1
./scripts/installer.ps1
```

The installer is written to `artifacts/releases/StealthNotes-Setup.exe`. Inno Setup is downloaded from its official release and its publisher signature is verified before installation. The self-contained package includes the .NET runtime. A framework-dependent package is available with `publish.ps1 -FrameworkDependent`.

Push to `main` builds and publishes a rolling GitHub Release tagged `latest`. Tags such as `v1.1.0` publish separate version releases. Pull requests only build artifacts. Work inside this standalone application repository; parent workspace files are research support material. The project file is the default version source for publish and installer scripts; update it when releasing a new version.

Keep domain changes in AppState actions. Windows render snapshots and must not write directly to SQLite. `Note.HasContent` means text or image metadata; formatting alone is a draft. `is_open` persists window choices; global hide/show stays session-only. Use `UpdateDocument`, `OpenNote`, `CloseNote`, `AddImage`, and `RemoveImage` rather than changing controls or the database directly.

Keep writes serialized and migrations transactional. Schema v2 adds RTF, open state, and image BLOBs. The SQLite API backup `backups/before-1.2.0.sqlite3` must complete before v1 migration. Do not copy a live WAL database as a plain file backup. New image bytes belong to ImageStore staging; queued persistence snapshots lease them until commit. Never put full image bytes in AppState or read BLOBs on the UI thread. Thumbnail and preview decodes are serialized and bounded.

Do not replace an editor's RTF during its own edits or routine state rendering. Preserve selection, caret, and undo history. External clipboard text is plain text; trusted in-process clipboard tokens preserve local RTF without accepting arbitrary embedded clipboard objects. Image previews stay in their note HWND.

Notes list rows must contain only redacted display strings for ghost notes, including accessibility text. Do not search ghost content, request ghost thumbnails, or auto-generate their titles from content.

Installer preflight is read-only and must fail closed. It uses inbox Windows PowerShell/CIM to identify running copies owned by the current Windows user. Do not terminate processes or change this check to match only the installation directory. If process inspection is blocked, installation must wait for the problem to be resolved.

Keep the app local: no telemetry, accounts, cloud, or network calls. Do not copy Microsoft Sticky Notes code or resources. Original contributions are licensed under MIT.

For ghost changes, record the OS version, API errors/readback, and capture method. Clearly distinguish PASS, FAIL, and NOT TESTED. Affinity readback does not prove exclusion from every recorder. Preserve failed results. Runtime tests are currently disabled at the project owner's request.

Describe the behavior change and validation actually performed in each pull request. Never commit note databases, private text or recordings, credentials, bin/obj, SDKs, or generated release files.
