using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using OtzarotApp.Models;

namespace OtzarotApp.Services;

/// <summary>
/// שירות לתקשורת עם מנוע החיפוש tantivy_search.exe.
/// מפעיל את ה-EXE כ-HTTP server בפנים ושולח בקשות JSON.
/// </summary>
public class TantivyService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly HttpClient _http;
    private Process? _process;
    private bool _disposed;
    private bool _ready;

    private const int    ServerPort    = 7777;
    private const string ServerBase    = $"http://127.0.0.1:{ServerPort}";
    private const string IndexName     = "otzaria";

    public bool IsReady => _ready;

    public TantivyService(SettingsService settings)
    {
        _settings = settings;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    // ─── הפעלת השרת ─────────────────────────────────────────
    public async Task<bool> StartServerAsync()
    {
        if (_ready) return true;

        var exe = _settings.TantivyPath;
        var idx = _settings.IndexPath;

        if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;
        if (string.IsNullOrEmpty(idx) || !Directory.Exists(idx))  return false;

        try
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"serve --index \"{idx}\" --name {IndexName} --port {ServerPort}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            _process.Start();

            // המתן עד שהשרת מוכן (עד 10 שניות)
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(500);
                try
                {
                    var ping = await _http.GetAsync($"{ServerBase}/health");
                    if (ping.IsSuccessStatusCode) { _ready = true; return true; }
                }
                catch { /* ממשיכים להמתין */ }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void StopServer()
    {
        _ready = false;
        try { _process?.Kill(entireProcessTree: true); } catch { }
        _process?.Dispose();
        _process = null;
    }

    // ─── יצירת אינדקס ────────────────────────────────────────
    public async Task<bool> BuildIndexAsync(string dbPath, string indexPath,
                                            IProgress<string>? progress = null)
    {
        var exe = _settings.TantivyPath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;

        try
        {
            StopServer();
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"index --source sqlite --db \"{dbPath}\" --table line --output \"{indexPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) progress?.Report(e.Data); };
            p.ErrorDataReceived  += (_, e) => { if (e.Data != null) progress?.Report(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            if (p.ExitCode == 0)
            {
                _settings.IndexPath = indexPath;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    // ─── חיפוש ──────────────────────────────────────────────
    public async Task<SearchResponse> SearchAsync(SearchParameters parms)
    {
        if (!_ready)
            return new SearchResponse { Error = "שרת החיפוש אינו פעיל" };

        try
        {
            // הרחבת שאילתא עברית בצד ה-C#
            var query = parms.Query;
            var body = new
            {
                index   = IndexName,
                query,
                fields  = new[] { "content", "heRef", "title" },
                limit   = parms.Limit,
                offset  = parms.Offset,
                fuzzy   = parms.Fuzzy,
                fuzzy_distance = parms.FuzzyDistance,
                conjunction = parms.Conjunction
            };

            var json    = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp    = await _http.PostAsync($"{ServerBase}/search", content);
            resp.EnsureSuccessStatusCode();

            var resultJson = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SearchResponse>(resultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result ?? new SearchResponse();
        }
        catch (Exception ex)
        {
            return new SearchResponse { Error = ex.Message };
        }
    }

    /// <summary>חיפוש מהיר לכותרות ספרים (השלמה אוטומטית)</summary>
    public async Task<List<BookSuggestion>> SuggestAsync(string query, int limit = 10)
    {
        if (!_ready || string.IsNullOrWhiteSpace(query))
            return [];

        var parms = new SearchParameters
        {
            Query = query,
            Limit = limit,
            Fuzzy = true,
            FuzzyDistance = 1
        };

        // חיפוש בשדה כותרת בלבד
        try
        {
            var body = new
            {
                index = IndexName,
                query,
                fields = new[] { "title", "heRef" },
                limit,
                fuzzy = true,
                fuzzy_distance = 1,
                summary_only = true
            };
            var json    = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp    = await _http.PostAsync($"{ServerBase}/search", content);
            resp.EnsureSuccessStatusCode();

            var resultJson = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SearchResponse>(resultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Hits is null) return [];

            // קבץ לפי ספר — מחזיר הצעה לכל ספר ייחודי
            return result.Hits
                .GroupBy(h => h.BookId)
                .Select(g =>
                {
                    var first = g.First();
                    return new BookSuggestion
                    {
                        BookId    = first.BookId,
                        Title     = first.Title,
                        HeRef     = first.HeRef,
                        LineIndex = first.LineIndex
                    };
                })
                .Take(limit)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    // ─── IDisposable ─────────────────────────────────────────
    public void Dispose()
    {
        if (!_disposed)
        {
            StopServer();
            _http.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>הצעת ספר להשלמה אוטומטית</summary>
public record BookSuggestion
{
    public int    BookId    { get; init; }
    public string Title     { get; init; } = string.Empty;
    public string HeRef     { get; init; } = string.Empty;
    public int    LineIndex { get; init; }
}
