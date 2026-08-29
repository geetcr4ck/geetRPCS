# Changelog

All notable changes to **geetRPCS**, newest first. Entries before v1.3.4 are condensed from repository history; the v1.0.0 – v1.2.9 period is reconstructed from git tags and the READMEs of the time.

---

## v1.4.4 — Miro Support

This update adds **Miro** to the app database so the collaborative whiteboard is detected and surfaces a Rich Presence with a custom icon and 18 new witty status lines. The data-driven model keeps the change to two JSON files (no code) and bumps both database versions to keep the in-app updater in sync.

### Added ✨

- `[FEAT]` **Miro App Entry** – New entry in `apps.json` under `UI/UX DESIGN` (process `Miro`, Discord Application ID `1543218469515034694`, large image key `miro`). The desktop app is detected by process name and switches to a dedicated Discord client; `apps.json`'s `db_version` bumps to `1.4.4` and the existing `Tests/Program.cs` validator already confirms the 19-digit clientId, unique process, and empty-button shape.
- `[FEAT]` **Miro Witty Texts** – 18 new lines in `witty.json` keyed by `Miro` (sticky-note storm, infinite canvas flex, mind map madness, wireframe warrior, design sprint running, voting dot democracy, etc.), matching the playful one-emoji-per-line tone of the Figma / Canva / Notion sets. `witty.json`'s `_version` bumps to `1.2.1` so the in-app updater picks the change up.
- `[ASSET]` **Miro Icon** – `assets/assetpack/miro.png` is added and `assets/AssetPack.zip` is rebuilt to include it so the in-app asset-pack download stays in sync with the source folder. Discord-side, the icon must be uploaded to the Miro Discord application under the asset name `miro` (matches `largeKey`); without that, the presence posts but the large image is blank.

### Changed 🔄

- `[DOC]` **README Mirrors** – `README.md` and `README.id.md` list Miro in the UI/UX Design row of the supported-apps table, alongside Figma and Canva.

---

## v1.4.2 — The Incognito, Custom Presence GUI & Built-in Guide Update

This update is about **privacy masking**, the **move off WinForms**, and removing the last reasons to open a JSON file: **rich presence can now be customized entirely from the system tray**, and a **built-in Help & Guide window** explains how the app works without opening GitHub. Every window and dialog is now Fluent WPF (`UI/Modern/`, ModernWpf), leaving WinForms to host only the tray icon and its Fluent-rendered menu. Browser private/incognito windows are detected automatically in any UI language, and the window title is redacted from Discord Rich Presence (the tested external patch now lives in the source tree; no PowerShell script is needed anymore). On top of that, the Manage Apps window lost its white flash and gained the native Windows open animation, and an instrumentation-driven pass cut idle CPU and RAM (working set avg 78.4 → 69.3 MB, max 90.6 → 72.6 MB; idle CPU 3.83% → 3.29% of one core, verified with `Tests/measure.ps1` and a ~142 samples/s screen-capture watcher).

### Added ✨

- `[FEAT]` **Custom Rich Presence Editor (GUI for config.json)** – New tray item "✨ Custom Rich Presence" opens a Fluent dialog editing the idle details/state, the active templates with click-to-insert placeholder chips (`{app_name}`, `{process_name}`, `{window_title}`, `{witty_text}`), the elapsed-time toggle, up to two buttons with live http(s)/label validation, and an advanced **Discord Application ID** section (live 17-20 digit validation, amber warning callout, tutorial + asset-pack links) that absorbs the old Change Application ID dialog — the separate tray item is gone; per-app App IDs remain in Manage Apps. Save writes through the new `AppCoordinator.SaveConfig`; Reset Default restores `GetDefaultConfig()`. `Assets` pass through untouched.
- `[FEAT]` **Full Per-App Override Editor** – The Manage Apps expander now customizes much more than Details/State: large image key/text, per-app Application ID (validated; invalid input is never propagated to the RPC client switch), elapsed time (three-state: inherit/on/off) and buttons. Empty fields inherit the app-database defaults; "Reset to default" clears the customization. The override model (`AppOverrideConfig`) gained `largeKey`, `largeText`, `showTimestamps`, `buttons` and `clientId` — all optional, backward compatible.
- `[FEAT]` **Add Custom App (GUI for apps.json)** – New "➕ Add Custom App" button in Manage Apps: process name (with `.exe` stripping, character validation and duplicate detection), match-mode pickers for process and optional window title, details template with placeholder chips, large image, timestamps and buttons. Entries are stored as `customApps` in `settings.json` and merged at load by `AppConfigManager` — a custom entry with the same process **replaces** the built-in (a way to tune built-ins), new processes are appended and become detectable. `apps.json` stays read-only, so the automatic app-database updates never remove user apps.
- `[FEAT]` **Effective-App Resolution** – New `AppConfigManager.GetEffectiveApp` merges a user override over the database entry (clone) and is now used at the three lookup sites: `PresenceBuilder` (timestamps/buttons), `PresenceAssets` (large image) and `AppCoordinator` (per-app Discord client switching). Details/State precedence stays in `PresenceBuilder` where it already lived.
- `[FEAT]` **Built-in Help & Guide** – New tray item "❓ Help & Guide" opens a Fluent window with six topics distilled from the README: Getting Started (tray overview + hotkeys), Customize Presence (placeholders, the new editors, custom apps, App ID & assets), Features (mouse energy, witty engine, private mode, preview & statistics), Updates & Stats, Troubleshooting (the "presence not showing" checklist, reload, log), and About (version + links to the full online README, tutorial, issues, discussions, releases). No documentation is shipped with the binary; the guide is localized instead.
- `[FEAT]` **Tray Theme Switcher** – New Theme submenu in the tray menu (System / Dark / Light) restyles the whole UI immediately, without a restart. The choice persists as the new `themeMode` setting in `settings.json`, applies to every ModernWpf window via `WpfHost.ApplyThemeMode`, and the tray menu itself re-renders its glyphs and colors with the matching palette (`ThemePalette`). The active mode is shown in the submenu.
- `[UX]` **Fluent Tray Menu** – The tray `ContextMenuStrip` is now drawn by a custom renderer (`UI/FluentMenuRenderer.cs`) with Segoe Fluent glyph bitmaps (`UI/Modern/FluentGlyphs.cs`), real checked-state icons and colors mapped from the active ModernWpf theme (`UI/ThemePalette.cs`), so the menu matches the Fluent windows instead of the native WinForms look.
- `[TEST]` **New Regression Tests** – Override precedence (each new field wins/ inherits), custom-app merge semantics (append, replace-by-process case-insensitively, exact-match set membership, advanced modes preserved), config JSON round-trip via `SerializeConfig`, plus WPF interop smoke tests for the Custom Rich Presence editor (pre-fill, button + App ID validation gating, save payload), the Guide window (six topics, topic rendering) and the always-on-top regressions (Manage Apps releases its Topmost pin after activation; the preview window opens un-pinned).
- `[FEAT]` **Automatic Private-Browsing Detection** – New `Utils/PrivateBrowsingDetector` recognizes private windows of Chrome, Edge, Firefox, Brave and Zen from the native window title and the MSAA accessible window name (new `Placeholders.GetAccessibleWindowName`, with a local COM interop declaration because the classic `Accessibility` assembly does not exist on .NET 8). A detected private window replaces `{window_title}` with `**********` instead of leaking the page title.
- `[FEAT]` **Language-Independent Chromium Detection** – For Chromium-based browsers the detector matches the *shape* of the private-mode annotation — `"<title> (<word>)"` appended to the accessible window name on a genuine browser window — rather than translated words, so private windows are caught in **every** UI language. Known non-private annotations (Guest, Tamu, Gast, Invité, Гость, ゲスト, 访客, …) are excluded so guest windows stay visible.
- `[FEAT]` **Extended Firefox/Zen Indicators** – Curated private-mode phrases for 35+ languages: DE, FR, ES, PT, IT, NL, PL, RU, UK, CS, SK, SL, HR, HU, EL, TR, RO, CA, BG, AR, HE, FA, HI, TH, VI, MS, FI, SV, DA, NB, LT, JA, KO, zh-CN and zh-TW, on top of the existing EN/ID set.
- `[TEST]` **Detector Test Coverage** – 47 automated cases in the dependency-free test runner: per-language positives, unknown-language annotations caught by the structural rule, and false-positive guards (lone "privé"/"anonymous" words, Brave guest windows, page titles ending in "(Private)", non-browser apps).
- `[TEST]` **Self-Test Harness** – New diagnostic flags: `--selftest-manageapps` opens/closes the Manage Apps window 3 times through the real tray-menu path then exits cleanly (pairs with an external screen watcher), and `--selftest-idle` runs normally for ~65s then exits cleanly (pairs with `Tests/measure.ps1`).
- `[PERF]` **RAM/CPU Measurement Harness** – New `Tests/measure.ps1` samples working set, private bytes and CPU time of an idle run, drops the startup-skewed first 10 seconds and writes a CSV + summary; baseline vs optimized runs are committed as the verification method for this update.
- `[CORE]` **Crash Trace Logging** – Global handlers for WPF `DispatcherUnhandledException`, `AppDomain.UnhandledException` and unobserved task exceptions now write to `geetRPCS.log` before the process goes down; the runtime log previously showed a process death with zero trace right after "Creating PresencePreviewWindow...". UI-thread exceptions from the WinForms tray host are routed to the AppDomain handler.
- `[CORE]` **Trim Telemetry** – `MemoryHelper.TrimMemory` logs the working set before/after at DEBUG, so memory behavior is finally visible in the log.
- `[TEST]` **New Regression Tests** – Manage Apps fresh-window lifecycle (Esc really closes), instant full-opacity show, and the presence-preview image-cache FIFO bound (16 entries, oldest evicted, newest kept). Full suite passes warning-free.

### Changed 🔄

- `[CORE]` **Watcher Tracks the Exact Foreground Window** – `TaskbarWatcher` now also hooks `EVENT_OBJECT_NAMECHANGE`, tracks the active window handle and refreshes presence when the foreground tab title changes; the existing 3-second liveness poll doubles as a fallback for browsers that miss name-change events.
- `[CORE]` **Serialized Presence Updates** – `AppCoordinator` stores the watcher's window handle and serializes presence updates with a dedicated lock, so a stale normal-window update can never overwrite a private-window mask; periodic refreshes (witty rotation, mouse energy, mode toggles) reuse the exact foreground window instead of guessing via `Process.MainWindowHandle` — the core fix for Brave multi-window masking.
- `[UX]` **Unified Title Redaction** – Manual Private Mode now shows the same `**********` redaction as automatic detection (previously a shorter `********` mask applied to manual mode only).
- `[UX]` **Fluent Message Dialogs** – New `UI/Modern/MessageDialog` (ModernWpf, same visual language as the Change App ID dialog, severity accent bar, Enter/Esc keyboard semantics) is now the app's single message surface: it replaces the remaining WinForms `InfoDialog`/`ConfirmDialog` Forms and every user-facing native `MessageBox` (app-ID results, shortcut and config errors, stats export, update-check failures, startup errors, link errors inside the WPF windows). The one remaining native box is the pre-initialization "already running" notice, where loading the whole ModernWpf stack for a duplicate-instance popup is not worth it.
- `[PERF]` **Idle CPU: Input-Side Machinery** – The global low-level mouse hook is fully uninstalled while Mouse Energy is toggled off (previously only accumulation was skipped, leaving up to ~1000 managed callbacks/s installed); the `EVENT_OBJECT_NAMECHANGE` filter compares against a cached foreground HWND instead of calling `GetForegroundWindow` for every system-wide title change; the 3-second liveness check uses one `GetProcessById` PID lookup instead of a full `Process.GetProcessesByName` system snapshot; and config matches are cached per process name with a 5-minute TTL.
- `[PERF]` **Mouse Energy Stability** – A state must hold ~10 seconds before committing (was 2) and energy-driven presence rebuilds are at least 30 seconds apart (was 5), ending the Normal/Relaxing flapping that rebuilt and re-pushed the full presence every 5-10 seconds during casual use. Both constants are named and commented for easy tuning back.
- `[PERF]` **Log Volume & I/O** – Mouse "Energy:" transition lines demoted to DEBUG (they were 27% of all real-app production log lines at INFO); the 1-second log flush skips quiet periods via a dirty flag; the witty rotation timer only runs while an app is actually tracked instead of waking the UI thread every 5 seconds forever.
- `[PERF]` **Memory Management** – `TrimMemory` now runs a forced blocking Gen2 collection (the old `GCCollectionMode.Optimized` call was frequently skipped by the runtime, so the managed heap never actually shrank; all call sites run on background threads), and Statistics, Update and Change App ID dialogs get the same deferred post-close trim the Manage Apps window already had.
- `[PERF]` **Presence Preview Footprint** – One process-shared static `HttpClient` replaces the per-open instance (no more handler/connection-pool churn on every open), and the decoded-image memory cache is FIFO-capped at 16 entries.
- `[DOC]` **Honest RAM Claim** – README (EN + ID) now states ~70 MB idle working set instead of the stale "5-20 MB"; the number comes from `Tests/measure.ps1` and the resident Fluent/ModernWpf UI stack is called out as by design.
- `[CORE]` **Generalized config.json Writing** – config.json persistence is now the single `SaveConfig(Config)` path (exposed to the tray as `ITrayCoordinator.SaveConfig`), used by the Custom Rich Presence editor; the old `ChangeApplicationId` method was removed together with its dialog.
- `[UX]` **Tray Menu Slimmed** – The redundant "Change Application ID..." item is gone: changing the default ID lives in Custom Rich Presence → advanced section, and per-app IDs live in Manage Apps.
- `[UX]` **Live Custom-App Changes** – Adding/removing a custom app refreshes the merged app list via `AppConfigManager.Reload` + `RefreshCurrentPresence` instead of a full `ReloadConfig`, so the RPC connection is never torn down for a small data change; removing the currently-active custom app falls back to idle presence.
- `[DOC]` **Localization Note** – ~66 new keys per language (presence editors + guide). English values are the placeholder in the 22 non-English/non-Indonesian languages until translators catch up (runtime already falls back per-key to English); **Indonesian is fully translated**.

### Removed 🗑️

- `[MAINT]` **The Last WinForms Windows** – `ManageAppsForm`, `PresencePreviewForm`, `InfoDialog` and `ConfirmDialog` are all gone; their Fluent WPF replacements live in `UI/Modern/`, with `MessageDialog` taking over the message-surface role (see Changed).
- `[PERF]` **Scheduled Memory-Trim Loop** – The unconditional every-30-minutes `EmptyWorkingSet` cycle is gone; it was the classic "RAM optimizer" anti-pattern (paged-out pages immediately soft-fault back in). Trims are now purely event-driven.
- `[MAINT]` **Dead Reuse Machinery** – The `_reallyClose`/`CloseForReal` leftovers from the abandoned hide-don't-close window lifecycle were removed.
- `[MAINT]` **HandBrake Support** – The HandBrake entry was removed from `apps.json`, its witty texts from `witty.json`, and its asset from the asset pack; HandBrake is no longer detected.

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
- `[TEST]` **Automated Test Suite** – New dependency-free `Tests` project validating ID rules, telemetry defaults, `apps.json` integrity (unique processes, valid client ids, non-empty image keys, valid button URLs/labels, max 2 buttons) **and language parity** (every `en.json` key exists in every language file and `template.json`).
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
