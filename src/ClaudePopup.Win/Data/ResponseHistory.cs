using System.Diagnostics;
using System.Text.Json;

namespace ClaudePopup;

record HistoryEntry(string Title, string Message, string Question, string Type, DateTime Timestamp, string SessionId = "", string Cwd = "");

/// <summary>
/// Lightweight index entry cached in memory (no message/question text).
/// </summary>
record HistoryIndex(string Title, string Type, DateTime Timestamp, string DayFile, int Index, string SessionId = "", string Cwd = "");

static class ResponseHistory
{
    private static readonly string _historyDir = Path.Combine(AppSettings.DataDir, "history");

    private static List<HistoryIndex>? _cachedIndex;

    public static bool IsEnabled
    {
        get => AppSettings.Load().HistoryEnabled;
        set
        {
            var settings = AppSettings.Load();
            AppSettings.Save(settings with { HistoryEnabled = value });
        }
    }

    private static string GetDayFile(DateTime date)
        => Path.Combine(_historyDir, $"{date:yyyyMMdd}.json");

    public static void SaveQuestion(string question, string sessionId = "", string cwd = "")
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(question)) return;

        var entries = LoadDayEntries(DateTime.Now);
        entries.Add(new HistoryEntry("Claude Code", "", question.Trim(), NotificationType.Pending, DateTime.Now, sessionId, cwd));
        WriteDay(DateTime.Now, entries);
    }

    public static void SaveResponse(string title, string message, string type, string sessionId = "", string cwd = "")
    {
        if (!IsEnabled) return;

        var entries = LoadDayEntries(DateTime.Now);

        if (entries.Count > 0 && entries[^1].Type == NotificationType.Pending)
        {
            var pending = entries[^1];
            entries[^1] = pending with { Title = title, Message = message, Type = type, Timestamp = DateTime.Now, SessionId = sessionId, Cwd = cwd };
        }
        else
        {
            entries.Add(new HistoryEntry(title, message, "", type, DateTime.Now, sessionId, cwd));
        }

        WriteDay(DateTime.Now, entries);
    }

    /// <summary>
    /// Returns the latest full entry by reading from disk.
    /// </summary>
    public static HistoryEntry? GetLatest()
    {
        var index = LoadIndex();
        if (index.Count == 0) return null;
        return LoadEntry(index[^1]);
    }

    /// <summary>
    /// Loads a full entry from disk by its index reference.
    /// </summary>
    public static HistoryEntry? LoadEntry(HistoryIndex idx)
    {
        try
        {
            if (!File.Exists(idx.DayFile)) return null;
            string json = File.ReadAllText(idx.DayFile);
            var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json);
            if (entries != null && idx.Index < entries.Count)
                return entries[idx.Index];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load history entry: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Returns lightweight index (no message text), capped at last 100 entries.
    /// </summary>
    public static List<HistoryIndex> LoadIndex()
    {
        if (_cachedIndex != null) return _cachedIndex;

        var index = new List<HistoryIndex>();
        try
        {
            if (!Directory.Exists(_historyDir)) return index;

            foreach (var file in Directory.GetFiles(_historyDir, "*.json").OrderBy(f => f))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json);
                    if (entries == null) continue;

                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        index.Add(new HistoryIndex(e.Title, e.Type, e.Timestamp, file, i, e.SessionId, e.Cwd));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to read history file {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load history index: {ex.Message}");
        }

        // Keep only last 100
        if (index.Count > 100)
            index = index.Skip(index.Count - 100).ToList();

        _cachedIndex = index;
        return index;
    }

    private static List<HistoryEntry> LoadDayEntries(DateTime date)
    {
        try
        {
            var file = GetDayFile(date);
            if (File.Exists(file))
            {
                string json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? new List<HistoryEntry>();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load day entries: {ex.Message}");
        }

        return new List<HistoryEntry>();
    }

    private static void WriteDay(DateTime date, List<HistoryEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(_historyDir);
            string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetDayFile(date), json);
            _cachedIndex = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write history: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns index entries for today only, ordered by timestamp.
    /// </summary>
    public static List<HistoryIndex> LoadTodayIndex()
    {
        var todayFile = GetDayFile(DateTime.Now);
        return LoadIndex().Where(i => string.Equals(i.DayFile, todayFile, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Timestamp).ToList();
    }

    /// <summary>
    /// Returns distinct CWD folder paths from today's entries (non-empty only).
    /// </summary>
    public static List<string> GetTodayDistinctCwd()
        => LoadTodayIndex().Where(i => !string.IsNullOrEmpty(i.Cwd))
            .Select(i => i.Cwd).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Returns distinct SessionId values from today's entries (non-empty only),
    /// paired with the CWD for display context.
    /// </summary>
    public static List<(string SessionId, string Cwd)> GetTodayDistinctSessions()
        => LoadTodayIndex().Where(i => !string.IsNullOrEmpty(i.SessionId))
            .GroupBy(i => i.SessionId)
            .Select(g => (g.Key, g.First().Cwd))
            .ToList();

    /// <summary>
    /// Returns today's entries filtered by CWD, ordered by timestamp.
    /// </summary>
    public static List<HistoryIndex> FilterTodayByCwd(string cwd)
        => LoadTodayIndex().Where(i => string.Equals(i.Cwd, cwd, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Returns today's entries filtered by SessionId, ordered by timestamp.
    /// </summary>
    public static List<HistoryIndex> FilterTodayBySession(string sessionId)
        => LoadTodayIndex().Where(i => string.Equals(i.SessionId, sessionId, StringComparison.Ordinal)).ToList();

    public static void Invalidate() => _cachedIndex = null;
}
