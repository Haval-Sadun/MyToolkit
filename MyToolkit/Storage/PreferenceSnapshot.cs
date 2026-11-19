using System.Text.Json;

namespace MyToolkit.Storage;

/// <summary>Where a <see cref="PreferenceSnapshot"/> reads and writes settings. The default is the platform's preferences (<see cref="MauiPreferenceStore"/>); tests pass an in-memory one.</summary>
public interface IPreferenceStore
{
    bool ContainsKey(string key);

    int GetInt(string key);

    bool GetBool(string key);

    string GetString(string key);

    void SetInt(string key, int value);

    void SetBool(string key, bool value);

    void SetString(string key, string value);
}

/// <summary>How a setting is stored.</summary>
public enum PreferenceKind
{
    Int,
    Bool,
    Text,
}

/// <summary>A setting a snapshot carries.</summary>
public sealed record PreferenceKey(string Key, PreferenceKind Kind);

/// <summary>Exports and imports a named list of settings as JSON, for a backup. An explicit list: one-time migration flags and runtime bookkeeping stay out, and a snapshot can never write a key that is not named here.
/// Platform-free.</summary>
public sealed class PreferenceSnapshot(IReadOnlyList<PreferenceKey> keys, IPreferenceStore store)
{
    /// <summary>The current values of every listed setting that has been set.</summary>
    public string Export()
    {
        var values = new Dictionary<string, object>();
        foreach (var (key, kind) in keys)
        {
            if (!store.ContainsKey(key))
                continue;

            values[key] = kind switch
            {
                PreferenceKind.Int => store.GetInt(key),
                PreferenceKind.Bool => store.GetBool(key),
                _ => store.GetString(key),
            };
        }

        return JsonSerializer.Serialize(values);
    }

    /// <summary>Writes the listed settings found in <paramref name="json"/>; unknown keys and values of the wrong type are skipped.</summary>
    public void Import(string json)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return;

        foreach (var (key, kind) in keys)
        {
            if (!root.TryGetProperty(key, out var value))
                continue;

            switch (kind)
            {
                case PreferenceKind.Int when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number):
                    store.SetInt(key, number);
                    break;
                case PreferenceKind.Bool when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    store.SetBool(key, value.GetBoolean());
                    break;
                case PreferenceKind.Text when value.ValueKind == JsonValueKind.String:
                    store.SetString(key, value.GetString() ?? string.Empty);
                    break;
            }
        }
    }
}
