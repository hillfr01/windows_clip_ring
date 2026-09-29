namespace ClipRing;

/// <summary>
/// Keyboard-driven popup listing the clipboard history.
/// Up/Down to move, Enter to pick, Esc to cancel, 1-9/0 to pick directly.
/// </summary>
internal sealed class RingPopup : Form
{
    private const string Ellipsis = " ...";
    private const TextFormatFlags MeasureFlags =
        TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private readonly IReadOnlyList<string> _snippets;
    private readonly ListBox _list;
    private readonly int _textPadding;
    private (string Line1, string? Line2)[]? _lines;

    /// <summary>The snippet the user chose, or null if they cancelled.</summary>
    public string? SelectedSnippet { get; private set; }

    public RingPopup(IReadOnlyList<string> snippets)
    {
        _snippets = snippets;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        BackColor = SystemColors.ActiveBorder;
        Padding = new Padding(1);
        Font = new Font("Segoe UI", 10f);
        _textPadding = LogicalToDeviceUnits(10);

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = Font.Height + LogicalToDeviceUnits(10),
            Text = "  Clip Ring   ↑↓ select · Enter copy · Esc cancel",
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = SystemColors.Control,
            ForeColor = SystemColors.GrayText,
        };

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            // Room for two lines of text per entry.
            ItemHeight = Math.Min(255, Font.Height * 2 + LogicalToDeviceUnits(10)),
        };
        _list.DrawItem += DrawItem;
        _list.DoubleClick += (_, _) => Choose(_list.SelectedIndex);

        if (_snippets.Count == 0)
        {
            _list.Items.Add("(nothing copied yet)");
            _list.Enabled = false;
        }
        else
        {
            foreach (var snippet in _snippets)
                _list.Items.Add(snippet);
            _list.SelectedIndex = 0;
        }

        Controls.Add(_list);
        Controls.Add(header);

        int rows = Math.Max(_list.Items.Count, 1);
        ClientSize = new Size(LogicalToDeviceUnits(640), header.Height + rows * _list.ItemHeight + Padding.Vertical + 4);
        CenterOnCursorScreen();

        Deactivate += (_, _) => Close();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        NativeMethods.SetForegroundWindow(Handle);
        _list.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                Choose(_list.SelectedIndex);
                e.SuppressKeyPress = true;
                break;
            case Keys.Escape:
                Close();
                e.SuppressKeyPress = true;
                break;
            case >= Keys.D0 and <= Keys.D9:
            case >= Keys.NumPad0 and <= Keys.NumPad9:
                int digit = e.KeyCode >= Keys.NumPad0 ? e.KeyCode - Keys.NumPad0 : e.KeyCode - Keys.D0;
                Choose(digit == 0 ? 9 : digit - 1);
                e.SuppressKeyPress = true;
                break;
        }
        base.OnKeyDown(e);
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= _snippets.Count)
        {
            if (_snippets.Count == 0) Close();
            return;
        }
        SelectedSnippet = _snippets[index];
        Close();
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();

        var color = !_list.Enabled ? SystemColors.GrayText
            : (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText
            : SystemColors.WindowText;
        var font = e.Font ?? Font;
        int x = e.Bounds.X + _textPadding;
        int width = e.Bounds.Width - 2 * _textPadding;

        if (!_list.Enabled)
        {
            DrawLine(e.Graphics, _list.Items[e.Index].ToString(), font, color, x, e.Bounds.Y + (e.Bounds.Height - font.Height) / 2, width);
            return;
        }

        _lines ??= _snippets.Select(s => FormatTwoLines(e.Graphics, s, font, width)).ToArray();
        var (line1, line2) = _lines[e.Index];

        if (line2 == null)
        {
            DrawLine(e.Graphics, line1, font, color, x, e.Bounds.Y + (e.Bounds.Height - font.Height) / 2, width);
        }
        else
        {
            int top = e.Bounds.Y + (e.Bounds.Height - font.Height * 2) / 2;
            DrawLine(e.Graphics, line1, font, color, x, top, width);
            DrawLine(e.Graphics, line2, font, color, x, top + font.Height, width);
        }

        // Thin divider so two-line entries are easy to tell apart.
        using var pen = new Pen(SystemColors.ControlLight);
        e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }

    private void CenterOnCursorScreen()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 3);
    }

    private static void DrawLine(Graphics g, string? text, Font font, Color color, int x, int y, int width) =>
        TextRenderer.DrawText(g, text, font, new Rectangle(x, y, width, font.Height), color, MeasureFlags);

    /// <summary>
    /// Fits a snippet plus its " (N char)" count into at most two lines of the given width. Text too long
    /// for two lines becomes "start of the text ..." on line one and "end of the text (N char)" on line two.
    /// </summary>
    private static (string Line1, string? Line2) FormatTwoLines(Graphics g, string text, Font font, int width)
    {
        // Show newlines/tabs/runs of spaces as single spaces.
        var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var count = $" ({text.Length} char)";
        int Measure(string s) => TextRenderer.MeasureText(g, s, font, new Size(int.MaxValue, int.MaxValue), MeasureFlags).Width;

        if (Measure(flat + count) <= width)
            return (flat + count, null);

        // Wraps onto two lines without cutting anything?
        int firstLen = BreakAtWord(flat, FitPrefix(flat, width, Measure));
        var rest = flat[firstLen..].TrimStart();
        if (Measure(rest + count) <= width)
            return (flat[..firstLen].TrimEnd(), (rest + count).TrimStart());

        // Too long: beginning + " ..." on line one, end + " (N char)" on line two.
        int headLen = BreakAtWord(flat, FitPrefix(flat, width - Measure(Ellipsis), Measure));
        int tailLen = FitSuffix(flat, width - Measure(count), Measure);
        tailLen = Math.Min(tailLen, flat.Length - headLen);
        int tailStart = StartAtWord(flat, flat.Length - tailLen);

        return (flat[..headLen].TrimEnd() + Ellipsis, flat[tailStart..].TrimStart() + count);
    }

    /// <summary>Longest prefix length that fits in the width.</summary>
    private static int FitPrefix(string s, int width, Func<string, int> measure)
    {
        int lo = 0, hi = s.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (measure(s[..mid]) <= width) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>Longest suffix length that fits in the width.</summary>
    private static int FitSuffix(string s, int width, Func<string, int> measure)
    {
        int lo = 0, hi = s.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (measure(s[^mid..]) <= width) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>Moves a cut point back to the previous space, unless that would waste over a third of the line.</summary>
    private static int BreakAtWord(string s, int length)
    {
        if (length >= s.Length || s[length] == ' ') return length;
        int space = s.LastIndexOf(' ', Math.Max(0, length - 1));
        return space > length * 2 / 3 ? space : length;
    }

    /// <summary>Moves a start point forward past a partial word, unless that would waste over a third of the line.</summary>
    private static int StartAtWord(string s, int start)
    {
        if (start <= 0 || s[start - 1] == ' ') return start;
        int space = s.IndexOf(' ', start);
        return space >= 0 && space - start < (s.Length - start) / 3 ? space + 1 : start;
    }
}
