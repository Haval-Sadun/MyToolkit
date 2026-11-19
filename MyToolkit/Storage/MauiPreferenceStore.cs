using Microsoft.Maui.Storage;

namespace MyToolkit.Storage;

/// <summary>The platform's own preferences (<see cref="Preferences"/>) as a store for <see cref="PreferenceSnapshot"/>.</summary>
public sealed class MauiPreferenceStore : IPreferenceStore
{
    public bool ContainsKey(string key) => Preferences.Default.ContainsKey(key);

    public int GetInt(string key) => Preferences.Default.Get(key, 0);

    public bool GetBool(string key) => Preferences.Default.Get(key, false);

    public string GetString(string key) => Preferences.Default.Get(key, string.Empty);

    public void SetInt(string key, int value) => Preferences.Default.Set(key, value);

    public void SetBool(string key, bool value) => Preferences.Default.Set(key, value);

    public void SetString(string key, string value) => Preferences.Default.Set(key, value);
}
