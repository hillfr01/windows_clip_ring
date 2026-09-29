namespace ClipRing;

/// <summary>
/// Hidden window that receives global hotkey and clipboard-change notifications.
/// </summary>
internal sealed class MessageWindow : NativeWindow, IDisposable
{
    public event EventHandler<int>? HotkeyPressed;
    public event EventHandler? ClipboardChanged;

    private readonly List<int> _hotkeyIds = new();

    public MessageWindow()
    {
        CreateHandle(new CreateParams());
        NativeMethods.AddClipboardFormatListener(Handle);
    }

    public bool RegisterHotkey(int id, uint modifiers, Keys key)
    {
        if (!NativeMethods.RegisterHotKey(Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, (uint)key))
            return false;
        _hotkeyIds.Add(id);
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_HOTKEY:
                HotkeyPressed?.Invoke(this, (int)m.WParam);
                return;
            case NativeMethods.WM_CLIPBOARDUPDATE:
                ClipboardChanged?.Invoke(this, EventArgs.Empty);
                return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        foreach (var id in _hotkeyIds)
            NativeMethods.UnregisterHotKey(Handle, id);
        NativeMethods.RemoveClipboardFormatListener(Handle);
        DestroyHandle();
    }
}
