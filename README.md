# Clip Ring

A tiny clipboard history for Windows. Press **Win+Shift+Z** to pop up your last 10 copied text snippets, pick one, and paste it with **Ctrl+V**.

Clip Ring runs quietly in the system tray.

## Usage

1. Copy text as usual.
2. Press **Win+Shift+Z** (or double-click the tray icon) to open the ring.
3. Choose a snippet:
   - press **1**–**9** or **0** (for the 10th) to pick by position, or
   - use the arrow keys and press **Enter**.
4. Press **Esc** to close the ring without choosing.
5. The chosen snippet is put on the clipboard and focus returns to the window you were in, so just press **Ctrl+V**.

Right-click the tray icon for:

- **Show ring**
- **Clear history**
- **Start with Windows** (toggle)
- **Exit**

## Behavior

- Keeps the 10 most recent **text** copies. Re-copying a snippet moves it to the top rather than duplicating it.
- History is saved to `%APPDATA%\ClipRing\history.txt`, so it survives restarts. It is stored as plain, unencrypted text.
- Copies flagged with `ExcludeClipboardContentFromMonitorProcessing` are skipped. Password managers set this to ask clipboard tools not to record the copy.
- Only one instance runs at a time.
- If another program already owns Win+Shift+Z, Clip Ring shows a warning at startup.

## Build

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```bash
dotnet run
```

To produce a standalone executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

The output is `bin/Release/net8.0-windows/win-x64/publish/ClipRing.exe`.

## License

[Apache License 2.0](LICENSE)
