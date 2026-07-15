using Microsoft.Data.Sqlite;
using OtzarotApp.Models;

namespace OtzarotApp.Services;

/// <summary>
/// שירות לגישה למסד נתונים SQLite של אוצריא (seforim.db).
/// כל הפעולות synchronous (SQLite מהיר מספיק לקריאה).
/// </summary>
public class DatabaseService : IDisposable
{
    private SqliteConnection? _conn;
    private readonly SettingsService _settings;
    private bool _disposed;

    public bool IsOpen => _conn is not null;

    public DatabaseService(SettingsService settings)
    {
        _settings = settings;
    }

    // ─── פתיחה / סגירה ──────────────────────────────────────
    public bool TryOpen(string? path = null)
    {
        var dbPath = path ?? _settings.DbPath;
        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            return false;

        try
        {
            Close();
            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            _conn = new SqliteConnection(connStr);
            _conn.Open();

            if (!string.IsNullOrEmpty(path))
                _settings.DbPath = path;

            return true;
        }
        catch
        {
            _conn = null;
            return false;
        }
    }

    public void Close()
    {
        _conn?.Close();
        _conn?.Dispose();
        _conn = null;
    }

    // ─── קטגוריות ────────────────────────────────────────────
    public List<Category> GetRootCategories()
    {
        return QueryCategories(
            "SELECT id, title, parentId, orderIndex FROM category " +
            "WHERE parentId IS NULL ORDER BY orderIndex, title");
    }

    public List<Category> GetSubCategories(int parentId)
    {
        return QueryCategories(
            "SELECT id, title, parentId, orderIndex FROM category " +
            "WHERE parentId = @p ORDER BY orderIndex, title",
            ("@p", parentId));
    }

    private List<Category> QueryCategories(string sql, params (string, object)[] parms)
    {
        var list = new List<Category>();
        if (_conn is null) return list;

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v);

        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Category
            {
                Id = r.GetInt32(0),
                Title = r.GetString(1).Trim(),
                ParentId = r.IsDBNull(2) ? null : r.GetInt32(2),
                OrderIndex = r.GetInt32(3)
            });
        return list;
    }

    // ─── ספרים ───────────────────────────────────────────────
    public List<Book> GetBooksInCategory(int categoryId)
    {
        var list = new List<Book>();
        if (_conn is null) return list;

        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, title, heShortDesc, totalLines, hasNekudot, hasTeamim, volume, categoryId " +
            "FROM book WHERE categoryId = @c ORDER BY orderIndex, title";
        cmd.Parameters.AddWithValue("@c", categoryId);

        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(ReadBook(r));
        return list;
    }

    public Book? GetBook(int bookId)
    {
        if (_conn is null) return null;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, title, heShortDesc, totalLines, hasNekudot, hasTeamim, volume, categoryId " +
            "FROM book WHERE id = @b";
        cmd.Parameters.AddWithValue("@b", bookId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadBook(r) : null;
    }

    public List<Book> SearchBooks(string query, int limit = 100)
    {
        var list = new List<Book>();
        if (_conn is null || string.IsNullOrWhiteSpace(query)) return list;

        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT b.id, b.title, b.heShortDesc, b.totalLines, b.hasNekudot, b.hasTeamim, b.volume, b.categoryId " +
            "FROM book b " +
            "WHERE b.title LIKE @q " +
            "ORDER BY b.title LIMIT @lim";
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        cmd.Parameters.AddWithValue("@lim", limit);

        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(ReadBook(r));
        return list;
    }

    private static Book ReadBook(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Title = r.GetString(1).Trim(),
        HeShortDesc = r.IsDBNull(2) ? null : r.GetString(2).Trim(),
        TotalLines = r.GetInt32(3),
        HasNekudot = r.GetInt32(4) != 0,
        HasTeamim = r.GetInt32(5) != 0,
        Volume = r.IsDBNull(6) ? null : r.GetString(6).Trim(),
        CategoryId = r.GetInt32(7)
    };

    // ─── שורות טקסט ─────────────────────────────────────────
    public List<BookLine> GetBookLines(int bookId, int startLine = 0, int limit = 200)
    {
        var list = new List<BookLine>();
        if (_conn is null) return list;

        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, bookId, lineIndex, content, heRef " +
            "FROM line WHERE bookId = @b ORDER BY lineIndex LIMIT @lim OFFSET @off";
        cmd.Parameters.AddWithValue("@b", bookId);
        cmd.Parameters.AddWithValue("@lim", limit);
        cmd.Parameters.AddWithValue("@off", startLine);

        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new BookLine
            {
                Id = r.GetInt32(0),
                BookId = r.GetInt32(1),
                LineIndex = r.GetInt32(2),
                Content = r.IsDBNull(3) ? string.Empty : r.GetString(3),
                HeRef = r.IsDBNull(4) ? null : r.GetString(4)
            });
        return list;
    }

    public int GetBookLineCount(int bookId)
    {
        if (_conn is null) return 0;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM line WHERE bookId = @b";
        cmd.Parameters.AddWithValue("@b", bookId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ─── תוכן עניינים ────────────────────────────────────────
    public List<TocEntry> GetBookToc(int bookId)
    {
        var flat = new List<TocEntry>();
        if (_conn is null) return flat;

        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT te.id, te.bookId, te.parentId, tt.text, te.lineIndex, te.level " +
            "FROM tocEntry te JOIN tocText tt ON te.textId = tt.id " +
            "WHERE te.bookId = @b ORDER BY te.lineIndex";
        cmd.Parameters.AddWithValue("@b", bookId);

        using var r = cmd.ExecuteReader();
        while (r.Read())
            flat.Add(new TocEntry
            {
                Id = r.GetInt32(0),
                BookId = r.GetInt32(1),
                ParentId = r.IsDBNull(2) ? null : r.GetInt32(2),
                Title = r.GetString(3).Trim(),
                LineIndex = r.GetInt32(4),
                Level = r.GetInt32(5)
            });

        return BuildTocTree(flat);
    }

    private static List<TocEntry> BuildTocTree(List<TocEntry> flat)
    {
        var map = flat.ToDictionary(e => e.Id);
        var roots = new List<TocEntry>();
        foreach (var e in flat)
        {
            if (e.ParentId is null || !map.ContainsKey(e.ParentId.Value))
                roots.Add(e);
            else
                map[e.ParentId.Value].Children.Add(e);
        }
        return roots;
    }

    // ─── מפרשים ──────────────────────────────────────────────
    public List<Commentary> GetBookCommentaries(int bookId)
    {
        var list = new List<Commentary>();
        if (_conn is null) return list;

        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            @"SELECT l.targetBookId, b.title, b.heShortDesc, b.volume,
                     ct.name, l.connectionTypeId
              FROM link l
              JOIN book b ON l.targetBookId = b.id
              JOIN connection_type ct ON l.connectionTypeId = ct.id
              WHERE l.sourceBookId = @s
              GROUP BY l.targetBookId, l.connectionTypeId
              ORDER BY ct.id, b.title";
        cmd.Parameters.AddWithValue("@s", bookId);

        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Commentary
            {
                SourceBookId = bookId,
                TargetBookId = r.GetInt32(0),
                BookTitle = r.GetString(1).Trim(),
                HeShortDesc = r.IsDBNull(2) ? null : r.GetString(2).Trim(),
                Volume = r.IsDBNull(3) ? null : r.GetString(3).Trim(),
                LinkType = r.GetString(4).Trim(),
                LinkTypeId = r.GetInt32(5)
            });
        return list;
    }

    public List<Commentary> GetLineCommentaries(int bookId, int lineIndex)
    {
        var list = new List<Commentary>();
        if (_conn is null) return list;

        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            @"SELECT l.targetBookId, b.title, b.heShortDesc, b.volume,
                     ct.name, l.connectionTypeId,
                     tl.lineIndex as targetLine
              FROM link l
              JOIN book b ON l.targetBookId = b.id
              JOIN connection_type ct ON l.connectionTypeId = ct.id
              JOIN line sl ON l.sourceLineId = sl.id
              JOIN line tl ON l.targetLineId = tl.id
              WHERE l.sourceBookId = @s AND sl.lineIndex = @li
              ORDER BY ct.id";
        cmd.Parameters.AddWithValue("@s", bookId);
        cmd.Parameters.AddWithValue("@li", lineIndex);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var c = new Commentary
            {
                SourceBookId = bookId,
                TargetBookId = r.GetInt32(0),
                BookTitle = r.GetString(1).Trim(),
                HeShortDesc = r.IsDBNull(2) ? null : r.GetString(2).Trim(),
                Volume = r.IsDBNull(3) ? null : r.GetString(3).Trim(),
                LinkType = r.GetString(4).Trim(),
                LinkTypeId = r.GetInt32(5),
                TargetLineIndex = r.IsDBNull(6) ? null : r.GetInt32(6)
            };
            list.Add(c);
        }
        return list;
    }

    // ─── IDisposable ─────────────────────────────────────────
    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
