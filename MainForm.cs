using System.Drawing.Drawing2D;

namespace Typesetterer3;

internal sealed class MainForm : Form
{
    private static readonly Color AppBlack = Color.FromArgb(8, 10, 14);
    private static readonly Color PanelBlack = Color.FromArgb(14, 17, 23);
    private static readonly Color RowBlack = Color.FromArgb(18, 22, 29);
    private static readonly Color BorderBlack = Color.FromArgb(35, 42, 54);
    private static readonly Color AccentBlue = Color.FromArgb(30, 96, 230);
    private static readonly Color TextWhite = Color.FromArgb(235, 239, 246);
    private static readonly Color TextMuted = Color.FromArgb(130, 142, 164);

    private readonly HotkeyService _hotkeys = new();
    private readonly ListBox _lineList = new();
    private readonly Label _progressLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _clipboardButton = new();
    private readonly Button _fileButton = new();
    private readonly Button _saveSlot1Button = new();
    private readonly Button _saveSlot2Button = new();
    private readonly TextBox _tagsTextBox = new();
    private readonly ToolTip _toolTip = new();
    private readonly List<ScriptLine> _lines = [];

    private int _currentIndex = -1;
    private string _currentScriptName = "Untitled";

    public MainForm()
    {
        Text = "Typesetterer 3.0";
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        MinimumSize = new Size(300, 210);
        Size = new Size(390, 285);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        Font = new Font("Segoe UI", 9F);
        BackColor = AppBlack;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;

        RestoreWindowBounds();
        BuildUi();
        WireEvents();
        UpdateState();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _hotkeys.StartPasteShortcutMonitor();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeys.Dispose();
            _lineList.Dispose();
            _progressLabel.Dispose();
            _statusLabel.Dispose();
            _clipboardButton.Dispose();
            _fileButton.Dispose();
            _saveSlot1Button.Dispose();
            _saveSlot2Button.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        WindowSettings.Save(bounds);
        base.OnFormClosing(e);
    }

    private void RestoreWindowBounds()
    {
        var settings = WindowSettings.Load();
        if (settings is null) return;

        var bounds = settings.Bounds;
        if (bounds.Width < MinimumSize.Width || bounds.Height < MinimumSize.Height) return;
        if (!Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds))) return;

        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(7),
            BackColor = BackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var topButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 4),
            AutoSize = true,
            BackColor = AppBlack,
        };
        topButtons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topButtons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topButtons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topButtons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        StyleTopButton(_clipboardButton, "CLIP");
        StyleTopButton(_fileButton, "FILE");
        StyleTopButton(_saveSlot1Button, "S1");
        StyleTopButton(_saveSlot2Button, "S2");
        _toolTip.SetToolTip(_clipboardButton, "Load formatted text from clipboard");
        _toolTip.SetToolTip(_fileButton, "Load and format a script file");
        _toolTip.SetToolTip(_saveSlot1Button, "Left-click restore project slot 1. Right-click save current script and line.");
        _toolTip.SetToolTip(_saveSlot2Button, "Left-click restore project slot 2. Right-click save current script and line.");
        topButtons.Controls.Add(_clipboardButton, 0, 0);
        topButtons.Controls.Add(_fileButton, 1, 0);
        topButtons.Controls.Add(_saveSlot1Button, 2, 0);
        topButtons.Controls.Add(_saveSlot2Button, 3, 0);

        _tagsTextBox.Dock = DockStyle.Top;
        _tagsTextBox.BackColor = RowBlack;
        _tagsTextBox.ForeColor = TextMuted;
        _tagsTextBox.BorderStyle = BorderStyle.FixedSingle;
        _tagsTextBox.Font = new Font("Segoe UI", 8F);
        _tagsTextBox.Text = WindowSettings.GetIgnoredTags();
        _toolTip.SetToolTip(_tagsTextBox, "Ignored tags (comma-separated). Text before first space is ignored if it matches a tag.");

        var strip = new Panel
        {
            Dock = DockStyle.Top,
            Height = 22,
            Margin = new Padding(0, 0, 0, 4),
            BackColor = AppBlack,
        };
        _progressLabel.TextAlign = ContentAlignment.MiddleCenter;
        _progressLabel.Dock = DockStyle.Fill;
        _progressLabel.Font = new Font(Font, FontStyle.Bold);
        _progressLabel.ForeColor = TextWhite;
        _progressLabel.BackColor = AppBlack;
        strip.Controls.Add(_progressLabel);

        _lineList.Dock = DockStyle.Fill;
        _lineList.BorderStyle = BorderStyle.None;
        _lineList.DrawMode = DrawMode.OwnerDrawFixed;
        _lineList.ItemHeight = 24;
        _lineList.IntegralHeight = false;
        _lineList.BackColor = PanelBlack;
        _lineList.ForeColor = TextWhite;
        _lineList.HorizontalScrollbar = false;

        _statusLabel.Dock = DockStyle.Bottom;
        _statusLabel.Height = 19;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = TextMuted;
        _statusLabel.BackColor = AppBlack;

        root.Controls.Add(topButtons, 0, 0);
        root.Controls.Add(_tagsTextBox, 0, 1);
        root.Controls.Add(strip, 0, 2);
        root.Controls.Add(_lineList, 0, 3);
        root.Controls.Add(_statusLabel, 0, 4);
        Controls.Add(root);
    }

    private static void StyleTopButton(Button button, string text)
    {
        button.Text = text;
        button.Dock = DockStyle.Left;
        button.Width = 48;
        button.Height = 22;
        button.Margin = new Padding(0, 0, 5, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = BorderBlack;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(24, 30, 42);
        button.FlatAppearance.MouseDownBackColor = AccentBlue;
        button.BackColor = PanelBlack;
        button.ForeColor = TextMuted;
        button.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        button.TextAlign = ContentAlignment.MiddleCenter;
    }

    private void WireEvents()
    {
        _clipboardButton.Click += (_, _) => LoadFromClipboard();
        _fileButton.Click += (_, _) => LoadFromFile();
        _saveSlot1Button.MouseUp += (_, e) => HandleSaveSlotMouseUp(1, e);
        _saveSlot2Button.MouseUp += (_, e) => HandleSaveSlotMouseUp(2, e);
        _lineList.DrawItem += DrawLineItem;
        _lineList.MouseDown += (_, e) =>
        {
            var index = _lineList.IndexFromPoint(e.Location);
            if (index >= 0 && index < _lines.Count && _lines[index].Pasteable)
            {
                SelectLine(index, $"Line {ReadableLineNumber(index)} copied.");
            }
        };

        _tagsTextBox.TextChanged += (_, _) => WindowSettings.SaveIgnoredTags(_tagsTextBox.Text);

        _hotkeys.PasteShortcutObserved += (_, _) => BeginInvoke(new Action(() => _ = HandlePasteSequenceAsync()));
    }

    private void LoadFromClipboard()
    {
        if (!Clipboard.ContainsText(TextDataFormat.UnicodeText) && !Clipboard.ContainsText(TextDataFormat.Text))
        {
            UpdateState("Clipboard does not contain text.");
            return;
        }

        var text = Clipboard.GetText(TextDataFormat.UnicodeText);
        LoadLines(ScriptParser.ParseText(text), "Clipboard", "Loaded clipboard text.");
    }

    private void LoadFromFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select script file",
            Filter = "Script files (*.txt;*.md;*.docx)|*.txt;*.md;*.docx|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var fileName = Path.GetFileName(dialog.FileName);
            LoadLines(ScriptParser.ParseFile(dialog.FileName), fileName, $"Loaded {fileName}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unable to load file", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadLines(List<ScriptLine> lines, string scriptName, string status)
    {
        _lines.Clear();
        _lines.AddRange(lines.Where(line => line.Pasteable));
        _currentScriptName = scriptName;
        _currentIndex = FindNextPasteable(-1);
        RefreshLineList();
        CopyCurrentLineToClipboard();
        UpdateState(status);
    }

    private async Task HandlePasteSequenceAsync()
    {
        if (_currentIndex < 0 || _currentIndex >= _lines.Count)
        {
            UpdateState("No current line to paste.");
            return;
        }

        // 1. Immediately copy current line again to ensure it's on clipboard
        CopyCurrentLineToClipboard();

        // 2. Trigger CTRL+V (if the user triggered this via hotkey, it might already be in flight, 
        // but the user requested the hotkey to "trigger" it).
        // SendWait helps ensure the OS processes it before we change the clipboard.
        SendKeys.SendWait("^v");

        // 3. Mark as pasted and update UI
        _lines[_currentIndex].IsPasted = true;
        
        await Task.Delay(200);

        if (!PhotoshopFocusService.IsPhotoshopForeground())
        {
            UpdateState($"Line {ReadableLineNumber(_currentIndex)} pasted.");
            return;
        }

        // 4. Advance and copy NEXT line
        _currentIndex = FindNextPasteable(_currentIndex);
        CopyCurrentLineToClipboard();
        UpdateState(_currentIndex < 0 ? "End of script." : $"Line {ReadableLineNumber(_currentIndex)} copied.");
    }

    private void SelectLine(int index, string status)
    {
        _currentIndex = index;
        _lineList.SelectedIndex = index;
        CopyCurrentLineToClipboard();
        UpdateState(status);
    }

    private void CopyCurrentLineToClipboard()
    {
        if (_currentIndex < 0 || _currentIndex >= _lines.Count) return;
        PasteService.CopyText(_lines[_currentIndex].CleanText);
    }

    private void HandleSaveSlotMouseUp(int slot, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            SaveCurrentSlot(slot);
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            RestoreSaveSlot(slot);
        }
    }

    private void SaveCurrentSlot(int slot)
    {
        if (_currentIndex < 0 || _currentIndex >= _lines.Count)
        {
            UpdateState("Load a script before saving a slot.");
            return;
        }

        var saveSlot = new ScriptSaveSlot
        {
            CurrentIndex = _currentIndex,
            Name = _currentScriptName,
            SavedAt = DateTime.UtcNow,
            Lines = _lines.Select(line => new SavedLine { RawText = line.RawText, IsPasted = line.IsPasted }).ToList(),
        };

        WindowSettings.SaveSlot(slot, saveSlot);
        UpdateState($"Saved {_currentScriptName} to slot {slot} at line {ReadableLineNumber(_currentIndex)}.");
    }

    private void RestoreSaveSlot(int slot)
    {
        var saveSlot = WindowSettings.GetSaveSlot(slot);
        if (saveSlot is null || saveSlot.Lines.Count == 0)
        {
            UpdateState($"Slot {slot} is empty.");
            return;
        }

        if (saveSlot.CurrentIndex < 0 || saveSlot.CurrentIndex >= saveSlot.Lines.Count)
        {
            UpdateState($"Slot {slot} is invalid.");
            return;
        }

        _lines.Clear();
        foreach (var saved in saveSlot.Lines)
        {
            _lines.Add(new ScriptLine
            {
                SourceNumber = 0, // Not strictly needed for restore
                RawText = saved.RawText,
                CleanText = ScriptParser.CleanSingleLine(saved.RawText),
                IsPasted = saved.IsPasted,
                Pasteable = true
            });
        }
        _currentScriptName = string.IsNullOrWhiteSpace(saveSlot.Name) ? $"Slot {slot}" : saveSlot.Name;
        _currentIndex = saveSlot.CurrentIndex;
        RefreshLineList();
        CopyCurrentLineToClipboard();
        UpdateState($"Restored {_currentScriptName} from slot {slot}.");
    }

    private int FindNextPasteable(int afterIndex)
    {
        for (var i = afterIndex + 1; i < _lines.Count; i++)
        {
            if (_lines[i].Pasteable) return i;
        }

        return -1;
    }

    private int ReadableLineNumber(int index)
    {
        if (index < 0) return 0;
        var count = 0;
        for (var i = 0; i <= index && i < _lines.Count; i++)
        {
            if (_lines[i].Pasteable) count++;
        }

        return count;
    }

    private int PasteableCount()
    {
        return _lines.Count(line => line.Pasteable);
    }

    private void RefreshLineList()
    {
        _lineList.BeginUpdate();
        _lineList.Items.Clear();
        foreach (var line in _lines)
        {
            _lineList.Items.Add(line);
        }

        _lineList.EndUpdate();
    }

    private void UpdateState(string? status = null)
    {
        var total = PasteableCount();
        if (total == 0)
        {
            _progressLabel.Text = "No script loaded";
        }
        else if (_currentIndex < 0)
        {
            _progressLabel.Text = $"Done - {total} lines";
        }
        else
        {
            _progressLabel.Text = $"Line {ReadableLineNumber(_currentIndex)} of {total}";
            _lineList.SelectedIndex = _currentIndex;
        }

        _statusLabel.Text = status ?? (_currentIndex < 0 && total > 0 ? "End of script." : "Press Ctrl+V in Photoshop.");
        _lineList.Invalidate();
    }

    private void DrawLineItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _lines.Count) return;

        var line = _lines[e.Index];
        var isCurrent = e.Index == _currentIndex;
        var backColor = isCurrent ? AccentBlue : (e.Index % 2 == 0 ? PanelBlack : RowBlack);
        var foreColor = isCurrent ? Color.White : TextWhite;

        using var background = new SolidBrush(backColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        e.Graphics.SmoothingMode = SmoothingMode.None;

        var textRect = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height);
        var displayColor = line.IsPasted && !isCurrent ? TextMuted : foreColor;
        TextRenderer.DrawText(e.Graphics, line.RawText, Font, textRect, displayColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        using var divider = new Pen(isCurrent ? Color.FromArgb(92, 146, 245) : BorderBlack);
        e.Graphics.DrawLine(divider, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }
}
