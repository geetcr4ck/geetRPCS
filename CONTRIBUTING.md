# Contributing to geetRPCS

First off, thanks for taking the time to contribute! 🎉

The following is a set of guidelines for contributing to geetRPCS. These are just guidelines, not rules. Use your best judgment and feel free to propose changes to this document in a pull request.

## Welcome

We love pull requests from everyone. By participating in this project, you agree to abide by our Code of Conduct (implied: be respectful and kind).

## Getting Started

1.  **Fork the repository** on GitHub.
2.  **Clone your fork** locally:
    ```bash
    git clone https://github.com/geetcr4ck/geetRPCS.git
    cd geetRPCS
    ```
3.  **Install .NET 8.0 SDK** if you haven't already.
4.  **Build the project** to ensure everything is working:
    ```bash
    dotnet build
    ```

## Reporting Bugs

Bugs are tracked as GitHub issues. When filing an issue, please include:

*   **Version**: The version of geetRPCS you are using (e.g., v1.3.5).
*   **Operating System**: Windows version.
*   **Steps to Reproduce**: A clear list of steps to trigger the bug.
*   **Expected vs Actual Behavior**: What happened compared to what you expected.
*   **Logs**: Attach `geetRPCS.log` if possible (found in the application folder or `%LOCALAPPDATA%\geetRPCS\`).

## Suggesting Enhancements

Enhancement suggestions are welcome! Please create an issue or start a discussion on GitHub using the **Feature Request** template.

*   **Use a clear and descriptive title**.
*   **Provide a detailed description** of the suggested enhancement.
*   **Explain why this enhancement would be useful** to most users.

## Contributing Translations

Want to help translate geetRPCS into your language? We'd love that!

Please check out our [Localization Guide](LOCALIZATION.md) for detailed instructions on how to add a new language, how the `en.json` fallback works, and how to add a new key.

**Important:** every user-visible string must go through `LanguageManager` — never hardcode English in the UI. When adding a key, add it to `Models/Language.cs`, `Languages/en.json`, and `Languages/template.json`; the other 22 languages inherit from English until translated. Run the `Tests` project to make sure every language file still parses as JSON.


## Pull Requests

1.  **Create a new branch** for your changes:
    ```bash
    git checkout -b fix/amazing-fix
    # or
    git checkout -b feat/amazing-feature
    ```
2.  **Make your changes**.
3.  **Test your changes**. Ensure the application builds and runs correctly:
    ```bash
    dotnet build
    dotnet run --project Tests
    ```
    The `Tests` project is a dependency-free runner that validates `apps.json` integrity and app-ID rules — run it after any change to `apps.json`, `Languages/*.json`, or the localization model.
4.  **Follow the coding style**:
    *   Use standard C# coding conventions.
    *   Keep code clean and readable.
    *   Add comments where necessary.
5.  **Commit your changes** with a descriptive commit message:
    ```bash
    git commit -m "Fix: specific issue description"
    ```
6.  **Push to your fork**:
    ```bash
    git push origin fix/amazing-fix
    ```
7.  **Open a Pull Request** against the `main` branch of the original repository.

## Coding Style

*   We use **C#** and **.NET 8.0**.
*   Follow [Microsoft's C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).
*   Ensure all new features handle potential errors gracefully (try-catch blocks where appropriate).
*   Logging: Use the centralized logging system (`Logger.LogError`, `Logger.LogInfo`) instead of `Console.WriteLine`.
*   Localization: All user-visible strings must be routed through `LanguageManager` (see [LOCALIZATION.md](LOCALIZATION.md)).

## License

By contributing, you agree that your contributions will be licensed under its **Apache 2.0 License**.
