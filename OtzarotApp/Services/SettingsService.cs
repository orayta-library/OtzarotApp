using System.Text.Json;
using Windows.Storage;

namespace OtzarotApp.Services;

/// <summary>
/// שירות לשמירה וטעינת הגדרות המשתמש.
/// מאחסן הגדרות ב-LocalSettings של Windows.
/// </summary>
public class SettingsService
{
    private readonly ApplicationDataContainer _localSettings;

    // מפתחות הגדרות
    private const string KeyDbPath       = "DbPath";
    private const string KeyFontFamily   = "FontFamily";
    private const string KeyFontSize     = "FontSize";
    private const string KeyTantivyPath  = "TantivyPath";
    private const string KeyIndexPath    = "IndexPath";
    private const string KeyTheme        = "Theme";

    public SettingsService()
    {
        _localSettings = ApplicationData.Current.LocalSettings;
    }

    // ─── נתיבים ──────────────────────────────────────────────
    public string DbPath
    {
        get => Get(KeyDbPath, string.Empty);
        set => Set(KeyDbPath, value);
    }

    public string TantivyPath
    {
        get => Get(KeyTantivyPath, string.Empty);
        set => Set(KeyTantivyPath, value);
    }

    public string IndexPath
    {
        get => Get(KeyIndexPath, string.Empty);
        set => Set(KeyIndexPath, value);
    }

    // ─── גופן ────────────────────────────────────────────────
    /// <summary>גופן ברירת מחדל: Adama אם מותקן, אחרת David</summary>
    public string FontFamily
    {
        get => Get(KeyFontFamily, "David");
        set => Set(KeyFontFamily, value);
    }

    public double FontSize
    {
        get => Get(KeyFontSize, 18.0);
        set => Set(KeyFontSize, value);
    }

    // ─── ערכת נושא ──────────────────────────────────────────
    /// <summary>Default / Light / Dark</summary>
    public string Theme
    {
        get => Get(KeyTheme, "Default");
        set => Set(KeyTheme, value);
    }

    // ─── helpers ────────────────────────────────────────────
    private T Get<T>(string key, T defaultValue)
    {
        if (_localSettings.Values.TryGetValue(key, out var raw) && raw is T typed)
            return typed;
        if (raw is string s && typeof(T) == typeof(double) &&
            double.TryParse(s, out var d))
            return (T)(object)d;
        return defaultValue;
    }

    private void Set<T>(string key, T value)
    {
        _localSettings.Values[key] = value;
    }
}
