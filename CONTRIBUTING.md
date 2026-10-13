# Contributing

Use Windows and the .NET SDK pinned in `global.json`.

```powershell
./scripts/build.ps1
./scripts/publish.ps1
./scripts/setup-inno.ps1
./scripts/installer.ps1
```

The installer is written to `artifacts/releases/StealthNotes-Setup.exe`. Inno Setup is downloaded from its official release and its publisher signature is verified before installation. The self-contained package includes the .NET runtime. A framework-dependent package is available with `publish.ps1 -FrameworkDependent`.

Push to `main` builds and publishes a rolling GitHub Release tagged `latest`. Tags such as `v1.0.1` publish separate version releases. Pull requests only build artifacts. The root workspace workflow mirrors the standalone app workflow; keep both copies in sync. Main builds read the version from the project file; update the source and script defaults when releasing a new version.

Keep domain changes in AppState actions. Windows render snapshots and must not write directly to SQLite. Keep database writes serialized and use transactional migrations for schema changes.

Keep the app local: no telemetry, accounts, cloud, or network calls. Do not copy Microsoft Sticky Notes code or resources. Original contributions are licensed under MIT.

For ghost changes, record the OS version, API errors/readback, and capture method. Clearly distinguish PASS, FAIL, and NOT TESTED. Affinity readback does not prove exclusion from every recorder. Preserve failed results. Runtime tests are currently disabled at the project owner's request.

Describe the behavior change and validation actually performed in each pull request. Never commit note databases, private text or recordings, credentials, bin/obj, SDKs, or generated release files.