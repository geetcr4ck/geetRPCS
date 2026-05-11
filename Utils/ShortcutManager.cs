using System;
using System.Diagnostics;
using System.IO;
using geetRPCS.Services;

namespace geetRPCS.Utils
{
    public static class ShortcutManager
    {
        private static readonly string AppName = "geetRPCS";
        private static readonly string ExePath = Environment.ProcessPath!;
        private static readonly string WorkingDir = Path.GetDirectoryName(ExePath) ?? "";
 
        // Check if desktop shortcut exists
        public static bool IsDesktopShortcutExists()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = Path.Combine(desktopPath, $"{AppName}.lnk");
            return File.Exists(shortcutPath);
        }

        // Check if start menu shortcut exists
        public static bool IsStartMenuShortcutExists()
        {
            string startMenuPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs",
                AppName
            );
            string shortcutPath = Path.Combine(startMenuPath, $"{AppName}.lnk");
            return File.Exists(shortcutPath);
        }

        // Create desktop shortcut using PowerShell
        public static void CreateDesktopShortcut()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, $"{AppName}.lnk");

                // Remove existing shortcut if any
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }

                // Create shortcut using PowerShell
                CreateShortcutViaPowerShell(shortcutPath, ExePath, WorkingDir);

                LogService.Log($"Desktop shortcut created: {shortcutPath}", "INFO");
            }
            catch (Exception ex)
            {
                LogService.Log($"Failed to create desktop shortcut: {ex.Message}", "ERROR");
                throw;
            }
        }

        // Create start menu shortcut using PowerShell
        public static void CreateStartMenuShortcut()
        {
            try
            {
                string startMenuPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs",
                    AppName
                );
                
                // Create folder if it doesn't exist
                if (!Directory.Exists(startMenuPath))
                {
                    Directory.CreateDirectory(startMenuPath);
                }

                string shortcutPath = Path.Combine(startMenuPath, $"{AppName}.lnk");

                // Remove existing shortcut if any
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }

                // Create shortcut using PowerShell
                CreateShortcutViaPowerShell(shortcutPath, ExePath, WorkingDir);

                LogService.Log($"Start Menu shortcut created: {shortcutPath}", "INFO");
            }
            catch (Exception ex)
            {
                LogService.Log($"Failed to create Start Menu shortcut: {ex.Message}", "ERROR");
                throw;
            }
        }
        
        // Remove desktop shortcut
        public static void RemoveDesktopShortcut()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, $"{AppName}.lnk");

                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                    LogService.Log($"Desktop shortcut removed: {shortcutPath}", "INFO");
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"Failed to remove desktop shortcut: {ex.Message}", "ERROR");
                throw;
            }
        }

        // Remove start menu shortcut
        public static void RemoveStartMenuShortcut()
        {
            try
            {
                string startMenuPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs",
                    AppName
                );
                string shortcutPath = Path.Combine(startMenuPath, $"{AppName}.lnk");

                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                    LogService.Log($"Start Menu shortcut removed: {shortcutPath}", "INFO");
                }

                // Remove folder if empty
                if (Directory.Exists(startMenuPath) && Directory.GetFiles(startMenuPath).Length == 0)
                {
                    Directory.Delete(startMenuPath);
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"Failed to remove Start Menu shortcut: {ex.Message}", "ERROR");
                throw;
            }
        }

        // Refresh icon cache to make shortcuts appear immediately
        public static void RefreshIconCache()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ie4uinit.exe",
                    Arguments = "-show",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch
            {
                // Ignore errors, not critical
            }
        }

        // Create shortcut using PowerShell
        private static void CreateShortcutViaPowerShell(string shortcutPath, string targetPath, string workingDir)
        {
            // Escape paths for PowerShell
            string escapedShortcutPath = shortcutPath.Replace("'", "''");
            string escapedTargetPath = targetPath.Replace("'", "''");
            string escapedWorkingDir = workingDir.Replace("'", "''");

            string psCommand = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{escapedShortcutPath}')
$Shortcut.TargetPath = '{escapedTargetPath}'
$Shortcut.WorkingDirectory = '{escapedWorkingDir}'
$Shortcut.IconLocation = '{escapedTargetPath},0'
$Shortcut.Description = '{AppName} - Discord Rich Presence Custom Switcher'
$Shortcut.Save()
";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command {psCommand}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(psi))
            {
                if (process != null)
                {
                    process.WaitForExit(5000); // 5 second timeout
                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new Exception($"PowerShell shortcut creation failed: {error}");
                    }
                }
            }
        }
    }
}
