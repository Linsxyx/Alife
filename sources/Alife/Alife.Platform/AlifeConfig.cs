using System.Globalization;
using Microsoft.Win32;

namespace Alife.Platform;

public static class AlifeConfig
{
    const string RegistryPath = @"Software\Alife";

    static Dictionary<string, string> data = new();

    public static void Initialize()
    {
        Load();
    }

    public static string GetString(string key, string defaultValue = "")
    {
        if (data.TryGetValue(key, out string? value))
            return value;
        return defaultValue;
    }

    public static void SetString(string key, string value)
    {
        data[key] = value;
        Save();
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        if (int.TryParse(GetString(key), out int result))
            return result;
        return defaultValue;
    }

    public static void SetInt(string key, int value)
    {
        SetString(key, value.ToString());
    }

    public static float GetFloat(string key, float defaultValue = 0f)
    {
        if (float.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            return result;
        return defaultValue;
    }

    public static void SetFloat(string key, float value)
    {
        SetString(key, value.ToString(CultureInfo.InvariantCulture));
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
        string value = GetString(key);
        if (bool.TryParse(value, out bool result))
            return result;
        if (value == "1")
            return true;
        if (value == "0")
            return false;
        return defaultValue;
    }

    public static void SetBool(string key, bool value)
    {
        SetString(key, value.ToString());
    }

    public static bool HasKey(string key)
    {
        return data.ContainsKey(key);
    }

    public static void Remove(string key)
    {
        if (data.Remove(key))
            Save();
    }

    public static void Clear()
    {
        data.Clear();
        Save();
    }

    static void Load()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key is not null)
            {
                data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string valueName in key.GetValueNames())
                {
                    object? value = key.GetValue(valueName);
                    if (value is string s)
                        data[valueName] = s;
                }
            }
            else
            {
                data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    static void Save()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            if (key is null) return;

            foreach (var kvp in data)
                key.SetValue(kvp.Key, kvp.Value, RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            AlifeTerminal.LogError($"AlifeConfig save failed: {ex.Message}");
        }
    }
}
