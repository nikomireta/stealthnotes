# Verification status

The user requested **no further tests**. The running benchmark was stopped. The current app and its installer have not been launched for runtime testing.

| Check | Status | Scope |
|---|---|---|
| Release win-x64 compilation | PASS | Initial build had 0 errors / 0 warnings; package metadata is in artifacts |
| Portable / framework-dependent publish | PASS | Compilation and packaging only |
| Inno Setup installer compilation | PASS | Includes runtime; size and hash are in StealthNotes-Setup.exe.json |
| Installer install / upgrade / uninstall | NOT TESTED | Installer was not launched |
| Current app UI / hotkeys / opacity / drag / resize | NOT TESTED | No additional tests requested |
| SQLite restore after restart | NOT TESTED | Same |
| 20 notes in the current app | NOT TESTED | Same |
| Current app idle RAM | NOT TESTED | Prototype numbers are not production measurements |
| WinForms 10 prototype OBS Display Capture | PASS | DXGI/WGC; the normal comparison note remained visible |
| WinForms 10 prototype existing OBS Window Capture source | FAIL | The pre-ghost image remained visible |
| Prototype fresh OBS BitBlt source while ghost was on | FAIL | The old image remained visible |
| Prototype fresh OBS WGC source while ghost was on | PASS | The note was excluded |
| Current app OBS / screen sharing | NOT TESTED | Prototype results do not verify the new app |
| Zero leaked frames on first show / toggle in every recorder | NOT TESTED | The prototype observation at +100 ms is not an atomicity guarantee |
| macOS / Linux | UNSUPPORTED | Windows-only app |
| Older Windows | NOT TESTED | The API minimum differs from runtime support |
| GitHub CI / automated Release | NOT RUN | Workflow files are ready; nothing was published remotely |

The earlier prototype passed 10/10 interaction checks; these are not tests of the current implementation. Selected summaries and images are in [evidence](evidence/README.md). Original experimental recordings remain in the parent workspace under `prototypes/artifacts/capture-winforms-final-qualified/` and `capture-native-qualified/`. Default OBS audio tracks were removed without re-encoding the video packets.

English UI and documentation were rebuilt and repackaged without running the app or installer. Historical raw evidence remains unchanged. No claim of being “undetectable” or passing all production gates is made.