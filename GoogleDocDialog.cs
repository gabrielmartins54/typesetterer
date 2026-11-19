using System.Drawing;
using System.Windows.Forms;

namespace Typesetterer3;

internal sealed class GoogleDocDialog : Form
{
    private static readonly Color AppBlack = Color.FromArgb(8, 10, 14);
    private static readonly Color PanelBlack = Color.FromArgb(14, 17, 23);
    private static readonly Color AccentBlue = Color.FromArgb(30, 96, 230);
    private static readonly Color TextWhite = Color.FromArgb(235, 239, 246);
    private static readonly Color TextMuted = Color.FromArgb(130, 142, 164);
    private static readonly Color RedAccent = Color.FromArgb(220, 53, 69);

    private readonly TextBox _urlTextBox;
    private readonly Label _statusLabel;
    private readonly Button _btnStart;
    private readonly Button _btnStop;
    private readonly Button _btnClose;

    public string Url { get; private set; } = string.Empty;
    public bool StopRequested { get; private set; }

    public GoogleDocDialog(string currentUrl, bool isSyncing)
    {
        Width = 460;
        Height = 230;
        Text = "[Beta] Google Docs Live Sync";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        BackColor = AppBlack;
        ForeColor = TextWhite;
        TopMost = true;
        KeyPreview = true;

        var descLabel = new Label
        {
            Text = "Paste Google Docs link below. Script updates in real-time as text is added in Google Docs.\n(Doc must be shared as 'Anyone with the link can view').",
            Location = new Point(16, 12),
            Width = 415,
            Height = 42,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8.5F),
        };

        _urlTextBox = new TextBox
        {
            Location = new Point(16, 62),
            Width = 415,
            Height = 25,
            BackColor = PanelBlack,
            ForeColor = TextWhite,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5F),
            Text = string.IsNullOrWhiteSpace(currentUrl) ? WindowSettings.GetLastGoogleDocUrl() : currentUrl,
        };

        _statusLabel = new Label
        {
            Location = new Point(16, 95),
            Width = 415,
            Height = 35,
            ForeColor = isSyncing ? Color.FromArgb(76, 175, 80) : TextMuted,
            Text = isSyncing ? "🟢 Live Sync active (Polling Google Docs...)" : "Status: Not connected",
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
        };

        _btnStart = new Button
        {
            Text = isSyncing ? "Update Link" : "Start Live Sync",
            Location = new Point(145, 142),
            Width = 130,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        _btnStart.FlatAppearance.BorderSize = 0;

        _btnStop = new Button
        {
            Text = "Stop Sync",
            Location = new Point(283, 142),
            Width = 90,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = RedAccent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = isSyncing,
        };
        _btnStop.FlatAppearance.BorderSize = 0;

        _btnClose = new Button
        {
            Text = "Close",
            Location = new Point(380, 142),
            Width = 51,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = PanelBlack,
            ForeColor = TextWhite,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand,
        };
        _btnClose.FlatAppearance.BorderSize = 0;

        Controls.Add(descLabel);
        Controls.Add(_urlTextBox);
        Controls.Add(_statusLabel);
        Controls.Add(_btnStart);
        Controls.Add(_btnStop);
        Controls.Add(_btnClose);

        _btnStart.Click += (_, _) =>
        {
            var text = _urlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show(this, "Please enter a valid Google Docs URL.", "Invalid URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var exportUrl = GoogleDocsService.TryGetExportUrl(text, out _);
            if (string.IsNullOrEmpty(exportUrl))
            {
                MessageBox.Show(this, "Unrecognized Google Docs URL. Make sure it contains '/document/d/...' or '/document/d/e/.../pub'.", "Invalid URL Format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Url = text;
            WindowSettings.SaveLastGoogleDocUrl(text);
            DialogResult = DialogResult.OK;
        };

        _btnStop.Click += (_, _) =>
        {
            StopRequested = true;
            DialogResult = DialogResult.OK;
        };

        _btnClose.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
        };
    }
}
