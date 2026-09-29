using System.Runtime.InteropServices;

namespace ClipRing;

/// <summary>
/// Runs in the system tray, records copied text, and shows the ring on Win+Shift+Z.
/// </summary>
internal sealed class TrayContext : ApplicationContext
{
    private const int RingHotkeyId = 1;
    private const string HotkeyText = "Win+Shift+Z";

    // Password managers set this format to ask clipboard tools not to record the copy.
    private const string ExcludeFormat = "ExcludeClipboardContentFromMonitorProcessing";

    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipRing", "history.txt");

    private readonly ClipHistory _history = new(HistoryPath);
    private readonly MessageWindow _window = new();
    private readonly NotifyIcon _tray;
    private RingPopup? _popup;

    public TrayContext()
    {
        _window.ClipboardChanged += (_, _) => CaptureClipboard();
        _window.HotkeyPressed += (_, id) => { if (id == RingHotkeyId) ShowRing(); };

        var menu = new ContextMenuStrip();
        menu.Items.Add($"Show ring ({HotkeyText})", null, (_, _) => ShowRing());
        menu.Items.Add("Clear history", null, (_, _) => _history.Clear());
        var startupItem = new ToolStripMenuItem("Start with Windows") { Checked = StartupRegistration.IsEnabled };
        startupItem.Click += (_, _) =>
        {
            StartupRegistration.SetEnabled(!startupItem.Checked);
            startupItem.Checked = StartupRegistration.IsEnabled;
        };
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = $"Clip Ring ({HotkeyText})",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowRing();

        CaptureClipboard();

        if (!_window.RegisterHotkey(RingHotkeyId, NativeMethods.MOD_WIN | NativeMethods.MOD_SHIFT, Keys.Z))
        {
            int error = Marshal.GetLastWin32Error();
            MessageBox.Show(
                $"Could not register {HotkeyText} (error {error}). Another program is probably using it.\n\n" +
                "Clip Ring is still running. Double-click its tray icon to open the ring.",
                "Clip Ring", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static Icon LoadTrayIcon()
    {
        // Ask for the small-icon size so Windows picks the matching frame for the current DPI.
        using var stream = typeof(TrayContext).Assembly.GetManifestResourceStream("ClipRing.app.ico");
        return stream != null ? new Icon(stream, SystemInformation.SmallIconSize) : SystemIcons.Application;
    }

    private void CaptureClipboard()
    {
        // The app that just copied may still hold the clipboard open, so retry briefly.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Clipboard.ContainsData(ExcludeFormat) || !Clipboard.ContainsText())
                    return;
                _history.Add(Clipboard.GetText());
                return;
            }
            catch (ExternalException)
            {
                Thread.Sleep(30);
            }
        }
    }

    private void ShowRing()
    {
        if (_popup != null)
        {
            _popup.Activate();
            return;
        }

        IntPtr previousWindow = NativeMethods.GetForegroundWindow();

        _popup = new RingPopup(_history.Items.ToList());
        _popup.FormClosed += (_, _) =>
        {
            string? chosen = _popup.SelectedSnippet;
            _popup.Dispose();
            _popup = null;

            if (chosen != null)
            {
                try
                {
                    Clipboard.SetDataObject(chosen, copy: true, retryTimes: 10, retryDelay: 50);
                }
                catch (ExternalException)
                {
                    _tray.ShowBalloonTip(3000, "Clip Ring", "Couldn't write to the clipboard. Try again.", ToolTipIcon.Warning);
                }
            }

            // Hand focus back to where the user was so Ctrl+V pastes there.
            if (previousWindow != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(previousWindow);
        };
        _popup.Show();
    }

    protected override void ExitThreadCore()
    {
        _popup?.Close();
        _tray.Visible = false;
        _tray.Dispose();
        _window.Dispose();
        base.ExitThreadCore();
    }
}
