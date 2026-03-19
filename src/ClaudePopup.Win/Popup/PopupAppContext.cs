using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace ClaudePopup;

class PopupAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly PopupForm _popupForm;
    private readonly CancellationTokenSource _cts = new();
    private Icon _appIcon;
    private SettingsForm? _settingsForm;

    public PopupAppContext(string initialTitle, string initialMessage, string initialType)
    {
        var theme = Themes.LoadSaved();
        _appIcon = Themes.CreateAppIcon(theme.Primary);

        _popupForm = new PopupForm(theme);
        _popupForm.ShowPopup(initialTitle, initialMessage, initialType);

        _trayIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "ClaudePopup",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        _trayIcon.DoubleClick += (_, _) => _popupForm.BringToForeground();

        Task.Run(() => PipeServerLoop(_cts.Token));
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Last Notification", null, (_, _) => _popupForm.BringToForeground());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings...", null, (_, _) => ShowSettingsForm());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        return menu;
    }

    private void ShowSettingsForm()
    {
        // Bring existing settings form to front if already open
        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            _settingsForm.BringToFront();
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_popupForm.CurrentTheme, _popupForm.IsSnoozed);

        _settingsForm.ThemeChanged += theme =>
        {
            // Update tray icon
            _appIcon.Dispose();
            _appIcon = Themes.CreateAppIcon(theme.Primary);
            _trayIcon.Icon = _appIcon;

            // Apply to popup
            _popupForm.ApplyTheme(theme);
        };

        _settingsForm.HistoryEnabledChanged += _ =>
        {
            _popupForm.UpdateHistoryNav();
        };

        _settingsForm.SnoozeChanged += snoozed =>
        {
            if (snoozed)
                _popupForm.Snooze();
            else
                _popupForm.Unsnooze();

            UpdateTrayText();
        };

        _popupForm.SnoozeChanged += UpdateTrayText;

        _settingsForm.FormClosed += (_, _) =>
        {
            _popupForm.SnoozeChanged -= UpdateTrayText;
            _settingsForm = null;
        };

        _settingsForm.Show();
    }

    private void UpdateTrayText()
    {
        if (_popupForm.IsSnoozed)
        {
            var remaining = _popupForm.SnoozeUntil - DateTime.Now;
            int mins = Math.Max(1, (int)remaining.TotalMinutes);
            _trayIcon.Text = $"ClaudePopup (snoozed {mins}m)";
        }
        else
        {
            _trayIcon.Text = "ClaudePopup";
        }
    }

    private void ExitApp()
    {
        _cts.Cancel();
        _settingsForm?.Close();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _appIcon.Dispose();
        _popupForm.ForceExit();
        ExitThread();
    }

    private async Task PipeServerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    Program.PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var json = await reader.ReadToEndAsync(ct);

                if (!string.IsNullOrEmpty(json))
                {
                    var msg = JsonSerializer.Deserialize<PipeMessage>(json);
                    if (msg != null)
                    {
                        _popupForm.Invoke(() => _popupForm.ShowPopup(
                            msg.title ?? "Claude Code",
                            msg.message ?? "Task completed.",
                            msg.type ?? NotificationType.Info));
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pipe server error: {ex.Message}");
                await Task.Delay(100, ct);
            }
        }
    }

    private record PipeMessage(string? title, string? message, string? type);
}
