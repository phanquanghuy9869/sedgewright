using System;
using System.IO;

namespace Sedgewright.Infras;

internal static class StoragePathResolver
{
    public static string GetDefaultDatabasePath()
    {
        string applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(applicationData, "Sentinel.UI", "sentinel.db");
    }
}
