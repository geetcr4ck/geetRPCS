# Localization Guide for geetRPCS

Thank you for your interest in translating geetRPCS! 🌍

This project uses a JSON-based localization system. Translations are stored in the `Languages` folder.

## How to Add a New Language

1.  **Duplicate the Template**:
    Copy `Languages/en.json` (or `template.json`) and rename it to your language code (e.g., `fr.json` for French, `de.json` for German).

    > **Note**: Please use the [ISO 639-1](https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes) two-letter language code.

2.  **Translate Values**:
    Open your new JSON file and translate the values (the text on the right side).

    *   **Do not change the keys** (the text on the left side).
    *   **Preserve emojis** 🚀 unless they don't make sense in your language.
    *   **Keep format placeholders** like `{0}` exactly as they are. These are replaced by numbers or text by the app.

    **Example:**
    ```json
    "menu_pause": "⏸️ Pause",       // English
    "menu_pause": "⏸️ Pause",       // French (same)
    "menu_pause": "⏸️ Jeda",        // Indonesian
    ```

3.  **Test Your Translation**:
    *   Restart geetRPCS.
    *   Go to **Settings** (or edit `settings.json`).
    *   Change the language code to your new file name (e.g., "fr" for `fr.json`).
    *   Restart the app again to see changes.

4.  **Submit a Pull Request**:
    Send your new JSON file to us via GitHub!

## Key Names & Context

Most keys are self-explanatory, prefixed by where they appear:
*   `menu_`: Items in the tray menu.
*   `msg_`: Balloon tips or notification messages.
*   `dialog_`: Popup message boxes.
*   `error_`: Error messages.
*   `stats_`: Statistics window text.

## Validating JSON

Make sure your JSON is valid! You can use online validators like [jsonlint.com](https://jsonlint.com/) to check for syntax errors (like missing commas or quotes).

## Need Help?

If you're unsure about the context of a string, feel free to ask in the [Discussions](https://github.com/geetcr4ck/geetRPCS/discussions).
