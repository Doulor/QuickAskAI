// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace AIExtension;

internal static class StableStorage
{
    private const string FolderName = "QuickAskAI";

    public static string GetPath(string fileName)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            FolderName,
            fileName);
    }

    public static void MigrateFromLegacyPath(string fileName, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            return;
        }

        var legacyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName,
            fileName);

        if (!File.Exists(legacyPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(legacyPath, targetPath, overwrite: false);
    }
}
