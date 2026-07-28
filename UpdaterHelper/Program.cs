/**
 * geetRPCS - Updater Helper
 * Helper utility for performing automated updates
 */
/*
 * Copyright (c) 2026 geetcr4ck
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 */

#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;

[assembly: Guid("C649A8F7-9477-4402-8618-97F107198C82")]

namespace geetRPCS.Updater
{
    class Program
    {
        private const int MAX_WAIT_SECONDS = 30;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);

        const int MOVEFILE_REPLACE_EXISTING = 0x1;
        const int MOVEFILE_COPY_ALLOWED = 0x2;

        static int Main(string[] args)
        {
            try
            {
                Console.Title = "geetRPCS Maintenance";
                ShowHeader();

                // Parse args
                string? sourcePath = null;
                string? targetPath = null;
                string? exeName = null;

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i].ToLowerInvariant();
                    if (arg == "--source" && i + 1 < args.Length)
                        sourcePath = args[++i];
                    else if (arg == "--target" && i + 1 < args.Length)
                        targetPath = args[++i];
                    else if (arg == "--exe" && i + 1 < args.Length)
                        exeName = args[++i];
                }

                if (string.IsNullOrEmpty(sourcePath) ||
                    string.IsNullOrEmpty(targetPath) ||
                    string.IsNullOrEmpty(exeName))
                {
                    ShowUsage();
                    return 1;
                }

                Log("Source: " + sourcePath);
                Log("Target: " + targetPath);
                Console.WriteLine();

                if (!Directory.Exists(sourcePath))
                {
                    Error("Source not found!");
                    Wait();
                    return 1;
                }

                // Step 1: Wait for app to close
                Step(1, "Waiting for application to close...");
                if (!WaitForExit("geetRPCS", MAX_WAIT_SECONDS))
                {
                    Error("Application still running!");
                    Wait();
                    return 1;
                }
                OK();

                // Step 2: Update files (overwrite)
                Step(2, "Updating files...");
                Thread.Sleep(500); // Deliberate delay

                if (!UpdateFiles(sourcePath, targetPath))
                {
                    Error("Update failed!");
                    Wait();
                    return 1;
                }
                OK();

                // Step 3: Cleanup temp
                Step(3, "Finalizing...");
                Thread.Sleep(300);
                Cleanup(sourcePath);
                OK();

                // Step 4: Launch
                Step(4, "Starting application...");
                Thread.Sleep(500);

                string exePath = Path.Combine(targetPath, exeName);
                if (File.Exists(exePath))
                {
                    LaunchViaExplorer(exePath);
                    OK();
                }
                else
                {
                    Error("Executable not found!");
                }

                ShowSuccess();
                Thread.Sleep(2000);
                return 0;
            }
            catch (Exception ex)
            {
                Error(ex.Message);
                Wait();
                return 1;
            }
        }

        static bool UpdateFiles(string source, string target)
        {
            try
            {
                Directory.CreateDirectory(target);
                string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
                int total = files.Length;
                int current = 0;

                foreach (string file in files)
                {
                    current++;

                    // Get relative path
                    string rel = file.Substring(source.Length).TrimStart('\\', '/');
                    string dest = Path.Combine(target, rel);

                    // Skip Updater.exe - can't update self while running
                    if (rel.Equals("Updater.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.Write($"\r  [{current}/{total}] {rel,-40} [SKIPPED]");
                        Thread.Sleep(20);
                        continue;
                    }

                    // Create directory
                    string? dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    // Progress
                    Console.Write($"\r  [{current}/{total}] {rel,-40}");

                    bool success = MoveFileEx(file, dest,
                        MOVEFILE_REPLACE_EXISTING | MOVEFILE_COPY_ALLOWED);

                    if (!success)
                    {
                        // Fallback to File.Copy
                        try { File.Copy(file, dest, true); }
                        catch { return false; }
                    }

                    Thread.Sleep(20);
                }

                Console.WriteLine();
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void LaunchViaExplorer(string path)
        {
            // Method 1: Via explorer.exe
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = false
                });
                return;
            }
            catch { }

            // Method 2: ShellExecute fallback
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        static bool WaitForExit(string name, int seconds)
        {
            for (int i = 0; i < seconds; i++)
            {
                bool running = false;
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase))
                            running = true;
                        p.Dispose();
                    }
                    catch { }
                }

                if (!running) return true;

                Console.Write($"\r  Waiting... {seconds - i}s   ");
                Thread.Sleep(1000);
            }
            Console.WriteLine();
            return false;
        }

        static void Cleanup(string source)
        {
            try
            {
                string? parent = Path.GetDirectoryName(source);
                if (!string.IsNullOrEmpty(parent) && parent.Contains("geetRPCS_update"))
                {
                    Thread.Sleep(200);
                    Directory.Delete(parent, true);
                }
            }
            catch { }
        }

        // === UI Helpers ===

        static void ShowHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("  geetRPCS Maintenance Tool v1.3.9");
            Console.WriteLine("  --------------------------------");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void ShowSuccess()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Update completed successfully!");
            Console.ResetColor();
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage: Updater.exe --source <path> --target <path> --exe <name>");
            Wait();
        }

        static void Step(int n, string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  [{n}/4] ");
            Console.ResetColor();
            Console.WriteLine(msg);
        }

        static void Log(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("  " + msg);
            Console.ResetColor();
        }

        static void OK()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Done.");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void Error(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  Error: " + msg);
            Console.ResetColor();
        }

        static void Wait()
        {
            Console.WriteLine();
            Console.WriteLine("  Press any key to exit...");
            Console.ReadKey(true);
        }
    }
}