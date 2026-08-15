# Localization Guide for geetRPCS

Thank you for your interest in translating geetRPCS! 🌍

This project uses a JSON-based localization system. Translations live in the `Languages` folder (`Languages\*.json`), one file per language, keyed by ISO 639-1 codes (e.g. `en.json`, `id.json`). The app loads the file matching the language selected in `settings.json` (`"language": "id"`).

## How Localization Works (Architecture)

1. **`Services/LanguageManager.cs`** is the single access point. Every user-visible string is read via `LanguageManager.Current.<Property>`, e.g. `LanguageManager.Current.MenuPause`.
2. **Strongly-typed model** – `Models/Language.cs` declares one C# property per key, mapped with `[JsonPropertyName("snake_case_key")]`. Properties are plain strings; keys missing from a file deserialize to `null`.
3. **Loading** – `LanguageManager.LoadLanguage(code)` reads `Languages/{code}.json`, deserializes it into the `Language` model, and caches it. `Current` returns the cached instance, loading the code from `settings.json` (defaulting to `en`) on first use.

### The Fallback Chain

Missing translations never crash the app — they fall back through three layers:

1. **Per-file merge with English** – `MergeLanguage()` parses the requested file, then fills every key that is missing or `null` with the value from `en.json`. A new key added to `en.json` therefore automatically appears (in English) in all 23 other language files without touching them.

   > **Warning**: whenever the merge fills a missing key, `LanguageManager` logs a warning to `geetRPCS.log` — `Language "xx": N untranslated key(s) fell back to English: key1, key2`. A clean startup should show no such warnings; if you see one, that language file is missing keys (see the automated parity check below).
2. **Missing language file** – if `Languages/{code}.json` doesn't exist, the app falls back to `en.json` entirely and logs a warning.
3. **Code-level fallback** – key lookups in code use `LanguageManager.Current.X ?? "English fallback"`. This is a development safety net for keys that don't exist yet even in `en.json` (e.g. brand-new strings before translation files are updated).

**Practical consequence:** to add or change a string you only *must* update `en.json` (and `template.json`, the translators' reference). All other language files inherit the English text until someone translates it.

## How to Add a New Key

Adding a new user-visible string takes three steps:

1. **Add the key to `Languages/en.json`** – the source of truth:
   ```json
   "menu_foo": "Bar",
   ```
   (Also add it to `Languages/template.json` so translators see it.)

2. **Add the property to `Models/Language.cs`**:
   ```csharp
   [JsonPropertyName("menu_foo")]
   public string MenuFoo { get; set; }
   ```
   Keep the C# name PascalCase (`MenuFoo` for `menu_foo`) and place it next to related keys.

3. **Use it in code** – always with a fallback:
   ```csharp
   label.Text = LanguageManager.Current.MenuFoo ?? "Bar";
   ```
   For strings with placeholders, use `{0}`, `{1}`… in the JSON and `string.Format` in code:
   ```json
   "stats_found": "{0} apps found"
   ```
   ```csharp
   lblCount.Text = string.Format(LanguageManager.Current.StatsFound, count);
   ```

That's it. `dotnet build` catches a missing property; a missing JSON key simply shows the English fallback (or the code-level `??` fallback).

## Key Names & Context

Most keys are self-explanatory, prefixed by where they appear:

- `menu_`: Items in the tray menu.
- `msg_`: Balloon tips or notification messages.
- `dialog_`: Popup message boxes.
- `btn_`: Button labels.
- `error_`: Error messages.
- `stats_`: Statistics window text.
- `update_`: Update dialogs and status text.
- `preview_`: Preview window text.
- `window_`: Form titles.

## How to Add a New Language

1. **Duplicate the Template**: Copy `Languages/template.json` and rename it to your language code (e.g. `fr.json` for French, `de.json` for German).

   > **Note**: Please use the [ISO 639-1](https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes) two-letter language code.

2. **Translate Values**: Open your new JSON file and translate the values (the text on the right side).

   - **Do not change the keys** (the text on the left side).
   - **Preserve emojis** 🚀 unless they don't make sense in your language.
   - **Keep format placeholders** like `{0}` exactly as they are. These are replaced by numbers or text by the app.
   - Missing keys are fine — untranslated keys automatically fall back to English (see the fallback chain above). Start with the strings you care most about.

   **Example:**
   ```json
   "menu_pause": "⏸️ Pause",       // English
   "menu_pause": "⏸️ Pause",       // French (same)
   "menu_pause": "⏸️ Jeda",        // Indonesian
   ```

3. **Test Your Translation**:
   - Restart geetRPCS.
   - Edit `settings.json` (in `%LOCALAPPDATA%\geetRPCS`) and change `"language"` to your file name (e.g. `"fr"` for `fr.json`).
   - Restart the app again to see changes.

4. **Submit a Pull Request**: Send your new JSON file to us via GitHub!

## Validating JSON

Make sure your JSON is valid! You can use online validators like [jsonlint.com](https://jsonlint.com/) to check for syntax errors (like missing commas or quotes). The language files use CRLF line endings and UTF-8 encoding — keep them that way.

## Automated Validation (Tests project)

The `Tests/` project is a dependency-free console runner (`dotnet run --project Tests`) that CI runs on every push and pull request. A missing translation key **fails the run** with a non-zero exit code, listing the exact file and key, so untranslated keys are caught in CI instead of silently falling back to English.

The runner validates:

- **Language parity** – every key in `en.json` must exist in every other language file **and** `template.json`.
- **apps.json integrity** – every app entry has a valid 17–20 digit client ID, unique process names, non-empty large/small keys, and valid button URLs/labels (max 2 buttons, labels ≤ 32 chars).
- **App-ID rules** – `IsValidApplicationId()` boundaries (17–20 digits, digits only).
- **Telemetry default** – telemetry stays ON for new installs.

After editing any language file or `en.json`, run `dotnet run --project Tests` locally — it should end with `ALL TESTS PASSED`.

## Need Help?

If you're unsure about the context of a string, feel free to ask in the [Discussions](https://github.com/geetcr4ck/geetRPCS/discussions).
