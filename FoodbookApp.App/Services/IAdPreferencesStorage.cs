namespace FoodbookApp.Services;

public interface IAdPreferencesStorage
{
    bool GetBool(string key, bool defaultValue);
    void SetBool(string key, bool value);
    int GetInt(string key, int defaultValue);
    void SetInt(string key, int value);
    string GetString(string key, string defaultValue);
    void SetString(string key, string value);
}
