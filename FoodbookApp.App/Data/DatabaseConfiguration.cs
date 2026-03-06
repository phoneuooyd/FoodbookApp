using System;
using System.IO;
using Microsoft.Maui.Storage;

#if ANDROID
using Android.App;
#endif

namespace Foodbook.Data
{
    public static class DatabaseConfiguration
    {
        private const string DatabaseFileName = "foodbookapp.db";

        public static string GetDatabasePath()
        {
#if ANDROID
            return GetAndroidExternalFilesPath();
#else
            return Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
#endif
        }

#if ANDROID
        private static string GetAndroidExternalFilesPath()
        {
            var context = Android.App.Application.Context;
            var dir = context.GetExternalFilesDir("database")
                ?? throw new InvalidOperationException("ExternalFilesDir not available");

            if (!Directory.Exists(dir.AbsolutePath))
                Directory.CreateDirectory(dir.AbsolutePath);

            var dbPath = Path.Combine(dir.AbsolutePath, DatabaseFileName);
            System.Diagnostics.Debug.WriteLine($"[DatabaseConfiguration] ExternalFilesDir path: {dbPath}");
            return dbPath;
        }
#endif

        public static string GetConnectionString() => $"Data Source={GetDatabasePath()}";

        public static (string walPath, string shmPath) GetWalFiles()
        {
            var dbPath = GetDatabasePath();
            return (dbPath + "-wal", dbPath + "-shm");
        }
    }
}
