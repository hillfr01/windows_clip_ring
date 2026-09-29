namespace ClipRing;

/// <summary>
/// Most-recent-first list of copied text snippets, capped at <see cref="Capacity"/>
/// and saved to a delimited text file so it survives restarts.
/// </summary>
internal sealed class ClipHistory
{
    public const int Capacity = 10;

    // Snippets can contain newlines, so entries are separated by a line holding only
    // the ASCII record-separator character, which never appears in normal copied text.
    private const string Separator = "\r\n\u001E\r\n";

    private readonly List<string> _items = new();
    private readonly string _filePath;

    public IReadOnlyList<string> Items => _items;

    public ClipHistory(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_items.Count > 0 && _items[0] == text) return;

        // Re-copying an existing snippet moves it to the top instead of duplicating it.
        _items.Remove(text);
        _items.Insert(0, text);
        if (_items.Count > Capacity)
            _items.RemoveRange(Capacity, _items.Count - Capacity);
        Save();
    }

    public void Clear()
    {
        _items.Clear();
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var entries = File.ReadAllText(_filePath).Split(Separator);
            _items.AddRange(entries.Where(e => !string.IsNullOrWhiteSpace(e)).Take(Capacity));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Start with an empty ring rather than failing to launch.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            // Write to a temp file first so a crash mid-write can't corrupt the history.
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, string.Join(Separator, _items));
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Keep working in memory; the next copy will try saving again.
        }
    }
}
