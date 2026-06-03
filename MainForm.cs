using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Velopack;
using Velopack.Sources;

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
    private readonly ToolStripLabel _progressLabel = new();
    private readonly Label _statusLabel = new();
    private readonly ToolStripMenuItem _clipMenuItem = new("Paste from clipboard");
    private readonly ToolStripMenuItem _fileMenuItem = new("Open file");
    private readonly ToolStripMenuItem _saveS1MenuItem = new("Save work in slot 1");
    private readonly ToolStripMenuItem _saveS2MenuItem = new("Save work in slot 2");
    private readonly ToolStripMenuItem _loadS1MenuItem = new("Load slot 1");
    private readonly ToolStripMenuItem _loadS2MenuItem = new("Load slot 2");
    private readonly ToolStripMenuItem _tagsMenuItem = new("Tags");
    private readonly ToolStripMenuItem _keyMenuItem = new("Pasting hotkey");
    private readonly ToolStripMenuItem _scriptKeyMenuItem = new("Script hotkey");
    private readonly MenuStrip _menuStrip = new();
    private readonly Panel _titleBar = new();
    private readonly ToolTip _toolTip = new();
    private readonly List<ScriptLine> _lines = [];

    private readonly System.Windows.Forms.Timer _photoshopOwnerTimer = new();
    private IntPtr _lastPhotoshopHandle = IntPtr.Zero;
    private bool _hiddenByPhotoshopState;
    private Rectangle? _normalBounds;
    private FormWindowState _normalState = FormWindowState.Normal;

    private int _currentIndex = -1;
    private string _currentScriptName = "Untitled";

    public MainForm()
    {
        Text = "Typesetterer 3.0";
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        MinimumSize = new Size(150, 100);
        Size = new Size(390, 285);
        FormBorderStyle = FormBorderStyle.Sizable;
        Font = new Font("Segoe UI", 9F);
        BackColor = AppBlack;
        Padding = new Padding(3); // Expose edges for resizing
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;

        RestoreWindowBounds();
        BuildUi();
        WireEvents();
        UpdateState();
        StartPhotoshopOwnerTimer();
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
            _photoshopOwnerTimer.Dispose();
            _hotkeys.Dispose();
            _lineList.Dispose();
            _progressLabel.Dispose();
            _statusLabel.Dispose();
            _menuStrip.Dispose();
            _titleBar.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _ = Task.Run(() => CheckForUpdatesAsync());
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource("https://github.com/gabrielmartins54/typesetterer", null, false));
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion == null) return;

            var updateInfo = newVersion;

            if (IsHandleCreated)
            {
                Invoke(new Action(() =>
                {
                    using var dialog = new UpdateDialog(mgr, updateInfo);
                    dialog.ShowDialog(this);
                }));
            }
        }
        catch
        {
            // Silently fail if no internet or error
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _photoshopOwnerTimer.Stop();
        if (Handle != IntPtr.Zero)
        {
            SetWindowOwner(Handle, IntPtr.Zero);
        }
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        if (_hiddenByPhotoshopState && _normalBounds.HasValue)
        {
            bounds = _normalBounds.Value;
        }
        WindowSettings.Save(bounds);
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCCALCSIZE = 0x0083;
        const int WM_NCHITTEST = 0x0084;
        
        if (m.Msg == WM_NCCALCSIZE && m.WParam.ToInt32() == 1)
        {
            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);

        if (m.Msg == WM_NCHITTEST && (int)m.Result == 1) // HTCLIENT
        {
            var screenPoint = new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16);
            var clientPoint = PointToClient(screenPoint);
            const int border = 10;
            
            if (clientPoint.X >= ClientSize.Width - border && clientPoint.Y >= ClientSize.Height - border)
                m.Result = (IntPtr)17; // HTBOTTOMRIGHT
            else if (clientPoint.X >= ClientSize.Width - border)
                m.Result = (IntPtr)11; // HTRIGHT
            else if (clientPoint.Y >= ClientSize.Height - border)
                m.Result = (IntPtr)15; // HTBOTTOM
        }
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
        _titleBar.Dock = DockStyle.Top;
        _titleBar.Height = 24;
        _titleBar.BackColor = RowBlack;
        
        var closeButton = new Button
        {
            Text = "X",
            Dock = DockStyle.Right,
            Width = 30,
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextMuted,
            BackColor = RowBlack,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold)
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.FlatAppearance.MouseOverBackColor = Color.DarkRed;
        closeButton.FlatAppearance.MouseDownBackColor = Color.Red;
        closeButton.Click += (_, _) => Close();
        
        _menuStrip.BackColor = RowBlack;
        _menuStrip.ForeColor = TextWhite;
        _menuStrip.Dock = DockStyle.Fill;
        _menuStrip.AutoSize = false;
        _menuStrip.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()) { RoundedEdges = false };

        var scriptMenu = new ToolStripMenuItem("Script");
        scriptMenu.DropDown.BackColor = PanelBlack;
        scriptMenu.DropDown.ForeColor = TextWhite;
        scriptMenu.DropDownItems.Add(_clipMenuItem);
        scriptMenu.DropDownItems.Add(_fileMenuItem);

        var stateMenu = new ToolStripMenuItem("State");
        stateMenu.DropDown.BackColor = PanelBlack;
        stateMenu.DropDown.ForeColor = TextWhite;
        stateMenu.DropDownItems.Add(_saveS1MenuItem);
        stateMenu.DropDownItems.Add(_saveS2MenuItem);
        stateMenu.DropDownItems.Add(_loadS1MenuItem);
        stateMenu.DropDownItems.Add(_loadS2MenuItem);

        var configMenu = new ToolStripMenuItem("Config");
        configMenu.DropDown.BackColor = PanelBlack;
        configMenu.DropDown.ForeColor = TextWhite;
        configMenu.DropDownItems.Add(_tagsMenuItem);
        configMenu.DropDownItems.Add(_keyMenuItem);
        configMenu.DropDownItems.Add(_scriptKeyMenuItem);

        _menuStrip.Items.Add(scriptMenu);
        _menuStrip.Items.Add(stateMenu);
        _menuStrip.Items.Add(configMenu);

        _progressLabel.Alignment = ToolStripItemAlignment.Right;
        _progressLabel.ForeColor = TextMuted;
        _progressLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _menuStrip.Items.Add(_progressLabel);



        _titleBar.Controls.Add(_menuStrip);
        _titleBar.Controls.Add(closeButton);

        _lineList.Dock = DockStyle.Fill;
        _lineList.BorderStyle = BorderStyle.None;
        _lineList.DrawMode = DrawMode.OwnerDrawFixed;
        _lineList.ItemHeight = 24;
        _lineList.IntegralHeight = false;
        _lineList.BackColor = AppBlack;
        _lineList.ForeColor = TextWhite;
        _lineList.HorizontalScrollbar = false;

        Controls.Add(_lineList);
        Controls.Add(_titleBar);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const int GWL_HWNDPARENT = -8;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static IntPtr SetWindowOwner(IntPtr child, IntPtr owner)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(child, GWL_HWNDPARENT, owner);
        else
            return SetWindowLong32(child, GWL_HWNDPARENT, owner);
    }

    private void WireTitleBarEvents()
    {
        void HandleDrag(object? s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        _titleBar.MouseDown += HandleDrag;
        _menuStrip.MouseDown += HandleDrag;
    }

    private void WireEvents()
    {
        WireTitleBarEvents();
        _clipMenuItem.Click += (_, _) => LoadFromClipboard();
        _fileMenuItem.Click += (_, _) => LoadFromFile();
        _saveS1MenuItem.Click += (_, _) => SaveCurrentSlot(1);
        _saveS2MenuItem.Click += (_, _) => SaveCurrentSlot(2);
        _loadS1MenuItem.Click += (_, _) => RestoreSaveSlot(1);
        _loadS2MenuItem.Click += (_, _) => RestoreSaveSlot(2);
        _tagsMenuItem.Click += (_, _) => PromptForTags();
        _keyMenuItem.Click += (_, _) => PromptForHotkey();
        _scriptKeyMenuItem.Click += (_, _) => PromptForScriptHotkey();
        _lineList.DrawItem += DrawLineItem;
        _lineList.MouseDown += (_, e) =>
        {
            var index = _lineList.IndexFromPoint(e.Location);
            if (index >= 0 && index < _lines.Count && _lines[index].Pasteable)
            {
                SelectLine(index, $"Line {ReadableLineNumber(index)} copied.");
            }
        };

        _hotkeys.PasteShortcutObserved += (_, _) => BeginInvoke(new Action(() => _ = HandlePasteSequenceAsync()));
        _hotkeys.ScriptShortcutObserved += (_, _) => BeginInvoke(new Action(RunBuiltInScript));
    }

    private void PromptForTags()
    {
        using var form = new Form
        {
            Width = 380,
            Height = 320,
            Text = "Manage Ignored Tags",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            BackColor = AppBlack,
            ForeColor = TextWhite,
            TopMost = true,
            KeyPreview = true,
        };

        var descLabel = new Label
        {
            Text = "Lines starting with these tags will have them stripped automatically.",
            Location = new Point(12, 10),
            Width = 340,
            Height = 35,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8.5F),
        };

        var flowPanel = new FlowLayoutPanel
        {
            Location = new Point(12, 50),
            Width = 340,
            Height = 170,
            BackColor = PanelBlack,
            AutoScroll = true,
            Padding = new Padding(6),
        };

        var addTextBox = new TextBox
        {
            Location = new Point(12, 232),
            Width = 250,
            Height = 25,
            BackColor = PanelBlack,
            ForeColor = TextWhite,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10F),
        };

        var btnAdd = new Button
        {
            Text = "Add",
            Location = new Point(272, 231),
            Width = 80,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        btnAdd.FlatAppearance.BorderSize = 0;

        form.Controls.Add(descLabel);
        form.Controls.Add(flowPanel);
        form.Controls.Add(addTextBox);
        form.Controls.Add(btnAdd);

        List<string> tagList = WindowSettings.GetIgnoredTags()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        void RefreshTags()
        {
            flowPanel.Controls.Clear();
            foreach (var tag in tagList)
            {
                var currentTag = tag;
                var pill = CreateTagPill(currentTag, () =>
                {
                    tagList.Remove(currentTag);
                    WindowSettings.SaveIgnoredTags(string.Join(",", tagList));
                    RefreshTags();
                });
                flowPanel.Controls.Add(pill);
            }
        }

        void AddCurrentTag()
        {
            var text = addTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (!tagList.Contains(text, StringComparer.OrdinalIgnoreCase))
            {
                tagList.Add(text);
                WindowSettings.SaveIgnoredTags(string.Join(",", tagList));
                RefreshTags();
            }
            addTextBox.Clear();
            addTextBox.Focus();
        }

        btnAdd.Click += (s, e) => AddCurrentTag();
        addTextBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddCurrentTag();
            }
        };

        form.Load += (s, e) => RefreshTags();
        form.ShowDialog(this);
    }

    private Panel CreateTagPill(string tag, Action onDelete)
    {
        var pill = new Panel
        {
            BackColor = RowBlack,
            Height = 28,
            Margin = new Padding(4),
            Padding = new Padding(6, 2, 6, 2),
        };

        var label = new Label
        {
            Text = tag,
            ForeColor = TextWhite,
            AutoSize = true,
            Location = new Point(6, 6),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        };

        var deleteBtn = new Button
        {
            Text = "×",
            ForeColor = Color.FromArgb(239, 83, 80), // Soft red
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Width = 18,
            Height = 18,
            Location = new Point(label.Right + 4, 5),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        deleteBtn.FlatAppearance.BorderSize = 0;
        deleteBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 239, 83, 80);
        deleteBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 239, 83, 80);
        deleteBtn.Click += (s, e) => onDelete();

        pill.Controls.Add(label);
        pill.Controls.Add(deleteBtn);
        
        pill.Width = label.PreferredWidth + 32;
        deleteBtn.Left = label.Right + 2;

        return pill;
    }

    private static string FormatHotkey(Keys key, bool ctrl, bool shift, bool alt)
    {
        if (key == Keys.None) return "None";
        var parts = new List<string>();
        if (ctrl) parts.Add("CTRL");
        if (shift) parts.Add("SHIFT");
        if (alt) parts.Add("ALT");
        parts.Add(key.ToString().ToUpperInvariant());
        return string.Join(" + ", parts);
    }

    private void PromptForHotkey()
    {
        var currentKey = (Keys)WindowSettings.GetCustomHotkey();
        var requiresCtrl = WindowSettings.GetHotkeyRequiresCtrl();
        var requiresShift = WindowSettings.GetHotkeyRequiresShift();
        var requiresAlt = WindowSettings.GetHotkeyRequiresAlt();
        var currentText = FormatHotkey(currentKey, requiresCtrl, requiresShift, requiresAlt);
        
        using var form = new HotkeyCaptureForm("Set Custom Hotkey", currentText);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            WindowSettings.SaveCustomHotkey((int)form.SelectedKey, form.SelectedControl, form.SelectedShift, form.SelectedAlt);
            UpdateState($"Hotkey changed to {FormatHotkey(form.SelectedKey, form.SelectedControl, form.SelectedShift, form.SelectedAlt)}");
        }
    }

    private void PromptForScriptHotkey()
    {
        var currentKey = (Keys)WindowSettings.GetScriptHotkey();
        var requiresCtrl = WindowSettings.GetScriptHotkeyRequiresCtrl();
        var requiresShift = WindowSettings.GetScriptHotkeyRequiresShift();
        var requiresAlt = WindowSettings.GetScriptHotkeyRequiresAlt();
        var currentText = FormatHotkey(currentKey, requiresCtrl, requiresShift, requiresAlt);
        
        using var form = new HotkeyCaptureForm("Set Script Hotkey", currentText);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            WindowSettings.SaveScriptHotkey((int)form.SelectedKey, form.SelectedControl, form.SelectedShift, form.SelectedAlt);
            UpdateState($"Script hotkey changed to {FormatHotkey(form.SelectedKey, form.SelectedControl, form.SelectedShift, form.SelectedAlt)}");
        }
    }

    private sealed class HotkeyCaptureForm : Form
    {
        private readonly Label _label;

        public Keys SelectedKey { get; private set; } = Keys.None;
        public bool SelectedControl { get; private set; }
        public bool SelectedShift { get; private set; }
        public bool SelectedAlt { get; private set; }

        public HotkeyCaptureForm(string title, string currentText)
        {
            Width = 320;
            Height = 130;
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            BackColor = AppBlack;
            ForeColor = TextWhite;
            KeyPreview = true;
            TopMost = true;

            _label = new Label
            {
                Left = 10,
                Top = 20,
                Width = 280,
                Height = 60,
                Text = $"Current: {currentText}\nPress any key combination to change\n(ESC to cancel)...",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F)
            };
            Controls.Add(_label);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var keyCode = keyData & Keys.KeyCode;

            if (keyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                return true;
            }

            // Exclude pure modifier keys
            if (keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu ||
                keyCode == Keys.LWin || keyCode == Keys.RWin)
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }

            SelectedKey = keyCode;
            SelectedControl = (keyData & Keys.Control) == Keys.Control;
            SelectedShift = (keyData & Keys.Shift) == Keys.Shift;
            SelectedAlt = (keyData & Keys.Alt) == Keys.Alt;

            DialogResult = DialogResult.OK;
            return true;
        }
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
        UpdateState(status);
    }

    private async Task HandlePasteSequenceAsync()
    {
        if (_currentIndex < 0 || _currentIndex >= _lines.Count)
        {
            UpdateState("No current line to paste.");
            return;
        }

        var pastedIndex = _currentIndex;
        if (!CopyCurrentLineToClipboard())
        {
            UpdateState("Clipboard is busy. Try hotkey again.");
            return;
        }

        // Give clipboard updates a short moment to settle before sending paste.
        await Task.Delay(40);

        // Trigger CTRL+V from the configured hotkey.
        SendKeys.SendWait("^v");

        _lines[pastedIndex].IsPasted = true;
        
        await Task.Delay(120);

        if (!PhotoshopFocusService.IsPhotoshopForeground())
        {
            UpdateState($"Line {ReadableLineNumber(pastedIndex)} pasted.");
            return;
        }

        // Advance selection only. Do not pre-copy next line.
        _currentIndex = FindNextPasteable(_currentIndex);
        UpdateState(_currentIndex < 0
            ? "End of script."
            : $"Line {ReadableLineNumber(pastedIndex)} pasted. Next line selected.");
    }

    private void SelectLine(int index, string status)
    {
        _currentIndex = index;
        _lineList.SelectedIndex = index;
        UpdateState(status);
    }

    private bool CopyCurrentLineToClipboard()
    {
        if (_currentIndex < 0 || _currentIndex >= _lines.Count) return false;
        return PasteService.CopyText(_lines[_currentIndex].CleanText);
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
        string progressText = "";
        if (total > 0)
        {
            if (_currentIndex < 0)
            {
                progressText = $"Done - {total} lines";
            }
            else
            {
                progressText = $"Line {ReadableLineNumber(_currentIndex)} of {total}";
                _lineList.SelectedIndex = _currentIndex;
            }
        }

        var displayStatus = status ?? (_currentIndex < 0 && total > 0 ? "End of script." : "");

        if (!string.IsNullOrEmpty(displayStatus))
        {
            _progressLabel.Text = string.IsNullOrEmpty(progressText) ? displayStatus : $"{progressText} | {displayStatus}";
        }
        else
        {
            _progressLabel.Text = progressText;
        }

        _statusLabel.Text = displayStatus;
        _lineList.Invalidate();
    }

    private void DrawLineItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _lines.Count) return;

        var line = _lines[e.Index];
        var isCurrent = e.Index == _currentIndex;
        var backColor = isCurrent ? AccentBlue : AppBlack;
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

    private void StartPhotoshopOwnerTimer()
    {
        _photoshopOwnerTimer.Interval = 150;
        _photoshopOwnerTimer.Tick += OnPhotoshopTimerTick;
        _photoshopOwnerTimer.Start();
    }

    private IntPtr GetPhotoshopHandle()
    {
        if (_lastPhotoshopHandle != IntPtr.Zero && PhotoshopFocusService.IsWindow(_lastPhotoshopHandle))
        {
            return _lastPhotoshopHandle;
        }

        IntPtr photoshopHandle = IntPtr.Zero;
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("Photoshop");
            foreach (var p in processes)
            {
                var handle = p.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    photoshopHandle = handle;
                    p.Dispose();
                    break;
                }
                p.Dispose();
            }
        }
        catch
        {
            // Ignore potential issues enumerating processes
        }

        if (photoshopHandle != _lastPhotoshopHandle)
        {
            _lastPhotoshopHandle = photoshopHandle;
            if (IsHandleCreated)
            {
                SetWindowOwner(Handle, photoshopHandle);
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        return photoshopHandle;
    }

    private void OnPhotoshopTimerTick(object? sender, EventArgs e)
    {
        IntPtr photoshopHandle = GetPhotoshopHandle();

        if (WindowState == FormWindowState.Minimized) return;

        bool shouldHide = false;
        if (photoshopHandle != IntPtr.Zero)
        {
            shouldHide = PhotoshopFocusService.IsPhotoshopInMenuOrModal(photoshopHandle);
        }

        if (shouldHide)
        {
            if (!_hiddenByPhotoshopState)
            {
                _hiddenByPhotoshopState = true;
                _normalState = WindowState;
                _normalBounds = Bounds;
                if (WindowState == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Normal;
                }
                Location = new Point(-32000, -32000);
            }
        }
        else
        {
            if (_hiddenByPhotoshopState)
            {
                _hiddenByPhotoshopState = false;
                if (_normalBounds.HasValue)
                {
                    Bounds = _normalBounds.Value;
                    _normalBounds = null;
                }
                WindowState = _normalState;
            }
        }
    }

    private void RunBuiltInScript()
    {
        BuiltInScript.Execute();
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => AccentBlue;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelectedGradientBegin => AccentBlue;
        public override Color MenuItemSelectedGradientEnd => AccentBlue;
        public override Color MenuItemPressedGradientBegin => PanelBlack;
        public override Color MenuItemPressedGradientEnd => PanelBlack;
        public override Color ToolStripDropDownBackground => PanelBlack;
        public override Color ImageMarginGradientBegin => PanelBlack;
        public override Color ImageMarginGradientMiddle => PanelBlack;
        public override Color ImageMarginGradientEnd => PanelBlack;
        public override Color MenuBorder => BorderBlack;
    }
}
