# StealthNotes

Local sticky notes for Windows 11 x64. Soft colors, rich text, pictures, opacity, and ghost mode.

## Install

Download **StealthNotes-Setup.exe** from [Releases](https://github.com/nikomireta/stealthnotes/releases), open it, and click **Install**. Everything you need is included.

When upgrading, **Quit every running copy from the tray**, including portable copies, then click **Retry** in Setup.

## Use

- Open **Notes list** from a note's menu, the tray menu, or **Ctrl + H**. Search the preview cards; click a card or press Enter to open it. Right-click for Close, Ghost, and Delete.
- **+** creates a note. Drag the header to move it; drag an edge to resize.
- The bottom toolbar formats text and adds pictures. Paste or drop images to stack them above the text. Images and text scroll together; right-click an image to remove it.
- The **ghost icon** enables click-through. **···** opens colors, opacity, and other actions.
- **×** closes a note; it stays in the list if it contains text or pictures. Empty drafts are discarded.

Notes save automatically. Startup restores the notes you left open. If none are open, it opens your most recently updated note, or creates an empty note. Launching the shortcut again or double-clicking the tray icon restores open notes, including hidden ones. The list appears only when you ask for it. After upgrading from 1.0/1.1, your other old notes remain available in the list.

| Action | Shortcut |
|---|---|
| New note | Ctrl + N |
| Notes list / search list | Ctrl + H / Ctrl + F |
| Bold / italic / underline / strike | Ctrl + B / I / U / T |
| Bullets | Ctrl + Shift + L |
| Undo / redo | Ctrl + Z / Y |
| Global new note | Ctrl + Alt + N |
| Ghost note / all notes | Ctrl + Alt + G / Ctrl + Alt + Shift + G |
| Hide / show open notes | Ctrl + Alt + H |
| Adjust opacity | Ctrl + Alt + Up / Down |

Global shortcuts are customizable in the tray menu. Ghost notes need hotkeys or the list to regain input; their list previews are hidden.

Pictures: PNG, JPEG, BMP, or GIF; up to 10 MiB and 20 megapixels each. GIFs are static. External text pastes as plain text.

Use the wheel or Page Up/Down to scroll pictures and text. Shift + Page Up/Down extends the text selection.

No accounts, cloud, telemetry, or auto-update. The user reports successful ghost-mode checks in OBS, Recordly, screenshots, Google Meet, Zoom, and Teams; see [verification details](docs/VERIFICATION.md#user-reported-manual-capture-checks). **Ghost is not guaranteed for every recorder or screen share.** Other runtime behavior and the installer remain unverified. Windows may warn about the unsigned installer.

MIT licensed. [Contributing](CONTRIBUTING.md) · [Verification](docs/VERIFICATION.md)
