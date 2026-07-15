using System.Runtime.InteropServices;
using Microsoft.UI.Text;

namespace OtzarotApp.Services;

/// <summary>
/// שירות לקבלת רשימת גופנים מותקנים במחשב.
/// משתמש ב-DirectWrite דרך P/Invoke לקבל את כל הגופנים.
/// </summary>
public class FontService
{
    private List<string>? _cachedFonts;

    /// <summary>מחזיר את כל הגופנים המותקנים, ממוינים לפי שם</summary>
    public IReadOnlyList<string> GetInstalledFonts()
    {
        if (_cachedFonts is not null)
            return _cachedFonts;

        _cachedFonts = GetFontsFromRegistry();
        return _cachedFonts;
    }

    /// <summary>בדיקה אם גופן מסוים מותקן</summary>
    public bool IsFontInstalled(string fontName)
        => GetInstalledFonts().Any(f =>
            f.Equals(fontName, StringComparison.OrdinalIgnoreCase));

    /// <summary>גופן ברירת מחדל: Adama אם קיים, אחרת David</summary>
    public string DefaultFontFamily =>
        IsFontInstalled("Adama")        ? "Adama"           :
        IsFontInstalled("David")        ? "David"           :
        IsFontInstalled("Frank Ruehl CLM") ? "Frank Ruehl CLM" :
        "Times New Roman";

    // ─── קריאת גופנים מ-Registry ─────────────────────────────
    private static List<string> GetFontsFromRegistry()
    {
        var fonts = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // קרא גופנים מ-Windows Registry
            using var key = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");

            if (key is not null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    // שם הרשומה הוא "FontName (TrueType)" — נחתוך את הסוגריים
                    var name = valueName;
                    var idx  = name.IndexOf('(');
                    if (idx > 0) name = name[..idx].Trim();
                    if (!string.IsNullOrEmpty(name))
                        fonts.Add(name);
                }
            }
        }
        catch
        {
            // fallback
        }

        // הוסף גופנים עבריים נפוצים שאולי לא ב-registry
        foreach (var f in new[] { "David", "Frank Ruehl CLM", "Narkisim",
                                   "Miriam", "Arial", "Calibri",
                                   "Segoe UI", "Times New Roman", "Tahoma" })
            fonts.Add(f);

        return [.. fonts];
    }
}
