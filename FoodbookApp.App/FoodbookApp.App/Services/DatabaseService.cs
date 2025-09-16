using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Data;

namespace Foodbook.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly IServiceProvider _services;

        public DatabaseService(IServiceProvider services)
        {
            _services = services;
        }

        public async Task InitializeAsync()
        {
            try
            {
                LogDebug("Initializing database synchronously (via DatabaseService)");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode=WAL;";
                    await cmd.ExecuteNonQueryAsync();
                }

                // EF migrations
                await db.Database.MigrateAsync();

                // Additional schema/validation
                await ApplyDatabaseMigrationsAsync(db);

                // Seed
                await SeedData.InitializeAsync(db);

                LogDebug("Database initialization finished");
            }
            catch (Exception ex)
            {
                LogError($"DB init failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public async Task<bool> EnsureDatabaseSchemaAsync()
        {
            try
            {
                LogDebug("Starting emergency database schema validation");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Use migrations instead of EnsureCreated to avoid bypassing migration history
                await db.Database.MigrateAsync();
                await ValidateDatabaseSchemaAsync(db);
                LogDebug("Emergency database schema validation completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Emergency database schema validation failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> MigrateDatabaseAsync()
        {
            try
            {
                LogDebug("Starting database migration and schema validation");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Apply EF migrations first
                await db.Database.MigrateAsync();
                // Then run additional validation/custom migrations
                await ValidateDatabaseSchemaAsync(db);
                await ApplyDatabaseMigrationsAsync(db);
                LogDebug("Database migration and schema validation completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Database migration failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> ResetDatabaseAsync()
        {
            try
            {
                LogDebug("Starting database reset");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                LogDebug("Deleting and recreating database");
                await db.Database.EnsureDeletedAsync();
                // Recreate schema using EF migrations instead of EnsureCreated
                await db.Database.MigrateAsync();
                await SeedData.InitializeAsync(db);
                LogDebug("Database reset completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Database reset failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        // Internal helpers migrated from MauiProgram
        private static async Task ValidateDatabaseSchemaAsync(AppDbContext db)
        {
            try
            {
                LogDebug("Starting comprehensive database schema validation");

                await EnsureTableExistsAsync(db, "Folders",
                    "CREATE TABLE IF NOT EXISTS [Folders] (\n" +
                    "[Id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,\n" +
                    "[Name] TEXT NOT NULL,\n" +
                    "[Description] TEXT,\n" +
                    "[ParentFolderId] INTEGER,\n" +
                    "[CreatedAt] TEXT NOT NULL DEFAULT '0001-01-01T00:00:00Z'\n)");

                await ValidateTableSchemaAsync(db, "Ingredients", new[]
                {
                    ("Id", "INTEGER"),
                    ("Name", "TEXT"),
                    ("Quantity", "REAL"),
                    ("Unit", "INTEGER"),
                    ("Calories", "REAL"),
                    ("Protein", "REAL"),
                    ("Fat", "REAL"),
                    ("Carbs", "REAL"),
                    ("RecipeId", "INTEGER")
                });

                await ValidateTableSchemaAsync(db, "Recipes", new[]
                {
                    ("Id", "INTEGER"),
                    ("Name", "TEXT"),
                    ("Description", "TEXT"),
                    ("Calories", "REAL"),
                    ("Protein", "REAL"),
                    ("Fat", "REAL"),
                    ("Carbs", "REAL"),
                    ("IloscPorcji", "INTEGER"),
                    ("FolderId", "INTEGER")
                });

                await ValidateTableSchemaAsync(db, "Plans", new[]
                {
                    ("Id", "INTEGER"),
                    ("StartDate", "TEXT"),
                    ("EndDate", "TEXT"),
                    ("IsArchived", "INTEGER")
                });

                await ValidateTableSchemaAsync(db, "PlannedMeals", new[]
                {
                    ("Id", "INTEGER"),
                    ("RecipeId", "INTEGER"),
                    ("Date", "TEXT"),
                    ("Portions", "INTEGER")
                });

                await ValidateTableSchemaAsync(db, "ShoppingListItems", new[]
                {
                    ("Id", "INTEGER"),
                    ("PlanId", "INTEGER"),
                    ("IngredientName", "TEXT"),
                    ("Unit", "INTEGER"),
                    ("IsChecked", "INTEGER"),
                    ("Quantity", "REAL"),
                    ("Order", "INTEGER")
                });

                await ValidateTableSchemaAsync(db, "Folders", new[]
                {
                    ("Id", "INTEGER"),
                    ("Name", "TEXT"),
                    ("Description", "TEXT"),
                    ("ParentFolderId", "INTEGER"),
                    ("CreatedAt", "TEXT")
                });

                LogDebug("Database schema validation completed successfully");
            }
            catch (Exception ex)
            {
                LogError($"Error during database schema validation: {ex.Message}");
                throw;
            }
        }

        private static async Task ValidateTableSchemaAsync(AppDbContext db, string tableName, (string columnName, string expectedType)[] expectedColumns)
        {
            try
            {
                LogDebug($"Validating table: {tableName}");
                var currentColumns = await GetTableColumnsAsync(db, tableName);
                if (!currentColumns.Any())
                {
                    LogWarning($"Table {tableName} does not exist or has no columns");
                    return;
                }

                var missingColumns = new List<string>();
                var typeConflicts = new List<string>();

                foreach (var (columnName, expectedType) in expectedColumns)
                {
                    var existingColumn = currentColumns.FirstOrDefault(c => c.name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                    if (existingColumn.name == null)
                    {
                        missingColumns.Add($"{columnName} ({expectedType})");
                    }
                    else
                    {
                        var actualType = MapSqliteType(existingColumn.type);
                        var expectedMappedType = MapSqliteType(expectedType);
                        if (!actualType.Equals(expectedMappedType, StringComparison.OrdinalIgnoreCase))
                        {
                            typeConflicts.Add($"{columnName}: expected {expectedType}, got {existingColumn.type}");
                        }
                    }
                }

                foreach (var missingColumn in missingColumns)
                {
                    var columnParts = missingColumn.Split('(', ')');
                    var columnName = columnParts[0].Trim();
                    var columnType = columnParts.Length > 1 ? columnParts[1].Trim() : "TEXT";
                    await AddMissingColumnAsync(db, tableName, columnName, columnType);
                }

                foreach (var conflict in typeConflicts)
                {
                    LogWarning($"Type conflict in {tableName}: {conflict}");
                }

                LogDebug($"Table {tableName} validation completed. Missing columns added: {missingColumns.Count}, Type conflicts: {typeConflicts.Count}");
            }
            catch (Exception ex)
            {
                LogError($"Error validating table {tableName}: {ex.Message}");
                throw;
            }
        }

        private static async Task<List<(string name, string type)>> GetTableColumnsAsync(AppDbContext db, string tableName)
        {
            try
            {
                var query = $"PRAGMA table_info([{tableName}])";
                var connection = db.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                var columns = new List<(string name, string type)>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    var type = reader.GetString(2);
                    columns.Add((name, type));
                }
                return columns;
            }
            catch (Exception ex)
            {
                LogError($"Error getting columns for table {tableName}: {ex.Message}");
                return new List<(string, string)>();
            }
        }

        private static async Task AddMissingColumnAsync(AppDbContext db, string tableName, string columnName, string columnType)
        {
            try
            {
                LogDebug($"Adding missing column {columnName} ({columnType}) to table {tableName}");
                string defaultValue;
                if (tableName.Equals("Folders", StringComparison.OrdinalIgnoreCase) &&
                    columnName.Equals("CreatedAt", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(columnType, "TEXT", StringComparison.OrdinalIgnoreCase))
                {
                    defaultValue = "'0001-01-01T00:00:00Z'";
                }
                else
                {
                    defaultValue = GetDefaultValueForType(columnType);
                }

                var sql = $"ALTER TABLE [{tableName}] ADD COLUMN [{columnName}] {columnType} NOT NULL DEFAULT {defaultValue}";
                await db.Database.ExecuteSqlRawAsync(sql);
                LogDebug($"Successfully added column {columnName} to table {tableName}");
            }
            catch (Exception ex)
            {
                LogError($"Error adding column {columnName} to table {tableName}: {ex.Message}");
                throw;
            }
        }

        private static async Task EnsureTableExistsAsync(AppDbContext db, string tableName, string createSql)
        {
            try
            {
                var exists = await db.Database.SqlQueryRaw<int>($"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name={{0}}", tableName).FirstOrDefaultAsync();
                if (exists == 0)
                {
                    LogWarning($"Table {tableName} is missing. Creating it now.");
                    await db.Database.ExecuteSqlRawAsync(createSql);
                    LogDebug($"Table {tableName} created.");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error ensuring table {tableName} exists: {ex.Message}");
                throw;
            }
        }

        private static string GetDefaultValueForType(string sqliteType) => sqliteType.ToUpperInvariant() switch
        {
            "INTEGER" => "0",
            "REAL" => "0.0",
            "TEXT" => "''",
            "BLOB" => "''",
            _ => "''"
        };

        private static string MapSqliteType(string type) => type.ToUpperInvariant() switch
        {
            "INT" or "INTEGER" or "BIGINT" => "INTEGER",
            "REAL" or "DOUBLE" or "FLOAT" => "REAL",
            "TEXT" or "VARCHAR" or "CHAR" => "TEXT",
            "BLOB" => "BLOB",
            _ => "TEXT"
        };

        private static async Task ApplyDatabaseMigrationsAsync(AppDbContext db)
        {
            try
            {
                LogDebug("Applying database migrations");
                await EnsureDatabaseVersionAsync(db);
                LogDebug("All migrations applied successfully");
            }
            catch (Exception ex)
            {
                LogError($"Error applying migrations: {ex.Message}");
                throw;
            }
        }

        private static async Task EnsureDatabaseVersionAsync(AppDbContext db)
        {
            try
            {
                _ = await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"DatabaseVersion\" (\"Version\" INTEGER PRIMARY KEY)");
                var currentVersion = await GetDatabaseVersionAsync(db);
                LogDebug($"Current database version: {currentVersion}");
                if (currentVersion < 1)
                {
                    await MigrateToVersion1Async(db);
                    await SetDatabaseVersionAsync(db, 1);
                }
            }
            catch (Exception ex)
            {
                LogError($"Error managing database version: {ex.Message}");
                throw;
            }
        }

        private static async Task<int> GetDatabaseVersionAsync(AppDbContext db)
        {
            try
            {
                var result = await db.Database.SqlQueryRaw<int>("SELECT COALESCE(MAX(Version), 0) FROM DatabaseVersion").FirstOrDefaultAsync();
                return result;
            }
            catch
            {
                return 0;
            }
        }

        private static async Task SetDatabaseVersionAsync(AppDbContext db, int version)
            => await db.Database.ExecuteSqlRawAsync("INSERT OR REPLACE INTO DatabaseVersion (Version) VALUES ({0})", version);

        private static async Task MigrateToVersion1Async(AppDbContext db)
        {
            LogDebug("Migrating to version 1");
            await ValidateDatabaseSchemaAsync(db);
            LogDebug("Migration to version 1 completed");
        }

        private static void LogDebug(string message) => System.Diagnostics.Debug.WriteLine($"[DatabaseService] {message}");
        private static void LogWarning(string message) => System.Diagnostics.Debug.WriteLine($"[DatabaseService] WARNING: {message}");
        private static void LogError(string message) => System.Diagnostics.Debug.WriteLine($"[DatabaseService] ERROR: {message}");
    }
}
