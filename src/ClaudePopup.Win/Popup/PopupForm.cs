using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ClaudePopup;

class PopupForm : Form
{
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
    private string _lastType = NotificationType.Info;
    private DateTime _snoozeUntil = DateTime.MinValue;
    private readonly CheckBox _snoozeCheckBox;

    private int _historyIndex = -1; // -1 = showing live/current message
    private bool _viewingHistory;

    private const int HeaderHeight = 58;
    private const int InfoBarHeight = 56;
    private const int FooterHeight = 100;

    public PopupTheme CurrentTheme => _theme;
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
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView2 init failed: {ex.Message}");
        }
    }

    public void ApplyTheme(PopupTheme theme)
    {
        _theme = theme;

        // Update form colors
        BackColor = theme.BgDark;
        ForeColor = theme.TextPrimary;
        var oldIcon = Icon;
        Icon = Themes.CreateAppIcon(theme.Primary);
        oldIcon?.Dispose();

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
            NotificationType.Success => (_theme.SuccessColor, "\u2713", _theme.SuccessBg, "Completed successfully"),
            NotificationType.Error => (_theme.ErrorColor, "\u2717", _theme.ErrorBg, "An error occurred"),
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
        NativeMethods.ShowWindow(Handle, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(Handle);
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
        catch (Exception ex)
        {
            Debug.WriteLine($"GetFirstName failed: {ex.Message}");
            return "User";
        }
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
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView2 cleanup failed: {ex.Message}");
        }
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
        // Compute sparkle origin relative to form coordinates from _animLabel bounds
        var labelBounds = RectangleToClient(_animLabel.RectangleToScreen(_animLabel.ClientRectangle));

        for (int i = 0; i < 3; i++)
        {
            _sparkles.Add(new Sparkle
            {
                X = labelBounds.X + _rng.Next(labelBounds.Width),
                Y = labelBounds.Y + _rng.Next(labelBounds.Height),
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
