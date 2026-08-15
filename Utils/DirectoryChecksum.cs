/**
 * geetRPCS - Directory Checksum
 * Deterministic combined SHA-256 over a directory's contents.
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

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace geetRPCS.Utils
{
    // Computes a deterministic SHA-256 over every file in a directory. Used to
    // verify that extracted update files are intact before Updater.exe copies
    // them over the installation. The main app computes the expected hash and
    // passes it to Updater.exe via --checksum; Updater recomputes it and aborts
    // on mismatch.
    //
    // IMPORTANT: this file is compiled into BOTH geetRPCS and UpdaterHelper
    // (linked from UpdaterHelper.csproj), so the algorithm MUST stay identical
    // in both binaries - do not change it in only one project.
    public static class DirectoryChecksum
    {
        // Hash input per file, in this exact order:
        //   "<relative path with forward slashes>" + 0x00 + "<raw file bytes>"
        // Files are processed sorted by relative path (ordinal, case-insensitive).
        public static string Compute(string directory)
        {
            string[] relPaths = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(directory, f))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            using var sha = SHA256.Create();
            using var hashStream = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);

            foreach (string rel in relPaths)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(rel.Replace('\\', '/'));
                hashStream.Write(nameBytes, 0, nameBytes.Length);
                hashStream.WriteByte(0);

                using var fs = new FileStream(Path.Combine(directory, rel), FileMode.Open, FileAccess.Read, FileShare.Read);
                fs.CopyTo(hashStream);
            }

            hashStream.FlushFinalBlock();
            return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
        }
    }
}
