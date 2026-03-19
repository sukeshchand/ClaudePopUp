using System.Drawing;

namespace ClaudePopup;

enum FilterMode { None, Cwd, Session }

class FilterDialog : Form
{
    private readonly PopupTheme _theme;
    private readonly RadioButton _rbCwd;
    private readonly RadioButton _rbSession;
    private readonly ListBox _listBox;
    private readonly RoundedButton _okButton;
    private readonly RoundedButton _clearButton;

    private List<string> _cwdValues = new();
    private List<(string SessionId, string Cwd)> _sessionValues = new();

    public FilterMode SelectedMode { get; private set; } = FilterMode.None;
    public string SelectedValue { get; private set; } = "";

    public FilterDialog(PopupTheme theme, FilterMode currentMode, string currentValue)
    {
        _theme = theme;
        Text = "Filter History";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = theme.BgDark;
        ForeColor = theme.TextPrimary;
        ClientSize = new Size(380, 360);
        Font = new Font("Segoe UI", 10f);

        var groupLabel = new Label
        {
            Text = "Group by:",
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            ForeColor = theme.TextPrimary,
            AutoSize = true,
            Location = new Point(16, 14),
            BackColor = Color.Transparent,
        };

        _rbCwd = new RadioButton
        {
            Text = "Working Folder",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(16, 40),
            Checked = currentMode == FilterMode.Cwd,
        };
        _rbCwd.CheckedChanged += (_, _) => { if (_rbCwd.Checked) PopulateList(FilterMode.Cwd); };

        _rbSession = new RadioButton
        {
            Text = "Session",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(180, 40),
            Checked = currentMode == FilterMode.Session,
        };
        _rbSession.CheckedChanged += (_, _) => { if (_rbSession.Checked) PopulateList(FilterMode.Session); };

        var listLabel = new Label
        {
            Text = "Select:",
            Font = new Font("Segoe UI", 9f),
            ForeColor = theme.TextSecondary,
            AutoSize = true,
            Location = new Point(16, 70),
            BackColor = Color.Transparent,
        };

        _listBox = new ListBox
        {
            Location = new Point(16, 92),
            Size = new Size(348, 190),
            Font = new Font("Cascadia Code", 9f),
            BackColor = theme.BgHeader,
            ForeColor = theme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
        };
        _listBox.DoubleClick += (_, _) => AcceptSelection();

        _okButton = new RoundedButton
        {
            Text = "OK",
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            Size = new Size(100, 36),
            Location = new Point(160, 300),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.Primary,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
        };
        _okButton.FlatAppearance.BorderSize = 0;
        _okButton.FlatAppearance.MouseOverBackColor = theme.PrimaryLight;
        _okButton.Click += (_, _) => AcceptSelection();

        _clearButton = new RoundedButton
        {
            Text = "Clear Filter",
            Font = new Font("Segoe UI", 9.5f),
            Size = new Size(100, 36),
            Location = new Point(268, 300),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.PrimaryDim,
            ForeColor = theme.TextSecondary,
            Cursor = Cursors.Hand,
        };
        _clearButton.FlatAppearance.BorderSize = 0;
        _clearButton.FlatAppearance.MouseOverBackColor = theme.Primary;
        _clearButton.Click += (_, _) =>
        {
            SelectedMode = FilterMode.None;
            SelectedValue = "";
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange(new Control[] { groupLabel, _rbCwd, _rbSession, listLabel, _listBox, _okButton, _clearButton });

        // Load data and populate
        ResponseHistory.Invalidate();
        _cwdValues = ResponseHistory.GetTodayDistinctCwd();
        _sessionValues = ResponseHistory.GetTodayDistinctSessions();

        if (currentMode == FilterMode.Cwd)
            PopulateList(FilterMode.Cwd, currentValue);
        else if (currentMode == FilterMode.Session)
            PopulateList(FilterMode.Session, currentValue);
        else if (_cwdValues.Count > 0)
        {
            _rbCwd.Checked = true;
            PopulateList(FilterMode.Cwd);
        }
        else if (_sessionValues.Count > 0)
        {
            _rbSession.Checked = true;
            PopulateList(FilterMode.Session);
        }
    }

    private void PopulateList(FilterMode mode, string? selectValue = null)
    {
        _listBox.Items.Clear();

        if (mode == FilterMode.Cwd)
        {
            foreach (var cwd in _cwdValues)
            {
                string folder = Path.GetFileName(cwd.TrimEnd('/', '\\'));
                if (string.IsNullOrEmpty(folder)) folder = cwd;
                int count = ResponseHistory.FilterTodayByCwd(cwd).Count;
                _listBox.Items.Add(new FilterItem(folder, cwd, count));
            }
        }
        else if (mode == FilterMode.Session)
        {
            foreach (var (sessionId, cwd) in _sessionValues)
            {
                string shortId = sessionId.Length > 8 ? sessionId[..8] : sessionId;
                string folder = string.IsNullOrEmpty(cwd) ? "" : Path.GetFileName(cwd.TrimEnd('/', '\\'));
                string display = string.IsNullOrEmpty(folder) ? shortId : $"{shortId} ({folder})";
                int count = ResponseHistory.FilterTodayBySession(sessionId).Count;
                _listBox.Items.Add(new FilterItem(display, sessionId, count));
            }
        }

        // Try to re-select the current value
        if (selectValue != null)
        {
            for (int i = 0; i < _listBox.Items.Count; i++)
            {
                if (_listBox.Items[i] is FilterItem fi && fi.Value == selectValue)
                {
                    _listBox.SelectedIndex = i;
                    break;
                }
            }
        }

        if (_listBox.SelectedIndex < 0 && _listBox.Items.Count > 0)
            _listBox.SelectedIndex = 0;
    }

    private void AcceptSelection()
    {
        if (_listBox.SelectedItem is FilterItem fi)
        {
            SelectedMode = _rbCwd.Checked ? FilterMode.Cwd : FilterMode.Session;
            SelectedValue = fi.Value;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private record FilterItem(string Display, string Value, int Count)
    {
        public override string ToString() => $"{Display}  ({Count})";
    }
}
