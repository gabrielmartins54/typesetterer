using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velopack;

namespace Typesetterer3;

public class UpdateDialog : Form
{
    private static readonly Color AppBlack = Color.FromArgb(17, 18, 19);
    private static readonly Color RowBlack = Color.FromArgb(24, 25, 26);
    private static readonly Color AccentBlue = Color.FromArgb(33, 150, 243);
    private static readonly Color TextWhite = Color.FromArgb(240, 240, 240);

    private readonly UpdateManager _manager;
    private readonly UpdateInfo _updateInfo;

    private Label _lblDescription = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblProgress = null!;
    private Button _btnYes = null!;
    private Button _btnNo = null!;

    public UpdateDialog(UpdateManager manager, UpdateInfo updateInfo)
    {
        _manager = manager;
        _updateInfo = updateInfo;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Size = new Size(420, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = AppBlack;
        Text = "Software Update";
        ShowIcon = false;

        _lblDescription = new Label
        {
            Text = $"A new version (v{_updateInfo.TargetFullRelease.Version}) is available.\nWould you like to install it now?",
            ForeColor = TextWhite,
            Location = new Point(20, 25),
            Size = new Size(380, 50),
            Font = new Font("Segoe UI", 10F, FontStyle.Regular)
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(20, 80),
            Size = new Size(380, 20),
            Minimum = 0,
            Maximum = 100,
            Visible = false
        };

        _lblProgress = new Label
        {
            Text = "0%",
            ForeColor = TextWhite,
            Location = new Point(20, 110),
            Size = new Size(380, 20),
            TextAlign = ContentAlignment.TopCenter,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Visible = false
        };

        _btnYes = new Button
        {
            Text = "Yes",
            ForeColor = TextWhite,
            BackColor = RowBlack,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(210, 110),
            Size = new Size(90, 32),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        _btnYes.FlatAppearance.BorderSize = 0;
        _btnYes.Click += async (s, e) => await StartDownloadAsync();

        _btnNo = new Button
        {
            Text = "No",
            ForeColor = TextWhite,
            BackColor = RowBlack,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(310, 110),
            Size = new Size(90, 32),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        _btnNo.FlatAppearance.BorderSize = 0;
        _btnNo.Click += (s, e) => Close();

        Controls.Add(_lblDescription);
        Controls.Add(_progressBar);
        Controls.Add(_lblProgress);
        Controls.Add(_btnYes);
        Controls.Add(_btnNo);
    }

    private async Task StartDownloadAsync()
    {
        // Switch dialog layout to downloading mode
        _btnYes.Visible = false;
        _btnNo.Visible = false;
        _lblDescription.Text = "Downloading update, please wait...";
        _progressBar.Visible = true;
        _lblProgress.Visible = true;

        try
        {
            // Download update files with progress updates
            await _manager.DownloadUpdatesAsync(_updateInfo, progress =>
            {
                if (IsHandleCreated)
                {
                    Invoke(new Action(() =>
                    {
                        _progressBar.Value = progress;
                        _lblProgress.Text = $"{progress}%";
                    }));
                }
            });

            _lblDescription.Text = "Applying update and restarting...";
            await Task.Delay(1000);

            // Apply updates and restart application
            _manager.ApplyUpdatesAndRestart(_updateInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to download update: {ex.Message}", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }
}
