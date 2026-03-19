using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ClaudePopup;

static class Program
{
    private const string MutexName = "ClaudePopup_SingleInstance_Mutex";
    internal const string PipeName = "ClaudePopup_Pipe";

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // No arguments → show setup instructions (always new instance)
        if (args.Length == 0)
        {
            Application.Run(new SetupForm());
            return;
        }

        string title = "Claude Code";
        string message = "Task completed.";
        string type = "info";
        string? messageFile = null;
        string? saveQuestion = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--title" or "-t" when i + 1 < args.Length:
                    title = args[++i];
                    break;
                case "--message" or "-m" when i + 1 < args.Length:
                    message = args[++i];
                    break;
                case "--message-file" when i + 1 < args.Length:
                    messageFile = args[++i];
                    break;
                case "--save-question" when i + 1 < args.Length:
                    saveQuestion = args[++i];
                    break;
                case "--type" when i + 1 < args.Length:
                    type = args[++i].ToLowerInvariant();
                    break;
            }
        }

        // Save question to history and exit (UserPromptSubmit hook)
        if (saveQuestion != null)
        {
            // Read from file if it's a file path
            if (File.Exists(saveQuestion))
                saveQuestion = File.ReadAllText(saveQuestion, Encoding.UTF8);
            ResponseHistory.SaveQuestion(saveQuestion.Replace("\\n", "\n").Replace("\\t", "\t").Trim());
            return;
        }

        // Read message from file if specified (avoids command-line length limits)
        if (messageFile != null && File.Exists(messageFile))
            message = File.ReadAllText(messageFile, Encoding.UTF8);

        message = message.Replace("\\n", "\n").Replace("\\t", "\t");

        // Save response to history (completes pending question entry)
        ResponseHistory.SaveResponse(title, message, type);

        using var mutex = new Mutex(true, MutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            SendToPipe(title, message, type);
            return;
        }

        Application.Run(new PopupAppContext(title, message, type));
    }

    private static void SendToPipe(string title, string message, string type)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(3000);
            var payload = JsonSerializer.Serialize(new { title, message, type });
            var bytes = Encoding.UTF8.GetBytes(payload);
            client.Write(bytes, 0, bytes.Length);
        }
        catch { }
    }
}

class PopupAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly PopupForm _popupForm;
    private readonly CancellationTokenSource _cts = new();
    private readonly ToolStripMenuItem _themeMenu;
    private ToolStripMenuItem _snoozeMenuItem = null!;
    private ToolStripMenuItem _historyMenuItem = null!;
    private Icon _appIcon;

    public PopupAppContext(string initialTitle, string initialMessage, string initialType)
    {
        var theme = Themes.LoadSaved();
        _appIcon = Themes.CreateAppIcon(theme.Primary);

        _popupForm = new PopupForm(theme);
        _popupForm.SnoozeChanged += UpdateSnoozeMenuItem;
        _popupForm.ShowPopup(initialTitle, initialMessage, initialType);

        _themeMenu = new ToolStripMenuItem("Theme");
        RebuildThemeMenu(theme);

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

    private void RebuildThemeMenu(PopupTheme current)
    {
        _themeMenu.DropDownItems.Clear();
        foreach (var theme in Themes.All)
        {
            var item = new ToolStripMenuItem(theme.Name)
            {
                Checked = theme.Name == current.Name,
                Tag = theme,
            };
            item.Click += OnThemeSelected;
            _themeMenu.DropDownItems.Add(item);
        }
    }

    private void OnThemeSelected(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item || item.Tag is not PopupTheme theme) return;

        // Update checks
        foreach (ToolStripMenuItem mi in _themeMenu.DropDownItems)
            mi.Checked = mi.Tag is PopupTheme t && t.Name == theme.Name;

        // Update icon
        _appIcon.Dispose();
        _appIcon = Themes.CreateAppIcon(theme.Primary);
        _trayIcon.Icon = _appIcon;

        // Apply to popup
        _popupForm.ApplyTheme(theme);

        // Save preference
        Themes.Save(theme);
    }

    private void UpdateSnoozeMenuItem()
    {
        if (_popupForm.IsSnoozed)
        {
            var remaining = _popupForm.SnoozeUntil - DateTime.Now;
            int mins = Math.Max(1, (int)remaining.TotalMinutes);
            _snoozeMenuItem.Text = $"Snoozed ({mins} min left) — click to resume";
            _snoozeMenuItem.Checked = true;
            _trayIcon.Text = $"ClaudePopup (snoozed {mins}m)";
        }
        else
        {
            _snoozeMenuItem.Text = "Snooze for 30 minutes";
            _snoozeMenuItem.Checked = false;
            _trayIcon.Text = "ClaudePopup";
        }
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Last Notification", null, (_, _) => _popupForm.BringToForeground());
        menu.Items.Add(new ToolStripSeparator());

        _snoozeMenuItem = new ToolStripMenuItem("Snooze for 30 minutes");
        _snoozeMenuItem.Click += (_, _) =>
        {
            if (_popupForm.IsSnoozed)
                _popupForm.Unsnooze();
            else
                _popupForm.Snooze();
        };
        menu.Items.Add(_snoozeMenuItem);

        _historyMenuItem = new ToolStripMenuItem("Save Response History")
        {
            Checked = ResponseHistory.IsEnabled,
            CheckOnClick = true,
        };
        _historyMenuItem.Click += (_, _) =>
        {
            ResponseHistory.IsEnabled = _historyMenuItem.Checked;
            _popupForm.UpdateHistoryNav();
        };
        menu.Items.Add(_historyMenuItem);

        menu.Items.Add(_themeMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        return menu;
    }

    private void ExitApp()
    {
        _cts.Cancel();
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
                            msg.type ?? "info"));
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(100, ct); }
        }
    }

    private record PipeMessage(string? title, string? message, string? type);
}

class PopupForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public static readonly string Version = "1.0.3";

    private PopupTheme _theme;

    private readonly Label _animLabel;
    private readonly Panel _headerPanel;
    private readonly Label _iconLabel;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _versionLabel;
    private readonly Panel _headerLine;
    private readonly WebView2 _messageWebView;
    private readonly Panel _footerPanel;
    private readonly Panel _webViewContainer;
    private readonly Panel _separator;
    private readonly RoundedButton _okButton;
    private readonly RoundedButton _prevButton;
    private readonly RoundedButton _nextButton;
    private readonly Label _navLabel;
    private readonly System.Windows.Forms.Timer _typeTimer;
    private readonly System.Windows.Forms.Timer _sparkleTimer;
    private readonly List<Sparkle> _sparkles = new();
    private readonly Random _rng = new();

    private string _funnyText = "";
    private int _charIndex;
    private Color _accentColor;
    private Color _iconBadgeBg;
    private bool _webViewReady;
    private string? _webView2UserDataFolder;
    private string? _pendingHtml;
    private bool _forceExit;
    private string _lastMessage = "";
    private string _lastType = "info";
    private DateTime _snoozeUntil = DateTime.MinValue;
    private readonly CheckBox _snoozeCheckBox;

    private int _historyIndex = -1; // -1 = showing live/current message
    private bool _viewingHistory;

    private const int HeaderHeight = 58;
    private const int InfoBarHeight = 56;
    private const int FooterHeight = 100;

    public bool IsSnoozed => DateTime.Now < _snoozeUntil;
    public DateTime SnoozeUntil => _snoozeUntil;

    public event Action? SnoozeChanged;

    public PopupForm(PopupTheme theme)
    {
        _theme = theme;
        _accentColor = theme.Primary;
        _iconBadgeBg = theme.PrimaryDim;

        Text = "Claude Code";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        MinimumSize = new Size(400, 300);
        Icon = Themes.CreateAppIcon(theme.Primary);

        BackColor = theme.BgDark;
        ForeColor = theme.TextPrimary;
        Font = new Font("Segoe UI", 10f);

        // --- Header (top, docked) ---
        _headerPanel = new Panel
        {
            BackColor = theme.BgHeader,
            Dock = DockStyle.Top,
            Height = HeaderHeight,
        };

        _animLabel = new Label
        {
            Text = "",
            Font = new Font("Cascadia Code", 11f, FontStyle.Italic),
            ForeColor = theme.PrimaryLight,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 0, 10, 0),
        };
        _headerPanel.Padding = new Padding(0, 10, 0, 10);
        _headerPanel.Controls.Add(_animLabel);

        _headerLine = new Panel
        {
            BackColor = theme.Primary,
            Dock = DockStyle.Top,
            Height = 2,
        };

        // --- Info bar (icon + title + subtitle + version) - fixed height below header ---
        var infoPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = InfoBarHeight,
            BackColor = theme.BgDark,
        };

        _iconLabel = new Label
        {
            Text = "\u2139",
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = theme.Primary,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(48, 48),
            Location = new Point(22, 4)
        };

        _titleLabel = new Label
        {
            Text = "Claude Code",
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = theme.TextPrimary,
            AutoSize = true,
            Location = new Point(82, 6),
            BackColor = Color.Transparent
        };

        _subtitleLabel = new Label
        {
            Text = "Notification",
            Font = new Font("Segoe UI", 9f),
            ForeColor = theme.TextSecondary,
            AutoSize = true,
            Location = new Point(82, 30),
            BackColor = Color.Transparent
        };

        _versionLabel = new Label
        {
            Text = $"v{Version}",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(60, 75, 105),
            AutoSize = true,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };

        // --- Nav buttons for history (in info bar, top center) ---
        _prevButton = new RoundedButton
        {
            Text = "\u25C0",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Size = new Size(32, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.PrimaryDim,
            ForeColor = theme.TextSecondary,
            Cursor = Cursors.Hand,
            Visible = false,
        };
        _prevButton.FlatAppearance.BorderSize = 0;
        _prevButton.FlatAppearance.MouseOverBackColor = theme.Primary;
        _prevButton.Click += (_, _) => NavigateHistory(-1);

        _nextButton = new RoundedButton
        {
            Text = "\u25B6",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Size = new Size(32, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.PrimaryDim,
            ForeColor = theme.TextSecondary,
            Cursor = Cursors.Hand,
            Visible = false,
        };
        _nextButton.FlatAppearance.BorderSize = 0;
        _nextButton.FlatAppearance.MouseOverBackColor = theme.Primary;
        _nextButton.Click += (_, _) => NavigateHistory(+1);

        _navLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 8f),
            ForeColor = theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Visible = false,
        };

        infoPanel.Controls.AddRange(new Control[]
        {
            _iconLabel, _titleLabel, _subtitleLabel,
            _prevButton, _navLabel, _nextButton
        });

        // --- Footer (bottom, docked) ---
        _footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = FooterHeight,
            BackColor = theme.BgDark,
        };

        _separator = new Panel
        {
            BackColor = theme.Border,
            Height = 1,
            Dock = DockStyle.Top,
        };

        _okButton = new RoundedButton
        {
            Text = "OK",
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            Size = new Size(130, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.Primary,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top,
        };
        _okButton.FlatAppearance.BorderSize = 0;
        _okButton.FlatAppearance.MouseOverBackColor = theme.PrimaryLight;
        _okButton.FlatAppearance.MouseDownBackColor = theme.PrimaryDim;
        _okButton.Click += (_, _) => OnOkClick();

        _snoozeCheckBox = new CheckBox
        {
            Text = "Snooze for 30 minutes",
            Font = new Font("Segoe UI", 9f),
            ForeColor = theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Location = new Point(20, 60),
        };

        _footerPanel.Controls.Add(_separator);
        _footerPanel.Controls.Add(_okButton);
        _footerPanel.Controls.Add(_snoozeCheckBox);
        _footerPanel.Controls.Add(_versionLabel);

        // --- WebView (fills remaining space, with padding) ---
        _messageWebView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = theme.BgDark,
        };

        _webViewContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 8, 16, 8),
            BackColor = theme.BgDark,
        };
        _webViewContainer.Controls.Add(_messageWebView);

        ClientSize = new Size(800, 500);
        AcceptButton = _okButton;

        // WinForms docks last-added first. We want:
        //   Top: _headerPanel, _headerLine, infoPanel
        //   Bottom: _footerPanel
        //   Fill: webViewContainer (with padding around WebView)
        // So add Fill first, then the docked panels in reverse visual order.
        Controls.Add(_webViewContainer);   // Fill - added first, docked last
        Controls.Add(_footerPanel);       // Bottom
        Controls.Add(infoPanel);          // Top (below header line)
        Controls.Add(_headerLine);        // Top (below header)
        Controls.Add(_headerPanel);       // Top (first)

        _typeTimer = new System.Windows.Forms.Timer { Interval = 60 };
        _typeTimer.Tick += TypeTimer_Tick;

        _sparkleTimer = new System.Windows.Forms.Timer { Interval = 40 };
        _sparkleTimer.Tick += SparkleTimer_Tick;

        InitializeWebView2();
    }

    private void PositionControls()
    {
        if (_footerPanel == null || _okButton == null || _versionLabel == null) return;

        // Center OK button in footer
        _okButton.Location = new Point((_footerPanel.ClientSize.Width - _okButton.Width) / 2, 16);

        // Version label bottom-right of footer
        _versionLabel.Location = new Point(
            _footerPanel.ClientSize.Width - _versionLabel.Width - 12,
            _footerPanel.ClientSize.Height - _versionLabel.Height - 6);

        // Nav buttons right-aligned in info panel
        var infoPanel = _prevButton.Parent;
        if (infoPanel != null)
        {
            int ipw = infoPanel.ClientSize.Width;

            // Nav buttons right-aligned in info panel
            if (_prevButton.Visible)
            {
                int navY = (InfoBarHeight - _prevButton.Height) / 2;
                int navX = ipw - _nextButton.Width - 12;
                _nextButton.Location = new Point(navX, navY);
                navX -= (8 + _navLabel.PreferredWidth);
                _navLabel.Location = new Point(navX, navY + (_prevButton.Height - _navLabel.Height) / 2);
                navX -= (8 + _prevButton.Width);
                _prevButton.Location = new Point(navX, navY);
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PositionControls();
        Invalidate();
    }

    private async void InitializeWebView2()
    {
        try
        {
            _webView2UserDataFolder = Path.Combine(Path.GetTempPath(), "ClaudePopup_" + Environment.ProcessId);
            var env = await CoreWebView2Environment.CreateAsync(null, _webView2UserDataFolder);
            await _messageWebView.EnsureCoreWebView2Async(env);
            _messageWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _messageWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _messageWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _messageWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _webViewReady = true;

            if (_pendingHtml != null)
            {
                _messageWebView.NavigateToString(_pendingHtml);
                _pendingHtml = null;
            }
        }
        catch { }
    }

    public void ApplyTheme(PopupTheme theme)
    {
        _theme = theme;

        // Update form colors
        BackColor = theme.BgDark;
        ForeColor = theme.TextPrimary;
        Icon = Themes.CreateAppIcon(theme.Primary);

        _headerPanel.BackColor = theme.BgHeader;
        _animLabel.ForeColor = theme.PrimaryLight;
        _headerLine.BackColor = theme.Primary;
        _titleLabel.ForeColor = theme.TextPrimary;
        _subtitleLabel.ForeColor = theme.TextSecondary;
        _separator.BackColor = theme.Border;
        _footerPanel.BackColor = theme.BgDark;
        _webViewContainer.BackColor = theme.BgDark;
        _messageWebView.DefaultBackgroundColor = theme.BgDark;

        _okButton.BackColor = theme.Primary;
        _okButton.FlatAppearance.MouseOverBackColor = theme.PrimaryLight;
        _okButton.FlatAppearance.MouseDownBackColor = theme.PrimaryDim;

        _snoozeCheckBox.ForeColor = theme.TextSecondary;

        _prevButton.BackColor = theme.PrimaryDim;
        _prevButton.ForeColor = theme.TextSecondary;
        _prevButton.FlatAppearance.MouseOverBackColor = theme.Primary;
        _nextButton.BackColor = theme.PrimaryDim;
        _nextButton.ForeColor = theme.TextSecondary;
        _nextButton.FlatAppearance.MouseOverBackColor = theme.Primary;
        _navLabel.ForeColor = theme.TextSecondary;

        // Re-apply type-based colors
        ApplyTypeColors(_lastType);

        // Re-render WebView content with new theme colors
        if (_webViewReady && !string.IsNullOrEmpty(_lastMessage))
        {
            _messageWebView.NavigateToString(RenderHtml(_lastMessage));
        }

        Invalidate();
    }

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private string RenderHtml(string message)
    {
        bool isLight = _theme.BgDark.GetBrightness() > 0.5f;
        return MarkdownRenderer.ToHtml(
            message,
            accentColorHex: ToHex(_accentColor),
            textColorHex: ToHex(isLight ? _theme.TextPrimary : _theme.TextSecondary),
            headingColorHex: ToHex(_theme.TextPrimary),
            bgColorHex: ToHex(_theme.BgDark),
            codeBgHex: isLight ? "rgba(0,0,0,0.06)" : "rgba(12, 16, 26, 0.8)");
    }

    private void ApplyTypeColors(string type)
    {
        var (accentColor, iconText, iconBadgeBg, subtitle) = type switch
        {
            "success" => (_theme.SuccessColor, "\u2713", _theme.SuccessBg, "Completed successfully"),
            "error" => (_theme.ErrorColor, "\u2717", _theme.ErrorBg, "An error occurred"),
            _ => (_theme.Primary, "\u2139", _theme.PrimaryDim, "Notification")
        };
        _accentColor = accentColor;
        _iconBadgeBg = iconBadgeBg;
        _iconLabel.Text = iconText;
        _iconLabel.ForeColor = accentColor;
        _subtitleLabel.Text = subtitle;
        _headerLine.BackColor = accentColor;
    }

    public void Snooze()
    {
        _snoozeUntil = DateTime.Now.AddMinutes(30);
        _snoozeCheckBox.Checked = true;
        SnoozeChanged?.Invoke();
    }

    public void Unsnooze()
    {
        _snoozeUntil = DateTime.MinValue;
        _snoozeCheckBox.Checked = false;
        SnoozeChanged?.Invoke();
    }

    private void OnOkClick()
    {
        if (_snoozeCheckBox.Checked)
        {
            _snoozeUntil = DateTime.Now.AddMinutes(30);
            SnoozeChanged?.Invoke();
        }
        else if (IsSnoozed)
        {
            // User unchecked → cancel snooze
            _snoozeUntil = DateTime.MinValue;
            SnoozeChanged?.Invoke();
        }
        Hide();
    }

    public void ShowPopup(string title, string message, string type)
    {
        // Always store latest message even if snoozed
        _lastMessage = message;
        _lastType = type;

        // Reset to live view
        _viewingHistory = false;
        _historyIndex = -1;

        // If snoozed, don't show the popup
        if (IsSnoozed)
            return;

        // Read question from the latest history entry
        ResponseHistory.Invalidate();
        var latest = ResponseHistory.GetLatest();
        string question = latest?.Question ?? "";

        DisplayMessage(title, message, type, question);

        // New funny quote + restart typewriter
        _funnyText = FunnyQuotes.Lines[_rng.Next(FunnyQuotes.Lines.Length)];
        _charIndex = 0;
        _animLabel.Text = "";
        _sparkles.Clear();
        _typeTimer.Start();

        Show();
        WindowState = FormWindowState.Normal;
        BringToTop();
        Invalidate();
    }

    private void BringToTop()
    {
        TopMost = true;
        ShowWindow(Handle, SW_RESTORE);
        SetForegroundWindow(Handle);
        BringToFront();
        Activate();
        // Release TopMost after a brief moment so user can click other windows over it
        var releaseTimer = new System.Windows.Forms.Timer { Interval = 500 };
        releaseTimer.Tick += (_, _) =>
        {
            TopMost = false;
            releaseTimer.Stop();
            releaseTimer.Dispose();
        };
        releaseTimer.Start();
    }

    private static string GetFirstName()
    {
        try
        {
            // Extract username from user profile directory (e.g. C:\Users\sukesh.chand)
            var userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var userName = Path.GetFileName(userDir) ?? "";
            // Split by common separators and take first part, then capitalize
            var first = userName.Split('.', ' ', '_', '-')[0];
            if (first.Length > 0)
                return char.ToUpper(first[0]) + first[1..];
            return userName;
        }
        catch { return "User"; }
    }

    private void DisplayMessage(string title, string message, string type, string question = "")
    {
        ApplyTypeColors(type);
        Text = title;
        _titleLabel.Text = title;

        // Build full content with styled user/Claude labels
        string firstName = GetFirstName();
        string fullContent;
        if (!string.IsNullOrWhiteSpace(question))
        {
            // Truncate long questions to first 3 lines
            var qLines = question.Split('\n');
            string shortQ = qLines.Length > 3
                ? string.Join("\n", qLines.Take(3)) + "..."
                : question;
            string escapedQ = System.Net.WebUtility.HtmlEncode(shortQ).Replace("\n", "<br/>");
            fullContent = $"<div class=\"user-block\"><div class=\"label\">{firstName}:</div><div class=\"text\">{escapedQ}</div></div>\n\n<div class=\"claude-label\">Claude:</div>\n\n{message}";
        }
        else
        {
            fullContent = $"<div class=\"claude-label\">Claude:</div>\n\n{message}";
        }

        // Estimate a good initial window height based on content, capped at 90% of screen
        var lineCount = fullContent.Split('\n').Length;
        int wrappedLines = lineCount;
        foreach (var line in fullContent.Split('\n'))
        {
            if (line.Length > 80)
                wrappedLines += (line.Length / 80);
        }
        int estimatedContentHeight = Math.Max(120, wrappedLines * 28 + 40);
        int maxHeight = (int)(Screen.FromControl(this).WorkingArea.Height * 0.9);
        int totalHeight = Math.Min(maxHeight, HeaderHeight + 2 + InfoBarHeight + estimatedContentHeight + FooterHeight);
        ClientSize = new Size(Math.Max(ClientSize.Width, 800), totalHeight);

        _snoozeCheckBox.Checked = IsSnoozed;

        string htmlContent = RenderHtml(fullContent);

        if (_webViewReady)
            _messageWebView.NavigateToString(htmlContent);
        else
            _pendingHtml = htmlContent;

        UpdateHistoryNav();
        PositionControls();
    }

    private void NavigateHistory(int direction)
    {
        ResponseHistory.Invalidate();
        var index = ResponseHistory.LoadIndex();
        if (index.Count == 0) return;

        if (!_viewingHistory)
        {
            _historyIndex = index.Count - 1;
            _viewingHistory = true;
        }

        _historyIndex += direction;
        _historyIndex = Math.Clamp(_historyIndex, 0, index.Count - 1);

        var entry = ResponseHistory.LoadEntry(index[_historyIndex]);
        if (entry != null)
            DisplayMessage(entry.Title, entry.Message, entry.Type, entry.Question);
    }

    public void UpdateHistoryNav()
    {
        var index = ResponseHistory.LoadIndex();
        bool showNav = ResponseHistory.IsEnabled && index.Count > 0;

        _prevButton.Visible = showNav;
        _nextButton.Visible = showNav;
        _navLabel.Visible = showNav;

        if (showNav)
        {
            if (_viewingHistory)
            {
                _navLabel.Text = $"{_historyIndex + 1} / {index.Count}";
                _prevButton.Enabled = _historyIndex > 0;
                _nextButton.Enabled = _historyIndex < index.Count - 1;
            }
            else
            {
                _navLabel.Text = $"Latest ({index.Count})";
                _prevButton.Enabled = index.Count > 0;
                _nextButton.Enabled = false;
            }

            // Position nav buttons at the right side of the info panel, not overlapping icon/title
            var infoPanel = _prevButton.Parent;
            if (infoPanel != null)
            {
                int ipw = infoPanel.ClientSize.Width;
                int navY = (InfoBarHeight - _prevButton.Height) / 2;
                int navX = ipw - _nextButton.Width - 12;
                _nextButton.Location = new Point(navX, navY);
                navX -= (8 + _navLabel.PreferredWidth);
                _navLabel.Location = new Point(navX, navY + (_prevButton.Height - _navLabel.Height) / 2);
                navX -= (8 + _prevButton.Width);
                _prevButton.Location = new Point(navX, navY);
            }
        }
    }

    public void BringToForeground()
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToTop();
    }

    public void ForceExit()
    {
        _forceExit = true;
        _typeTimer.Stop();
        _sparkleTimer.Stop();
        try
        {
            if (_webView2UserDataFolder != null && Directory.Exists(_webView2UserDataFolder))
                Directory.Delete(_webView2UserDataFolder, true);
        }
        catch { }
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_forceExit && e.CloseReason == CloseReason.UserClosing)
        {
            // Minimize to tray instead of exiting
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw badge circle behind the icon label (translate from icon's parent to form coords)
        var iconScreenPos = _iconLabel.Parent!.PointToScreen(_iconLabel.Location);
        var iconFormPos = PointToClient(iconScreenPos);
        int badgeX = iconFormPos.X, badgeY = iconFormPos.Y, badgeSize = 48;
        using (var badgeBrush = new SolidBrush(_iconBadgeBg))
            g.FillEllipse(badgeBrush, badgeX, badgeY, badgeSize, badgeSize);
        using (var badgePen = new Pen(Color.FromArgb(60, _accentColor), 1.5f))
            g.DrawEllipse(badgePen, badgeX, badgeY, badgeSize, badgeSize);

        foreach (var s in _sparkles)
        {
            int alpha = Math.Clamp((int)(s.Life * 255), 0, 255);
            using var brush = new SolidBrush(Color.FromArgb(alpha, s.Color));
            g.FillEllipse(brush, (float)s.X, (float)s.Y, s.Size, s.Size);
        }
    }

    private void TypeTimer_Tick(object? sender, EventArgs e)
    {
        if (_charIndex < _funnyText.Length)
        {
            _charIndex++;
            _animLabel.Text = "\u201C" + _funnyText[.._charIndex] + "\u2588";
        }
        else
        {
            _animLabel.Text = "\u201C" + _funnyText + "\u201D";
            _typeTimer.Stop();
            _sparkleTimer.Start();
            var stopTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            stopTimer.Tick += (_, _) =>
            {
                _sparkleTimer.Stop();
                stopTimer.Stop();
                stopTimer.Dispose();
                Invalidate();
            };
            stopTimer.Start();
        }
    }

    private void SparkleTimer_Tick(object? sender, EventArgs e)
    {
        for (int i = 0; i < 3; i++)
        {
            _sparkles.Add(new Sparkle
            {
                X = 280 + _rng.Next(100),
                Y = 22 + _rng.Next(20),
                VX = (_rng.NextDouble() - 0.5) * 8,
                VY = -_rng.NextDouble() * 4 - 1,
                Life = 1.0,
                Size = 3 + _rng.Next(5),
                Color = _rng.Next(4) switch
                {
                    0 => _theme.Sparkle1,
                    1 => _theme.Sparkle2,
                    2 => _theme.Sparkle3,
                    _ => _theme.Sparkle4
                }
            });
        }

        for (int i = _sparkles.Count - 1; i >= 0; i--)
        {
            var s = _sparkles[i];
            s.X += s.VX;
            s.Y += s.VY;
            s.VY += 0.15;
            s.Life -= 0.04;
            if (s.Life <= 0)
                _sparkles.RemoveAt(i);
        }

        Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderHeight + 2));
    }
}

class RoundedButton : Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width, Height);
        using var path = GetRoundedRect(rect, 8);
        using var brush = new SolidBrush(BackColor);
        g.Clear(Parent?.BackColor ?? Color.Transparent);
        g.FillPath(brush, path);

        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var textBrush = new SolidBrush(ForeColor);
        g.DrawString(Text, Font, textBrush, new RectangleF(0, 0, Width, Height), sf);
    }

    private static GraphicsPath GetRoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

class Sparkle
{
    public double X, Y, VX, VY, Life;
    public int Size;
    public Color Color;
}
