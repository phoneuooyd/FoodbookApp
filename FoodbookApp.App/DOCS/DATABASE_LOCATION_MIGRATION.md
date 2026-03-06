# Database Location Migration - Technical Summary

## Problem Solved
Visual Studio deployment on Android **always uninstalls the APK** before reinstalling, which wipes `/data/data/<package>/` (AppDataDirectory). This caused **user data loss** on every deployment, even with `EmbedAssembliesIntoApk=true`.

## Solution Implemented
Moved database from **AppDataDirectory** to **Android ExternalFilesDir**, which:
- ? **Survives VS deployment reinstalls**
- ? **Requires no runtime permissions** (Android 10+)
- ? **Is deleted on manual app uninstall** (correct behavior)
- ? **Automatic migration** from old location

---

## Technical Details

### Old Location (? Wiped on deploy)
```
/data/data/com.companyname.foodbookapp/files/foodbookapp.db
```

### New Location (? Survives deploy)
```
/storage/emulated/0/Android/data/com.companyname.foodbookapp/files/database/foodbookapp.db
```

---

## Code Changes

### 1. `Data/DatabaseConfiguration.cs`

**Platform-Specific Path Resolution:**
```csharp
public static string GetDatabasePath()
{
#if ANDROID
    return GetAndroidDatabasePath(); // ExternalFilesDir
#else
    return Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName); // Standard
#endif
}
```

**Android Implementation:**
```csharp
private static string GetAndroidDatabasePath()
{
    var context = Android.App.Application.Context;
    var externalDir = context.GetExternalFilesDir(null); // No permissions needed
    var dbDir = Path.Combine(externalDir.AbsolutePath, "database");
    
    if (!Directory.Exists(dbDir))
        Directory.CreateDirectory(dbDir);
    
    return Path.Combine(dbDir, DatabaseFileName);
}
```

**Automatic Migration Logic:**
```csharp
public static bool MigrateFromAppDataIfNeeded()
{
    if (!NeedsMigrationFromAppData())
        return true; // Already migrated or nothing to migrate

    var oldDbPath = GetOldAppDataDatabasePath();
    var newDbPath = GetDatabasePath();
    
    // Copy main DB + WAL + SHM files
    File.Copy(oldDbPath, newDbPath, overwrite: false);
    
    // Copy WAL/SHM if they exist
    // ... (see code for full implementation)
    
    // Delete old files after successful copy
    File.Delete(oldDbPath);
    
    return true;
}
```

### 2. `Services/DatabaseService.cs`

**Migration Trigger in ConditionalDeploymentAsync:**
```csharp
public async Task<bool> ConditionalDeploymentAsync()
{
    try
    {
#if ANDROID
        // CRITICAL: Migrate database BEFORE any other DB operations
        if (DatabaseConfiguration.NeedsMigrationFromAppData())
        {
            Log("Detected old database in AppDataDirectory - migrating to external storage...");
            bool migrated = DatabaseConfiguration.MigrateFromAppDataIfNeeded();
            if (migrated)
                Log("? Database migrated to external storage (survives reinstalls)");
            else
                Log("ERROR: Database migration failed - app may lose data on reinstall");
        }
#endif
        
        // ... rest of deployment logic
    }
}
```

---

## Migration Flow

### First Launch After Update

1. **App starts** ? `ConditionalDeploymentAsync()` called
2. **Check** ? `NeedsMigrationFromAppData()` returns `true`
   - Old DB exists at `/data/data/.../files/foodbookapp.db`
   - New DB does NOT exist at `/storage/emulated/0/Android/data/.../files/database/foodbookapp.db`
3. **Migrate** ? `MigrateFromAppDataIfNeeded()` executes
   - Copy `foodbookapp.db` to new location
   - Copy `foodbookapp.db-wal` (if exists)
   - Copy `foodbookapp.db-shm` (if exists)
4. **Cleanup** ? Delete old files from AppDataDirectory
5. **Continue** ? Normal startup (migrations, validation, etc.)

### Subsequent Launches

1. **App starts** ? `ConditionalDeploymentAsync()` called
2. **Check** ? `NeedsMigrationFromAppData()` returns `false`
   - New DB already exists at external location
3. **Skip migration** ? Continue with normal startup

---

## Testing Strategy

### Test 1: Fresh Install
1. Install app on clean device
2. Add data (recipes, etc.)
3. **Expected:** DB created at `/storage/emulated/0/Android/data/.../files/database/`

### Test 2: Migration from Old Version
1. Install old version (DB in AppDataDirectory)
2. Add data
3. Update to new version
4. **Expected:**
   - Log shows "Detected old database in AppDataDirectory - migrating..."
   - Log shows "? Database migrated to external storage"
   - All data preserved

### Test 3: VS Deployment Survival
1. Run app from VS ? Add data
2. Close app
3. **Deploy again** from VS (triggers uninstall/reinstall)
4. Launch app
5. **Expected:** Data still present (no loss)

### Test 4: Manual Uninstall
1. Add data
2. **Manually uninstall** app via Android Settings
3. Reinstall
4. **Expected:** Data is gone (correct — this is a clean uninstall)

---

## adb Verification Commands

### Check Database Location
```bash
adb shell
cd /storage/emulated/0/Android/data/com.companyname.foodbookapp/files/database
ls -lh
```

**Expected output:**
```
foodbookapp.db
foodbookapp.db-wal  (if WAL mode active)
foodbookapp.db-shm  (if WAL mode active)
```

### Check Old Location (Should be empty after migration)
```bash
adb shell
cd /data/data/com.companyname.foodbookapp/files
ls -lh foodbookapp.db
```

**Expected:** File not found (after migration)

---

## Platform Behavior Matrix

| Platform | Database Location | Survives VS Deploy? | Survives Manual Uninstall? |
|----------|-------------------|---------------------|----------------------------|
| **Android** | `/storage/emulated/0/Android/data/<package>/files/database/` | ? YES | ? NO (correct) |
| **Windows** | `%LOCALAPPDATA%\Packages\<package>\LocalState\` | ? YES | ? NO (correct) |
| **iOS** | `/var/mobile/Containers/Data/Application/<UUID>/Library/` | ? YES | ? NO (correct) |

---

## Important Notes

### 1. No Permissions Required
`GetExternalFilesDir(null)` is part of the **app's external private storage**. It:
- Does NOT require `READ_EXTERNAL_STORAGE` permission
- Does NOT require `WRITE_EXTERNAL_STORAGE` permission
- Is only accessible by your app (private)

### 2. Backup Considerations
External files directory is **NOT automatically backed up** by Google Backup. If you want cloud backup:
- Implement custom backup logic
- Or move to `AppDataDirectory` in production (safe — Play Store doesn't uninstall)

### 3. Production Strategy

**Option A (Recommended):** Keep external storage in production
- Pros: Consistent dev/prod behavior
- Cons: No automatic Google backup

**Option B:** Use `#if DEBUG` to switch locations
```csharp
#if DEBUG
    return GetAndroidDatabasePath(); // External (survives deploy)
#else
    return Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName); // Internal (auto-backup)
#endif
```

---

## Rollback Plan

If you need to revert:

1. **Comment out the migration code** in `ConditionalDeploymentAsync`
2. **Change `GetDatabasePath()`** back to:
   ```csharp
   public static string GetDatabasePath()
   {
       return Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
   }
   ```
3. **Redeploy**

**Note:** Existing data in external storage will remain (not deleted) but app will create a fresh DB in AppDataDirectory.

---

## Logs to Monitor

### Successful Migration
```
[DatabaseConfiguration] Database path: /storage/emulated/0/Android/data/.../files/database/foodbookapp.db
[DatabaseService] Detected old database in AppDataDirectory - migrating to external storage...
[DatabaseConfiguration] Migration needed: old DB found at /data/data/.../files/foodbookapp.db
[DatabaseConfiguration] Starting migration from ...
[DatabaseConfiguration] Migrated main DB file
[DatabaseConfiguration] Migrated WAL file
[DatabaseConfiguration] Migrated SHM file
[DatabaseConfiguration] Deleted old database files from AppDataDirectory
[DatabaseConfiguration] ? Migration completed successfully
[DatabaseService] ? Database migrated to external storage (survives reinstalls)
```

### No Migration Needed
```
[DatabaseConfiguration] Database path: /storage/emulated/0/Android/data/.../files/database/foodbookapp.db
[DatabaseService] DB exists (12345 bytes, modified 2025-01-15 01:00:00) — skipping creation, applying migrations only
```

---

## FAQ

### Q: Will this work on all Android versions?
**A:** Yes. `GetExternalFilesDir()` is available since API 8. No permissions needed on Android 10+ (API 29+). On older versions (API 21-28), it still works but storage is less isolated.

### Q: What happens if external storage is unavailable?
**A:** The app throws `InvalidOperationException` with message "Android external storage not available". This is extremely rare — external storage is always available on modern Android.

### Q: Can users access this database manually?
**A:** Only with root access or via `adb shell`. Regular file managers cannot access `/storage/emulated/0/Android/data/` on Android 11+ without root.

### Q: What about multi-user Android devices?
**A:** Each user profile gets separate storage. Database is isolated per user.

---

## Related Files Modified

1. **Data/DatabaseConfiguration.cs** — Platform-specific path logic + migration
2. **Services/DatabaseService.cs** — Migration trigger in `ConditionalDeploymentAsync()`

**No changes needed:**
- EF Core context
- Migrations
- Connection strings (auto-updated via `DatabaseConfiguration.GetConnectionString()`)
- Archive logic (already uses external storage)

---

## Success Criteria

? Database survives VS deployment reinstalls
? Automatic migration from old location (one-time)
? No permissions required
? Database deleted on manual app uninstall (expected)
? All existing features (migrations, archives, restore) work unchanged
? Windows/iOS behavior unchanged (still use AppDataDirectory)
