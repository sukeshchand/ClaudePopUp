using System.Drawing;
using System.Drawing.Drawing2D;

namespace ClaudePopup;

class SettingsForm : Form
{
    private PopupTheme _currentTheme;
    private readonly Panel _themePreview;
    private readonly Label _themeNameLabel;

    public event Action<PopupTheme>? ThemeChanged;
    public event Action<bool>? HistoryEnabledChanged;
    public event Action<bool>? SnoozeChanged;

    public SettingsForm(PopupTheme currentTheme, bool isSnoozed)
    {
        _currentTheme = currentTheme;

        Text = "ClaudePopup Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(480, 460);
        BackColor = currentTheme.BgDark;
        ForeColor = currentTheme.TextPrimary;
        Font = new Font("Segoe UI", 10f);
        Icon = Themes.CreateAppIcon(currentTheme.Primary);

        int y = 20;
        int leftMargin = 28;
        int contentWidth = ClientSize.Width - leftMargin * 2;

        // --- Title ---
        var titleLabel = new Label
        {
            Text = "Settings",
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
            ForeColor = currentTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(leftMargin, y),
            BackColor = Color.Transparent,
        };
        Controls.Add(titleLabel);
        y += 44;

        // --- Accent line ---
        var accentLine = new Panel
        {
            BackColor = currentTheme.Primary,
            Location = new Point(leftMargin, y),
            Size = new Size(contentWidth, 2),
        };
        Controls.Add(accentLine);
        y += 18;

        // === Theme Section ===
        var themeSectionLabel = new Label
        {
            Text = "THEME",
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = currentTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(leftMargin, y),
            BackColor = Color.Transparent,
        };
        Controls.Add(themeSectionLabel);
        y += 24;

        // Theme grid — colored circles for each theme
        int circleSize = 40;
        int circleSpacing = 12;
        int circlesPerRow = 7;
        int gridX = leftMargin;

        for (int i = 0; i < Themes.All.Length; i++)
        {
            var theme = Themes.All[i];
            int col = i % circlesPerRow;
            int row = i / circlesPerRow;

            var btn = new ThemeCircleButton
            {
                Theme = theme,
                IsSelected = theme.Name == currentTheme.Name,
                Size = new Size(circleSize, circleSize),
                Location = new Point(gridX + col * (circleSize + circleSpacing), y + row * (circleSize + circleSpacing)),
                Cursor = Cursors.Hand,
            };
            btn.Click += OnThemeCircleClick;
            Controls.Add(btn);
        }

        int themeRows = (Themes.All.Length + circlesPerRow - 1) / circlesPerRow;
        y += themeRows * (circleSize + circleSpacing) + 4;

        // Selected theme name + preview bar
        _themeNameLabel = new Label
        {
            Text = currentTheme.Name,
            Font = new Font("Segoe UI", 10f),
            ForeColor = currentTheme.Primary,
            AutoSize = true,
            Location = new Point(leftMargin, y),
            BackColor = Color.Transparent,
        };
        Controls.Add(_themeNameLabel);

        _themePreview = new Panel
        {
            BackColor = currentTheme.Primary,
            Location = new Point(leftMargin, y + 24),
            Size = new Size(contentWidth, 4),
        };
        Controls.Add(_themePreview);
        y += 48;

        // --- Separator ---
        var sep1 = new Panel
        {
            BackColor = currentTheme.Border,
            Location = new Point(leftMargin, y),
            Size = new Size(contentWidth, 1),
        };
        Controls.Add(sep1);
        y += 18;

        // === Options Section ===
        var optionsSectionLabel = new Label
        {
            Text = "OPTIONS",
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = currentTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(leftMargin, y),
            BackColor = Color.Transparent,
        };
        Controls.Add(optionsSectionLabel);
        y += 28;

        // History toggle
        var historyCheck = new CheckBox
        {
            Text = "Save response history",
            Font = new Font("Segoe UI", 10f),
            ForeColor = currentTheme.TextPrimary,
            BackColor = Color.Transparent,
            Checked = ResponseHistory.IsEnabled,
            AutoSize = true,
            Location = new Point(leftMargin, y),
            Cursor = Cursors.Hand,
        };
        historyCheck.CheckedChanged += (_, _) =>
        {
            ResponseHistory.IsEnabled = historyCheck.Checked;
            HistoryEnabledChanged?.Invoke(historyCheck.Checked);
        };
        Controls.Add(historyCheck);

        var historyDesc = new Label
        {
            Text = "Keeps a daily log of Claude responses in _data/history/",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = currentTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(leftMargin + 20, y + 24),
            BackColor = Color.Transparent,
        };
        Controls.Add(historyDesc);
        y += 56;

        // Snooze toggle
        var snoozeCheck = new CheckBox
        {
            Text = "Snooze notifications (30 minutes)",
            Font = new Font("Segoe UI", 10f),
            ForeColor = currentTheme.TextPrimary,
            BackColor = Color.Transparent,
            Checked = isSnoozed,
            AutoSize = true,
            Location = new Point(leftMargin, y),
            Cursor = Cursors.Hand,
        };
        snoozeCheck.CheckedChanged += (_, _) =>
        {
            SnoozeChanged?.Invoke(snoozeCheck.Checked);
        };
        Controls.Add(snoozeCheck);

        var snoozeDesc = new Label
        {
            Text = "Suppresses popup windows while snoozed",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = currentTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(leftMargin + 20, y + 24),
            BackColor = Color.Transparent,
        };
        Controls.Add(snoozeDesc);
        y += 56;

        // --- Separator ---
        var sep2 = new Panel
        {
            BackColor = currentTheme.Border,
            Location = new Point(leftMargin, y),
            Size = new Size(contentWidth, 1),
        };
        Controls.Add(sep2);
        y += 18;

        // --- Close button ---
        var closeButton = new RoundedButton
        {
            Text = "Close",
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            Size = new Size(120, 38),
            Location = new Point((ClientSize.Width - 120) / 2, y),
            FlatStyle = FlatStyle.Flat,
            BackColor = currentTheme.Primary,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.FlatAppearance.MouseOverBackColor = currentTheme.PrimaryLight;
        closeButton.FlatAppearance.MouseDownBackColor = currentTheme.PrimaryDim;
        closeButton.Click += (_, _) => Close();
        Controls.Add(closeButton);

        // --- Version label ---
        var versionLabel = new Label
        {
            Text = $"ClaudePopup v{PopupForm.Version}",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(60, 75, 105),
            AutoSize = true,
            Location = new Point(leftMargin, ClientSize.Height - 26),
            BackColor = Color.Transparent,
        };
        Controls.Add(versionLabel);
    }

    private void OnThemeCircleClick(object? sender, EventArgs e)
    {
        if (sender is not ThemeCircleButton btn) return;

        _currentTheme = btn.Theme;

        // Update all circle selections
        foreach (Control c in Controls)
        {
            if (c is ThemeCircleButton circle)
            {
                circle.IsSelected = circle.Theme.Name == _currentTheme.Name;
                circle.Invalidate();
            }
        }

        // Update preview
        _themeNameLabel.Text = _currentTheme.Name;
        _themeNameLabel.ForeColor = _currentTheme.Primary;
        _themePreview.BackColor = _currentTheme.Primary;

        // Save and notify
        Themes.Save(_currentTheme);
        ThemeChanged?.Invoke(_currentTheme);
    }
}

class ThemeCircleButton : Control
{
    public PopupTheme Theme { get; set; } = Themes.Default;
    public bool IsSelected { get; set; }

    public ThemeCircleButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int pad = IsSelected ? 3 : 6;
        var circleRect = new Rectangle(pad, pad, Width - pad * 2, Height - pad * 2);

        // Fill circle with theme primary color
        using var brush = new SolidBrush(Theme.Primary);
        g.FillEllipse(brush, circleRect);

        // Selection ring
        if (IsSelected)
        {
            using var pen = new Pen(Theme.Primary, 2.5f);
            g.DrawEllipse(pen, 1, 1, Width - 3, Height - 3);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Invalidate();
    }
}
