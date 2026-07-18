using Microsoft.Maui.Storage;

namespace FoodbookApp.Services;

public sealed class MauiAdPreferencesStorage : IAdPreferencesStorage
{
    public bool GetBool(string key, bool defaultValue) => Preferences.Get(key, defaultValue);
    public void SetBool(string key, bool value) => Preferences.Set(key, value);
    public int GetInt(string key, int defaultValue) => Preferences.Get(key, defaultValue);
    public void SetInt(string key, int value) => Preferences.Set(key, value);
    public string GetString(string key, string defaultValue) => Preferences.Get(key, defaultValue);
    public void SetString(string key, string value) => Preferences.Set(key, value);
}
