# Dependencies and licenses

StealthNotes code is licensed under MIT; see LICENSE. Dependencies retain their own licenses.

- [.NET / Windows Forms](https://github.com/dotnet/winforms): MIT. Self-contained distributions include the .NET runtime and its third-party notices.
- [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.12): MIT, including Microsoft.Data.Sqlite.Core.
- [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw): Apache-2.0. Core, provider, and native bundle packages retain their respective notices.
- [SQLite](https://www.sqlite.org/copyright.html): public domain. The e_sqlite3 binary is distributed through SQLitePCLRaw.lib.e_sqlite3.
- [Inno Setup](https://jrsoftware.org/): copyright 1997-2026 Jordan Russell; portions copyright 2000-2026 Martijn Laan. See `licenses/InnoSetup-LICENSE.txt` for the installer runtime license.

Transitive versions and content hashes are recorded in `src/StealthNotes/packages.lock.json`. The publish script includes the project's license and the license/notices files in `licenses/`.

Segoe UI is provided by Windows and is not bundled as an asset. The application's MIT license does not replace dependency or installer licenses.