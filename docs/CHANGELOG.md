# Changelog

## Unreleased

- Theme-colored note preview cards with a first image, up to five text lines, search, keyboard access, and context-menu actions. Selection and scroll anchors survive updates; ghost previews remain redacted.
- Startup, second launch, and tray double-click activate notes directly. Notes list is created only by its explicit menu/keyboard command.
- Images stack above rich text in one scrolling document, with per-image context-menu removal and no carousel controls.
- Background display previews capped at two megapixels, a shared 32 MiB cache, and cancellation of obsolete viewport requests.
- Existing originals, RTF, autosave, import limits, schema v2, process checks, and ghost/capture behavior retained. No new runtime dependencies.

Agent validation: Release compilation and static review only; no application, installer, capture, benchmark, or live-database tests were run by the agent. Separately, the user reports ghost notes were not visible in OBS, Recordly, screenshots, Google Meet, Zoom, and Teams. Exact build/source-mode coverage is unspecified; see [verification](VERIFICATION.md#user-reported-manual-capture-checks).

## 1.2.0

- Native rich text with bold, italic, underline, strikethrough, bullets, and text/formatting undo/redo.
- Image gallery with file import, clipboard paste, drag-and-drop, navigation, removal, and in-note preview. PNG/JPEG/BMP/GIF; 10 MiB and 20 megapixels per image; GIFs are static.
- Notes list with search, latest-edited sorting, open/close/ghost/delete actions, and a visible version number.
- Startup opens the list and notes last left open. Closed populated notes remain in the list; empty drafts are discarded.
- Ghost list previews and accessibility strings are redacted, and ghost content is excluded from search.
- Schema v2 stores RTF, open state, and image BLOBs transactionally. A SQLite API backup precedes migration; legacy notes default to closed.
- Background image decoding, lazy BLOB reads, a bounded thumbnail cache, and leased pending imports.
- Installer preflight detects running installed/portable copies for the Windows user and asks for Quit → Retry without force-closing them.
- Different or unverifiable running copies produce an explicit startup message rather than silently receiving activation.
- Windows-only, local-only, no sync/cloud/accounts/telemetry/ink/network auto-update.

Validation: compilation, static inspection, and packaging only. Runtime, migration, memory, capture, and installation behavior remain NOT TESTED.

## 1.1.0

- Empty/whitespace-only notes became temporary drafts, with backup before legacy cleanup.
- Soft rounded windows, pastel themes, vector buttons, and reduced rendering work.
- Not saved indicator and shared project version for packaging.
- The 1.2 Notes list and persisted open state supersede 1.1's restore-all-populated-notes behavior.
