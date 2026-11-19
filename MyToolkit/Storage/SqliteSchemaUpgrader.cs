using System.Data;
using System.Data.Common;

namespace MyToolkit.Storage;

/// <summary>Upgrades a SQLite database in place, one versioned step at a time, so a model change never wipes the user's data. Driver-agnostic (plain ADO.NET) and platform-free.</summary>
/// <remarks>
/// <c>PRAGMA user_version</c> is the schema version. The version is read and every step runs inside one immediate transaction; SQLite keeps
/// <c>user_version</c> in the database header, so the stamp rolls back with the steps. A failed step leaves the file exactly as it was.
/// An app keeps its steps (a dictionary of target version to the SQL that gets there) and its current version, and calls <see cref="Upgrade"/>.
/// </remarks>
public static class SqliteSchemaUpgrader
{
    /// <summary>Stored version (0 for a new or unversioned file), read straight from SQLite.</summary>
    public static int ReadVersion(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    /// <summary>Applies every step between the stored version and <paramref name="targetVersion"/>. Throws on failure, after rolling back.</summary>
    /// <param name="steps">SQL that upgrades TO each version.</param>
    /// <param name="firstShippedVersion">The first version that shipped. Its shape is whatever created the file, so it has no step.</param>
    /// <param name="subject">Names the database in messages: "The {subject} database is version…".</param>
    public static void Upgrade(DbConnection connection, IReadOnlyDictionary<int, string[]> steps, int targetVersion, int firstShippedVersion = 1, string subject = "app")
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        // Read inside the transaction: the write lock is already held, so no other writer can change the version between read and stamp.
        var from = ReadVersionInside(connection, transaction);
        if (from > targetVersion)
            throw new InvalidOperationException($"The {subject} database is version {from}, newer than this app understands ({targetVersion}). It was probably written by a newer build — nothing was changed.");

        if (from == targetVersion)
            return;

        // A brand-new file (version 0) with no steps needed is simply stamped. Anything older than the first shipped version still needs its own steps.
        for (var target = Math.Max(from + 1, firstShippedVersion + 1); target <= targetVersion; target++)
        {
            if (!steps.TryGetValue(target, out var statements))
                throw new InvalidOperationException($"No upgrade step is written for {subject} version {target}. Add it to the upgrade steps before shipping this version — refusing to stamp a schema that was never changed.");

            foreach (var sql in statements)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        using (var stamp = connection.CreateCommand())
        {
            stamp.Transaction = transaction;
            // PRAGMA takes no bound parameters; the value is a version number the app wrote, never user input.
            stamp.CommandText = $"PRAGMA user_version = {targetVersion};";
            stamp.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static int ReadVersionInside(DbConnection connection, DbTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
