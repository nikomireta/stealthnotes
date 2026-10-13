# Earlier prototype evidence

These files come from the **WinForms .NET 10 prototype**, not runtime tests of the current app. The user requested no further runtime testing.

- `interaction-prototype.json`: 10 passing prototype interaction checks.
- `capture-prototype.json`: analysis of all OBS 32.2.2 frames.
- `display-normal.png`: the normal control is visible.
- `display-ghost.png`: the ghost note is excluded; the normal note on the right remains visible.
- `window-ghost-failed.png`: Window Capture still shows the note image.
- `window-wgc-fresh.png`: a fresh WGC source created while ghost was on excludes the note.

JSON records include raw recording paths in the original experiment workspace. PNG files contain cropped experiment fixtures. Package hashes are listed in FILES.sha256.json. These records do not verify the current UI, SQLite persistence, or hotkey implementation. Raw records are preserved unchanged.