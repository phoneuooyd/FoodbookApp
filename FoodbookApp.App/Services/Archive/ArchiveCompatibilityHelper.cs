using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Foodbook.Services.Archive
{
    public static class ArchiveCompatibilityHelper
    {
        private static readonly string[] TablesToEnsure = new[]
        {
            "Recipes",
            "Ingredients",
            "RecipeLabels",
            "Folders",
            "PlannedMeals",
            "ShoppingListItems",
            "Plans"
        };

        public static void EnsureTimestampColumnsExist(string dbPath, Action<string>? log = null)
        {
            if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            {
                log?.Invoke($"ArchiveCompatibilityHelper: DB file not found: {dbPath}");
                return;
            }

            try
            {
                using var conn = new SqliteConnection($"Data Source={dbPath}");
                conn.Open();

                foreach (var table in TablesToEnsure)
                {
                    try
                    {
                        if (!TableExists(conn, table))
                        {
                            log?.Invoke($"ArchiveCompatibilityHelper: table '{table}' not present in archive DB");
                            continue;
                        }

                        var cols = GetColumnNames(conn, table);

                        if (!cols.Contains("CreatedAt"))
                        {
                            ExecuteNonQuery(conn, $"ALTER TABLE \"{table}\" ADD COLUMN CreatedAt TEXT;");
                            log?.Invoke($"ArchiveCompatibilityHelper: Added CreatedAt column to {table}");
                            // Set fallback to now for existing rows
                            ExecuteNonQuery(conn, $"UPDATE \"{table}\" SET CreatedAt = datetime('now') WHERE CreatedAt IS NULL;");
                            log?.Invoke($"ArchiveCompatibilityHelper: Populated CreatedAt defaults for {table}");
                        }

                        if (!cols.Contains("UpdatedAt"))
                        {
                            ExecuteNonQuery(conn, $"ALTER TABLE \"{table}\" ADD COLUMN UpdatedAt TEXT;");
                            log?.Invoke($"ArchiveCompatibilityHelper: Added UpdatedAt column to {table}");
                            // Default UpdatedAt to CreatedAt when possible
                            ExecuteNonQuery(conn, $"UPDATE \"{table}\" SET UpdatedAt = CreatedAt WHERE UpdatedAt IS NULL;");
                            log?.Invoke($"ArchiveCompatibilityHelper: Populated UpdatedAt defaults for {table}");
                        }
                    }
                    catch (Exception exTable)
                    {
                        log?.Invoke($"ArchiveCompatibilityHelper: Error ensuring columns on table {table}: {exTable.Message}");
                    }
                }

                conn.Close();
            }
            catch (Exception ex)
            {
                log?.Invoke($"ArchiveCompatibilityHelper: Failed to open DB {dbPath}: {ex.Message}");
            }
        }

        private static bool TableExists(SqliteConnection conn, string tableName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND lower(name)=lower($name);";
            cmd.Parameters.AddWithValue("$name", tableName);
            using var rdr = cmd.ExecuteReader();
            return rdr.Read();
        }

        private static HashSet<string> GetColumnNames(SqliteConnection conn, string tableName)
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                var nameObj = rdr[1]; // PRAGMA table_info returns: cid, name, type, notnull, dflt_value, pk
                if (nameObj != null)
                {
                    var name = nameObj.ToString();
                    if (!string.IsNullOrWhiteSpace(name)) cols.Add(name);
                }
            }
            return cols;
        }

        private static void ExecuteNonQuery(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}
