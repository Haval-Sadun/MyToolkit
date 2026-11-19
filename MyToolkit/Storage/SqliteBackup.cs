using System.Data.Common;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace MyToolkit.Storage;

/// <summary>What a backup says about itself.</summary>
/// <param name="Format">Layout of the zip; bumped if the entries ever change.</param>
/// <param name="SchemaVersion">The schema version the database inside was saved at.</param>
public sealed record BackupManifest(int Format, int SchemaVersion, string AppVersion, DateTimeOffset CreatedAt);

/// <summary>A checked backup: its manifest and the settings it carries.</summary>
public sealed record BackupContents(BackupManifest Manifest, string PrefsJson);

/// <summary>The resource keys an <see cref="InvalidBackupException"/> carries; an app translates them (the exception's own message is English).</summary>
public static class BackupErrorKeys
{
    public const string NotBackup = "Backup_Err_NotBackup";
    public const string Format = "Backup_Err_Format";
    public const string Newer = "Backup_Err_Newer";
    public const string NoDatabase = "Backup_Err_NoLibrary";
    public const string Mismatch = "Backup_Err_Mismatch";
    public const string Damaged = "Backup_Err_Damaged";
    public const string TooLarge = "Backup_Err_TooLarge";
    public const string Old = "Backup_Err_Old";
}

/// <summary>The file is not a backup this app can use; the message is English and safe to show, <see cref="ResourceKey"/> names its translation (see <see cref="BackupErrorKeys"/>).</summary>
public sealed class InvalidBackupException(string message, string resourceKey) : Exception(message)
{
    public string ResourceKey { get; } = resourceKey;
}

/// <summary>What <see cref="SqliteBackup"/> needs from the app. The SQLite driver is the app's: it hands out connections, so this library depends on none.</summary>
/// <param name="OpenConnection">An unopened connection to the file at the path; the flag asks for read-only. Never pooled, so files can be moved right after it is disposed.</param>
/// <param name="CurrentSchemaVersion">The schema version this build understands; a backup from a newer one is refused.</param>
/// <param name="Upgrade">Brings an open read-write connection to <see cref="CurrentSchemaVersion"/> (an app's <see cref="SqliteSchemaUpgrader"/> call).</param>
/// <param name="IsBusy">Whether a database exception means "locked by another process" (SQLite error 5 or 6).</param>
/// <param name="ClearPools">Closes pooled connections, so the file can be swapped.</param>
/// <param name="AppName">Names the app in the English messages of <see cref="InvalidBackupException"/>.</param>
/// <param name="DatabaseEntry">Entry names inside the zip. They stay as an app first shipped them, so older backups keep restoring.</param>
public sealed record SqliteBackupOptions(
    Func<string, bool, DbConnection> OpenConnection,
    int CurrentSchemaVersion,
    Action<DbConnection> Upgrade,
    Func<DbException, bool> IsBusy,
    Action ClearPools,
    string AppName = "the app",
    string DatabaseEntry = "database.sqlite3",
    string PrefsEntry = "prefs.json",
    string ManifestEntry = "manifest.json",
    int FormatVersion = 1);

/// <summary>A backup of a SQLite database is one zip: a consistent snapshot of the database, the settings, and a manifest. Restoring stages the database next to the live one and swaps it in at the next launch, before anything has the file open.
/// Platform-free, and driver-agnostic (plain ADO.NET).</summary>
/// <remarks>The safety rules live here so every app gets them: the snapshot is <c>VACUUM INTO</c> (consistent while the app runs); the swap folds the write-ahead log into the file first and replaces the file in one step, retrying while another process closes it,
/// and keeps the replaced database as <c>.pre-restore.bak</c>; a zip is read with size caps; a cancelled import never drops a restore already confirmed.</remarks>
public sealed class SqliteBackup(SqliteBackupOptions options)
{
    /// <summary>Settings and manifest are a few hundred bytes; anything near this is not a backup.</summary>
    public const long MaxJsonBytes = 1024 * 1024;

    /// <summary>A database is megabytes; this stops a zip that inflates to fill the disk.</summary>
    public const long MaxDatabaseBytes = 4L * 1024 * 1024 * 1024;

    private const string StagedSuffix = ".restore";
    private const string PreRestoreSuffix = ".pre-restore.bak";
    private const int SwapAttempts = 10;
    private const int SwapDelayMilliseconds = 300;

    /// <summary>The database file a committed restore waits in.</summary>
    public static string StagedPath(string databasePath) => databasePath + StagedSuffix;

    private static string CheckedPath(string databasePath) => databasePath + StagedSuffix + ".checked";

    private static string StagedPrefsPath(string databasePath) => databasePath + StagedSuffix + ".prefs.json";

    private static string TempPath(string databasePath) => databasePath + StagedSuffix + ".tmp";

    /// <summary>The checked copy of a prepared backup — what a merge reads.</summary>
    public static string PreparedPath(string databasePath) => CheckedPath(databasePath);

    /// <summary>True when a restore has left the database it replaced next to the live one.</summary>
    public static bool HasPreviousDatabase(string databasePath) => File.Exists(databasePath + PreRestoreSuffix);

    /// <summary>Queues the database a restore replaced to come back at the next launch. The swap keeps the current one as the new .pre-restore.bak, so it can be swapped back again. False when there is none.</summary>
    public static bool StagePreviousDatabase(string databasePath)
    {
        var previous = databasePath + PreRestoreSuffix;
        if (!File.Exists(previous))
            return false;

        // Copied beside first and moved into place: a copy cut short must never look like a staged database.
        var temp = TempPath(databasePath);
        try
        {
            File.Copy(previous, temp, overwrite: true);
            File.Move(temp, StagedPath(databasePath), overwrite: true);
        }
        catch
        {
            DeleteQuietly(temp);
            throw;
        }

        // Settings staged by an earlier restore belong to that backup, not to this swap.
        DeleteQuietly(StagedPrefsPath(databasePath));
        return true;
    }

    /// <summary>Writes the backup zip. The database is copied with VACUUM INTO on its own connection, so the snapshot is consistent while the app keeps running.</summary>
    public BackupManifest Create(string databasePath, string prefsJson, string appVersion, string zipPath)
    {
        var snapshot = Path.Combine(Path.GetTempPath(), $"zt-backup-{Guid.NewGuid():N}.sqlite3");
        try
        {
            // Create mode refuses an existing file.
            DeleteQuietly(zipPath);

            int schemaVersion;
            using (var connection = options.OpenConnection(databasePath, true))
            {
                connection.Open();
                schemaVersion = SqliteSchemaUpgrader.ReadVersion(connection);
                using var command = connection.CreateCommand();
                command.CommandText = "VACUUM INTO $path;";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "$path";
                parameter.Value = snapshot;
                command.Parameters.Add(parameter);
                command.ExecuteNonQuery();
            }

            var manifest = new BackupManifest(options.FormatVersion, schemaVersion, appVersion, DateTimeOffset.UtcNow);
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(snapshot, options.DatabaseEntry, CompressionLevel.Optimal);
            WriteText(archive, options.PrefsEntry, prefsJson);
            WriteText(archive, options.ManifestEntry, JsonSerializer.Serialize(manifest));
            return manifest;
        }
        finally
        {
            DeleteQuietly(snapshot);
        }
    }

    /// <summary>Checks the zip and, if it is sound, parks its database as a checked copy. Nothing live is touched and nothing restores yet. Throws <see cref="InvalidBackupException"/> otherwise.</summary>
    public BackupContents Prepare(string zipPath, string databasePath)
    {
        var checkedPath = CheckedPath(databasePath);
        try
        {
            return ExtractAndCheck(zipPath, checkedPath);
        }
        catch
        {
            DeleteQuietly(checkedPath);
            throw;
        }
    }

    /// <summary>Brings the checked copy up to the current schema, so a merge can read it with the current model. It is a private copy, so changing it harms nothing.</summary>
    public void UpgradePrepared(string databasePath)
    {
        try
        {
            using var connection = options.OpenConnection(CheckedPath(databasePath), false);
            connection.Open();
            if (SqliteSchemaUpgrader.ReadVersion(connection) < options.CurrentSchemaVersion)
                options.Upgrade(connection);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            throw new InvalidBackupException($"This backup was made by an older version of {options.AppName} and could not be brought up to date.", BackupErrorKeys.Old);
        }
    }

    /// <summary>Marks the checked copy as the restore to apply at the next launch, with the settings that go with it (applied only if the swap happens). Only after the user agreed — a killed app must never restore by surprise.</summary>
    public static void Commit(string databasePath, string? prefsJson = null)
    {
        if (prefsJson is not null)
        {
            var temp = TempPath(databasePath);
            File.WriteAllText(temp, prefsJson);
            File.Move(temp, StagedPrefsPath(databasePath), overwrite: true);
        }
        else
        {
            DeleteQuietly(StagedPrefsPath(databasePath));
        }

        File.Move(CheckedPath(databasePath), StagedPath(databasePath), overwrite: true);
    }

    /// <summary>The settings a committed restore carries, once; null when there are none. Read only after the swap succeeded.</summary>
    public static string? TakeStagedPrefs(string databasePath)
    {
        var path = StagedPrefsPath(databasePath);
        if (!File.Exists(path))
            return null;

        try
        {
            return File.ReadAllText(path);
        }
        finally
        {
            DeleteQuietly(path);
        }
    }

    /// <summary>Drops only the checked copy of an import that was cancelled or merged. A restore already committed, or a previous database already queued, stays.</summary>
    public static void DiscardPrepared(string databasePath) => DeleteQuietly(CheckedPath(databasePath));

    /// <summary>Drops a prepared and a committed restore alike.</summary>
    public static void Discard(string databasePath)
    {
        DeleteQuietly(CheckedPath(databasePath));
        DeleteQuietly(StagedPath(databasePath));
        DeleteQuietly(StagedPrefsPath(databasePath));
    }

    /// <summary>Swaps a staged database in, keeping the one it replaces as a .pre-restore.bak. Call before any connection to the database opens. True when a swap happened.
    /// Throws <see cref="IOException"/> with the reason when the database could not be swapped; the current one is then untouched.</summary>
    /// <param name="log">Receives a plain-language line for each step that needed a retry or a workaround.</param>
    public bool ApplyStaged(string databasePath, Action<string>? log = null)
    {
        var staged = StagedPath(databasePath);
        if (!File.Exists(staged))
            return false;

        options.ClearPools();
        var hasLive = File.Exists(databasePath);
        if (hasLive)
            Checkpoint(databasePath, log);

        // Safe now: the changes they held are in the main file (or the file cannot be read at all).
        foreach (var leftover in new[] { "-journal", "-wal", "-shm" })
            DeleteQuietly(databasePath + leftover);

        WithRetry(
            () =>
            {
                // One replace: the live file becomes the .pre-restore.bak and the staged one takes its place, so a failure never leaves no database at all.
                if (hasLive)
                    File.Replace(staged, databasePath, databasePath + PreRestoreSuffix);
                else
                    File.Move(staged, databasePath);
            },
            log);
        return true;
    }

    // Folds the write-ahead log into the main file first: the .pre-restore.bak is only a faithful copy if nothing is still waiting in the log.
    private void Checkpoint(string databasePath, Action<string>? log)
    {
        try
        {
            using var connection = options.OpenConnection(databasePath, false);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            using var reader = command.ExecuteReader();
            if (reader.Read() && reader.GetInt32(0) != 0)
                throw new IOException("The current database is still in use by another process, so its latest changes could not be saved into the copy that is kept. The restore was not applied and your data is unchanged. Close any other window of the app and try again.");
        }
        catch (DbException ex) when (options.IsBusy(ex))
        {
            throw new IOException($"The current database is locked by another process ({ex.GetType().Name}: {ex.Message}), so its latest changes could not be saved into the copy that is kept. The restore was not applied and your data is unchanged. Close any other window of the app and try again.", ex);
        }
        catch (DbException ex)
        {
            // A database that cannot be read is what a restore is for; there is nothing more to save from it.
            log?.Invoke($"The current database could not be read to save its latest changes before replacing it ({ex.GetType().Name}: {ex.Message}). Continuing with the restore.");
        }
    }

    // Windows opens files without delete sharing: for a moment after a restart the old process may still hold the database.
    private static void WithRetry(Action action, Action<string>? log)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < SwapAttempts)
            {
                log?.Invoke($"Swapping the database failed on try {attempt} of {SwapAttempts} ({ex.GetType().Name}: {ex.Message}). The previous process may still be closing the file; trying again.");
                Thread.Sleep(SwapDelayMilliseconds);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new IOException($"The database could not be swapped after {SwapAttempts} tries ({ex.GetType().Name}: {ex.Message}). Something is still holding the file, such as another window of the app, a backup program or an antivirus scan. Your data is unchanged and the restore will be tried again at the next launch.", ex);
            }
        }
    }

    private BackupContents ExtractAndCheck(string zipPath, string databaseTarget)
    {
        ZipArchive archive;
        try
        {
            archive = ZipFile.OpenRead(zipPath);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            throw new InvalidBackupException($"This isn't a backup file of {options.AppName}.", BackupErrorKeys.NotBackup);
        }

        using (archive)
        {
            var manifest = ReadManifest(archive);
            if (manifest.Format != options.FormatVersion)
                throw new InvalidBackupException($"This backup was made by a different version of {options.AppName}.", BackupErrorKeys.Format);

            if (manifest.SchemaVersion > options.CurrentSchemaVersion)
                throw new InvalidBackupException($"This backup was made by a newer version of {options.AppName}. Update the app first.", BackupErrorKeys.Newer);

            var database = archive.GetEntry(options.DatabaseEntry) ?? throw new InvalidBackupException("This backup has no database in it.", BackupErrorKeys.NoDatabase);
            ExtractBounded(database, databaseTarget);
            CheckDatabase(databaseTarget, manifest.SchemaVersion);

            var prefs = archive.GetEntry(options.PrefsEntry);
            return new BackupContents(manifest, prefs is null ? "{}" : ReadText(prefs));
        }
    }

    private InvalidBackupException TooLarge() => new($"This file is too large to be a backup of {options.AppName}.", BackupErrorKeys.TooLarge);

    // The declared size can lie in a crafted zip, so the bytes are counted as they come out too.
    private void ExtractBounded(ZipArchiveEntry entry, string target)
    {
        if (entry.Length > MaxDatabaseBytes)
            throw TooLarge();

        using var input = entry.Open();
        using var output = File.Create(target);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > MaxDatabaseBytes)
                throw TooLarge();

            output.Write(buffer, 0, read);
        }
    }

    private BackupManifest ReadManifest(ZipArchive archive)
    {
        var entry = archive.GetEntry(options.ManifestEntry) ?? throw new InvalidBackupException($"This isn't a backup file of {options.AppName}.", BackupErrorKeys.NotBackup);
        try
        {
            return JsonSerializer.Deserialize<BackupManifest>(ReadText(entry)) ?? throw new InvalidBackupException($"This isn't a backup file of {options.AppName}.", BackupErrorKeys.NotBackup);
        }
        catch (JsonException)
        {
            throw new InvalidBackupException($"This isn't a backup file of {options.AppName}.", BackupErrorKeys.NotBackup);
        }
    }

    private void CheckDatabase(string path, int expectedVersion)
    {
        try
        {
            using var connection = options.OpenConnection(path, true);
            connection.Open();
            if (SqliteSchemaUpgrader.ReadVersion(connection) != expectedVersion)
                throw new InvalidBackupException("This backup's database doesn't match its description.", BackupErrorKeys.Mismatch);

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            if (command.ExecuteScalar() as string != "ok")
                throw new InvalidBackupException("This backup's database is damaged.", BackupErrorKeys.Damaged);
        }
        catch (DbException)
        {
            throw new InvalidBackupException("This backup's database is damaged.", BackupErrorKeys.Damaged);
        }
    }

    private static void WriteText(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name, CompressionLevel.Optimal).Open());
        writer.Write(text);
    }

    private string ReadText(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxJsonBytes)
            throw TooLarge();

        using var input = entry.Open();
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (memory.Length + read > MaxJsonBytes)
                throw TooLarge();

            memory.Write(buffer, 0, read);
        }

        // A byte-order mark (a file edited elsewhere) would break the JSON parser.
        var bytes = memory.GetBuffer();
        var start = memory.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        return Encoding.UTF8.GetString(bytes, start, (int)memory.Length - start);
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover temp file is harmless.
        }
    }
}
