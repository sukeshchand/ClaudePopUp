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

        // === Update Section ===
        var updateSectionLabel = new Label
        {
            Text = "UPDATES",
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = currentTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(leftMargin, y),
            BackColor = Color.Transparent,
        };
        Controls.Add(updateSectionLabel);
        y += 24;

        var updatePathLabel = new Label
        {
            Text = "Update location (network path):",
            Font = new Font("Segoe UI", 9f),
            ForeColor = currentTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(leftMargin, y),
            BackColor = Color.Transparent,
        };
        Controls.Add(updatePathLabel);
        y += 22;

        var updatePathBox = new TextBox
        {
            Text = AppSettings.Load().UpdateLocation,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = currentTheme.TextPrimary,
            BackColor = currentTheme.BgHeader,
            BorderStyle = BorderStyle.FixedSingle,
            Size = new Size(contentWidth - 40, 26),
            Location = new Point(leftMargin, y),
        };
        updatePathBox.LostFocus += (_, _) =>
        {
            var settings = AppSettings.Load();
            AppSettings.Save(settings with { UpdateLocation = updatePathBox.Text.Trim() });
        };
        Controls.Add(updatePathBox);

        var savePathButton = new RoundedButton
        {
            Text = "Save",
            Font = new Font("Segoe UI", 8.5f),
            Size = new Size(34, 26),
            Location = new Point(leftMargin + contentWidth - 34, y),
            FlatStyle = FlatStyle.Flat,
            BackColor = currentTheme.PrimaryDim,
            ForeColor = currentTheme.TextSecondary,
            Cursor = Cursors.Hand,
        };
        savePathButton.FlatAppearance.BorderSize = 0;
        savePathButton.FlatAppearance.MouseOverBackColor = currentTheme.Primary;
        savePathButton.Click += (_, _) =>
        {
            var settings = AppSettings.Load();
            AppSettings.Save(settings with { UpdateLocation = updatePathBox.Text.Trim() });
            savePathButton.Text = "\u2713";
            var resetTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            resetTimer.Tick += (_, _) => { savePathButton.Text = "Save"; resetTimer.Stop(); resetTimer.Dispose(); };
            resetTimer.Start();
        };
        Controls.Add(savePathButton);
        y += 34;

        var checkNowButton = new RoundedButton
        {
            Text = "Check for Updates",
            Font = new Font("Segoe UI", 8.5f),
            Size = new Size(140, 28),
            Location = new Point(leftMargin, y),
            FlatStyle = FlatStyle.Flat,
            BackColor = currentTheme.PrimaryDim,
            ForeColor = currentTheme.TextSecondary,
            Cursor = Cursors.Hand,
        };
        checkNowButton.FlatAppearance.BorderSize = 0;
        checkNowButton.FlatAppearance.MouseOverBackColor = currentTheme.Primary;

        var checkResultLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = currentTheme.TextSecondary,
            AutoSize = true,
            Location = new Point(leftMargin + 150, y + 5),
            BackColor = Color.Transparent,
        };

        var updateLinkLabel = new Label
        {
            Text = "Install Update",
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Underline | FontStyle.Bold),
            ForeColor = currentTheme.Primary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Visible = false,
        };
        updateLinkLabel.Click += (_, _) =>
        {
            updateLinkLabel.Text = "Updating...";
            updateLinkLabel.Enabled = false;
            var result = Updater.Apply();
            if (result.Success)
            {
                Application.Exit();
            }
            else
            {
                MessageBox.Show(this, result.Message, "Update Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                updateLinkLabel.Text = "Install Update";
                updateLinkLabel.Enabled = true;
            }
        };

        checkNowButton.Click += (_, _) =>
        {
            // Save the path first
            var settings = AppSettings.Load();
            AppSettings.Save(settings with { UpdateLocation = updatePathBox.Text.Trim() });

            checkNowButton.Enabled = false;
            checkNowButton.Text = "Checking...";
            checkResultLabel.Text = "";
            updateLinkLabel.Visible = false;

            UpdateChecker.CheckNow();
            var meta = UpdateChecker.LatestMetadata;
            if (meta != null)
            {
                checkResultLabel.ForeColor = currentTheme.SuccessColor;
                checkResultLabel.Text = $"v{meta.Version} available";
                updateLinkLabel.Visible = true;
                updateLinkLabel.Location = new Point(
                    checkResultLabel.Left + checkResultLabel.PreferredWidth + 10,
                    checkResultLabel.Top);
            }
            else
            {
                checkResultLabel.ForeColor = currentTheme.TextSecondary;
                checkResultLabel.Text = "You are up to date.";
            }

            checkNowButton.Enabled = true;
            checkNowButton.Text = "Check for Updates";
        };
        Controls.Add(checkNowButton);
        Controls.Add(checkResultLabel);
        Controls.Add(updateLinkLabel);
        y += 40;

        // --- Separator ---
        var sep3 = new Panel
        {
            BackColor = currentTheme.Border,
            Location = new Point(leftMargin, y),
            Size = new Size(contentWidth, 1),
        };
        Controls.Add(sep3);
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
            Text = $"ClaudePopup v{AppVersion.Current}",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(60, 75, 105),
            AutoSize = true,
            Location = new Point(leftMargin, ClientSize.Height - 26),
            BackColor = Color.Transparent,
        };
        Controls.Add(versionLabel);

        // Adjust form height to fit content
        ClientSize = new Size(ClientSize.Width, y + 38 + 40);
        versionLabel.Location = new Point(leftMargin, ClientSize.Height - 26);
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
