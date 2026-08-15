# Changelog

All notable changes to **geetRPCS**, newest first. The latest release is also summarized in [`RELEASE/releasenotes.md`](RELEASE/releasenotes.md). Entries before v1.3.4 are condensed from repository history; the v1.0.0 – v1.2.9 period is reconstructed from git tags and the READMEs of the time.

---

## v1.4.1 — The Polished Update

This update is all about **polish and correctness**: custom Discord-style dark-theme dialogs, real Application ID validation, a 100% localized UI in all 24 languages, game engine support, app database cleanup, and an automated test suite with a CI pipeline.

### Added ✨

- `[FEAT]` **Game Engine Presets** – New Discord presets for **Unity**, **Unreal Engine** (UnrealEditor & UE4Editor), **Godot Engine** (Godot & Godot_v) and **Roblox Studio**, each with dedicated assets.
- `[FEAT]` **Discord-style Dialogs** – Custom dark-theme `InfoDialog` (single OK button) and `ConfirmDialog` (Yes/No) replace the native `MessageBox` across statistics, config reload, shortcut management, update checks and error paths.
- `[UX]` **Redesigned Change App ID Dialog** – Discord-style palette with a warning callout (amber accent bar), inline validation error, a **Reset Default** button, and a **Save** button that only enables for a valid, changed ID.
- `[CORE]` **Application ID Validation** – New `IsValidApplicationId()` guard enforces Discord snowflake rules (17–20 decimal digits) in the Change App ID dialog and at config load.
- `[LOC]` **Full Tray Menu Localization** – The Auto-Update toggle, Manage Shortcuts submenu, shortcut created/removed notifications, Yes/No/OK buttons and shortcut error messages are now translated across all 24 language files.
- `[LOC]` **100% Localization Coverage** – Every user-visible string (tray menu, dialogs, Change App ID dialog, preview window, update dialogs, startup errors) now flows through `LanguageManager`, and all 24 language files ship complete translations for every key — nothing falls back to English anymore.
- `[LOC]` **Fallback Warnings** – `LanguageManager` logs a warning naming each key that falls back to English (`Language "xx": N untranslated key(s) fell back to English: ...`), surfacing untranslated strings in `geetRPCS.log`.
- `[TEST]` **Automated Test Suite** – New dependency-free `Tests` project validating ID rules, telemetry defaults, `apps.json` integrity (unique processes, valid client IDs, non-empty image keys, valid button URLs/labels, max 2 buttons) **and language parity** (every `en.json` key exists in every language file and `template.json`).
- `[CI]` **GitHub Actions Pipeline** – New `.github/workflows/ci.yml` builds the solution and runs the full test suite on every push to `main` and every pull request; a missing translation key fails the build.
- `[DOC]` **Localization Guide & Contributing** – [`LOCALIZATION.md`](LOCALIZATION.md) documents the fallback architecture and how to add keys/languages; [`CONTRIBUTING.md`](CONTRIBUTING.md) now points contributors at it and at the `Tests` project.

### Changed 🔄

- `[DATA]` **apps.json Cleanup** – Added missing client IDs to **Krita** and **Orange Data Mining**; removed duplicate **Maya** and **Figma** entries; introduced a dedicated **Game Engines** section.
- `[UX]` **Dialog Consistency** – Statistics, config reload, export, update-check and shortcut confirmations now share the same dark visual language as the rest of the app.
- `[UX]` **Timer Reset Removed** – The "Reset All Timers" / "Clear All Timers" tray items and their localization keys were removed from the menu.
- `[BUILD]` **Test Isolation** – The `Tests` project is excluded from the main build and enabled via `InternalsVisibleTo`.
- `[BUILD]` **Template Sync** – `Languages/template.json` is now a complete English mirror of `en.json` (202 keys) as the canonical translator reference.

### Fixed 🐛

- `[FIX]` **Invalid config rejected** – A `config.json` containing a malformed Discord Application ID is now rejected at load instead of being silently accepted.

---

## v1.4.0 — The Architecture Refactor Update

This update focused on **internal architecture and foundation improvements**: the monolithic `Program.cs` was decomposed, single-source versioning and unified user data paths were introduced, localization was rebuilt with a central fallback, and the idle presence experience was polished.

### Added ✨

- `[CORE]` **Split Program.cs** – The 1,549-line monolith was broken down into single-purpose components (`AppCoordinator`, `PresenceBuilder`, `TrayMenuController`, `StatsCoordinator`, `UpdateOrchestrator`), leaving `Program.cs` as a thin host.
- `[CORE]` **AppVersion** – New single source of truth for version info, read directly from the assembly and consumed by all version consumers.
- `[CORE]` **AppPaths** – Centralized path definitions splitting ship-able resources (`apps.json`, `witty.json`, `rpicon.ico`, `Languages\`) from user data (`%LOCALAPPDATA%\geetRPCS`).
- `[LOC]` **Localization Overhaul** – Languages are now scanned from `Languages\*.json` instead of being hardcoded, with centralized fallback to `en.json`.
- `[FEAT]` **Idle Presence Fallback** – Idle presence now falls back to config `Details`/`State` when empty and appends the current mouse energy state.
- `[FEAT]` **Per-App Timestamps** – Added a `showTimestamps` toggle per app (overrides the global Discord setting), backward compatible with existing configs.

### Changed 🔄

- `[CORE]` **Version Sync** – All projects and tools bumped to `1.4.0`, with a single `AppVersion` source of truth and updated `LargeImageText`.
- `[CORE]` **Hotkeys Preserved** – Ctrl+Alt+P (pause), V (preview), R (reload), H (private), S (stats today) via the thin host.
- `[UX]` **Idle Energy Refresh** – Idle presence now re-renders on mouse energy changes even when no app is active (rate-limited to 5s).

### Fixed 🐛

- `[FIX]` **Empty Idle Presence** – Empty `Details`/`State` values no longer produce a blank idle display in Discord.

---

## v1.3.9 — Network & Reliability Update

- `[CORE]` **Comprehensive service architecture** – Centralized app configuration with JSON source generation and dynamic narrative support.
- `[FEAT]` **Enhanced app database** – Improved handling of app configs and witty-text (narrative) placeholders.
- `[REL]` **Reliability improvements** – Network/update-path hardening for a smoother experience.

---

## v1.3.8 — Updater Helper & New Services

- `[FEAT]` **Updater Helper** – Dedicated maintenance tool (`Updater.exe`) for install/update/uninstall flows.
- `[CORE]` **New services** – Centralized **logging**, **statistics**, **witty narrative**, **taskbar watching** and an **animated tray icon**.
- `[TELEMETRY]` **TelemetryService** – Anonymous usage reporting with a copy-friendly user ID (see `PRIVACY.md`).
- `[PERF]` **Memory & CPU optimization** – Lower resource usage while idle.
- `[DOCS]` **ARCHITECTURE.md** – New architecture documentation (EN + ID).

---

## v1.3.7 — Expanded Language Support

- `[LOC]` **Multi-language support** – New `LanguageManager` with **20+ languages** and dynamic language switching.
- `[FEAT]` **New RPC status text** – Updated presence status lines.

---

## v1.3.6 — Silent Auto-Update & Shortcut Manager

- `[FEAT]` **Silent Auto-Update** – Background update checker that updates without interrupting your work.
- `[FEAT]` **Shortcut Manager** – Create/remove Desktop and Start Menu shortcuts from the tray menu.

---

## v1.3.5 — Centralized Logging System

- `[CORE]` **Centralized logging** – Unified log service replacing scattered logging, with a single log file.

---

## v1.3.4 — Sticky Presence Update

- `[FEAT]` **Sticky Rich Presence** – Presence persists across brief app switches to avoid flicker.
- `[PERF]` **Zero Input Lag** – Reduced hook latency for hotkeys and mouse tracking.
- `[UX]` **Non-Intrusive Updates** – Update prompts no longer interrupt your workflow.
- `[PRIVACY]` **Privacy Policy** – Added `PRIVACY.md` documenting data handling.

---

## v1.3.0 – v1.3.3 — Maintenance

- `[MAINT]` **Stabilization** – Minor fixes, asset cleanup and README/docs updates across the 1.3 line.

---

## v1.0.0 – v1.2.9 — Early Releases

The early versions built the foundation of geetRPCS. Tag history for this period only carries documentation commits, so these entries are reconstructed from the READMEs of the time.

### v1.2.x — Docs & Repo Polish

- `[DOCS]` Supported-app list revisions and README refactors (EN + ID).
- `[SEC]` VirusTotal link and **SHA-256 hash** added for build verification.
- `[MAINT]` New issue template and repository cleanup across v1.2.1 – v1.2.9.

### v1.1.0 — Licensing

- `[DOCS]` License information added and updated in the README.

### v1.0.0 — Initial Release

- `[FEAT]` **Automatic Discord Rich Presence** – Detects the app you're using and shows it on Discord in real time, hassle-free.
- `[DATA]` Initial app database – FL Studio, Ableton Live, Adobe Audition / Premiere Pro / After Effects / Photoshop / Illustrator / Lightroom, CapCut, Affinity, browsers (Brave, Chrome, Firefox, Zen Browser, Edge) and Office (Word, Excel, PowerPoint).
- `[FEAT]` By v1.2.9 the core feature set already included: **hybrid detection** (event-based + polling), **single instance** enforcement, **ultra-low RAM** (5–15 MB), **tray animation**, **smart preview window**, **app manager** (blacklist), **pause & private mode**, **statistics** (tracking + CSV/JSON export), **multi-language** (EN/ID), **mouse energy detector**, **true hot reload**, **quick actions**, **auto startup**, and **custom assets**.

---

<div align="center">
<sub>Made with ❤️ by geetcr4ck • © 2026 geetRPCS</sub>
</div>
