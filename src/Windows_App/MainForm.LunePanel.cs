using System.IO.Ports;

namespace WaterCoolingDevice;

internal sealed partial class MainForm
{
    private sealed record ThemeChoice(string Name, LunePanelTheme Theme, string File = "")
    {
        public override string ToString() => Name;
    }

    private LunePanelOptions lunePanelOptions = new();
    private LunePanelDisplay? lunePanel;
    private LuneForm? lunePanelScene;
    private LunePanelWindow? lunePanelWindow;
    private LuneThemeDocument? customTheme;
    private LuneThemeRenderer? customThemeRenderer;
    private string loadedThemeFile = string.Empty;
    private int customAnimationFrame;
    private int cyberAnimationFrame;
    private bool updatingLunePanelUi;
    private readonly System.Windows.Forms.Timer lunePanelTimer = new() { Interval = 1000 };
    private readonly Label lunePanelStatus = new() { AutoSize = true, MaximumSize = new Size(760, 0) };
    private readonly CheckBox lunePanelEnabled = new() { Text = "外部パネルへ表示", AutoSize = true };
    private readonly ComboBox lunePanelOrientation = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly ComboBox lunePanelTheme = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly CheckBox lunePanelReverse = new() {
        Text = "外部パネルを180°反転", AutoSize = true
    };
    private readonly NumericUpDown lunePanelBrightness = new() { Minimum = 0, Maximum = 100, Value = 50, Width = 80 };
    private readonly ComboBox lunePanelPort = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };

    private TabPage BuildLunePanelTab(bool restore)
    {
        if (restore) lunePanelOptions = LunePanelOptions.Load();
        var page = NewTab("LUNE Panel");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(28) };
        panel.Controls.Add(new Label { Text = "LUNE Display Studio", AutoSize = true,
            Font = new Font(Font.FontFamily, 15, FontStyle.Bold), ForeColor = Color.LightSkyBlue });
        panel.Controls.Add(new Label {
            Text = "LUNE・CYBER HUD・自作テーマをWindows画面に表示できます。\n" +
                   "TURZXを接続すると、同じ画面をPC画面外へ表示できます。",
            AutoSize = true, Margin = new Padding(3, 10, 3, 10)
        });
        panel.Controls.Add(new Label {
            Text = "外部表示できない場合：純正UsbMonitorがCOMポートを使用中です。\n" +
                   "通知領域も確認してUsbMonitorを完全に終了し、TURZXを抜き差ししてから再試行してください。",
            AutoSize = true, MaximumSize = new Size(760, 0), ForeColor = Color.Khaki,
            Margin = new Padding(3, 0, 3, 15)
        });

        lunePanelOrientation.Items.AddRange(["横向き / Landscape 480×320", "縦向き / Portrait 320×480"]);
        ReloadThemeChoices(lunePanelOptions.ThemeFile);
        lunePanelOrientation.SelectedIndex = lunePanelOptions.Landscape ? 0 : 1;
        lunePanelReverse.Checked = lunePanelOptions.Reverse;
        lunePanelBrightness.Value = lunePanelOptions.Brightness;
        ReloadLunePanelPorts();

        void Row(string label, params Control[] controls) {
            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(3, 8, 3, 0) };
            row.Controls.Add(new Label { Text = label, Width = 120, Padding = new Padding(0, 5, 0, 0) });
            row.Controls.AddRange(controls); panel.Controls.Add(row);
        }
        Row("テーマ", lunePanelTheme);
        panel.Controls.Add(themeGallery);
        RebuildThemeGallery();
        var edit = new Button { Text = "テーマを作る・編集", AutoSize = true };
        var import = new Button { Text = "テーマファイルを読込", AutoSize = true };
        var deleteTheme = new Button { Text = "テーマを削除", AutoSize = true,
            Enabled = lunePanelTheme.SelectedItem is ThemeChoice { Theme: LunePanelTheme.Custom } };
        var openWindow = new Button { Text = "Windowsに表示", AutoSize = true };
        Row("編集", edit, import, deleteTheme, openWindow);
        panel.Controls.Add(desktopAutoShow);
        panel.Controls.Add(restoreWindowPosition);
        Row("向き", lunePanelOrientation, lunePanelReverse);
        Row("明るさ", lunePanelBrightness);
        panel.Controls.Add(lunePanelEnabled);
        var reload = new Button { Text = "再検索", AutoSize = true };
        reload.Click += (_, _) => ReloadLunePanelPorts();
        Row("接続先", lunePanelPort, reload);
        panel.Controls.Add(new Label {
            Text = "背景は初回のみ全面送信し、その後は数値と小さなGIFの変更領域だけ更新します。",
            AutoSize = true, Margin = new Padding(3, 16, 3, 8), ForeColor = Color.Silver
        });
        panel.Controls.Add(lunePanelStatus);
        page.Controls.Add(panel);

        lunePanelEnabled.Checked = lunePanelOptions.Enabled;
        lunePanelEnabled.CheckedChanged += (_, _) => ApplyLunePanelOptions();
        lunePanelTheme.SelectedIndexChanged += (_, _) => {
            deleteTheme.Enabled = lunePanelTheme.SelectedItem is ThemeChoice {
                Theme: LunePanelTheme.Custom
            };
            ThemeSelectionChanged();
        };
        lunePanelOrientation.SelectedIndexChanged += (_, _) => ApplyLunePanelOptions();
        lunePanelReverse.CheckedChanged += (_, _) => ApplyLunePanelOptions();
        lunePanelBrightness.ValueChanged += (_, _) => ApplyLunePanelOptions();
        lunePanelPort.SelectedIndexChanged += (_, _) => ApplyLunePanelOptions();
        lunePanelTimer.Tick += (_, _) => RefreshLunePanel();
        edit.Click += (_, _) => EditSelectedTheme();
        import.Click += (_, _) => ImportTheme();
        deleteTheme.Click += (_, _) => DeleteSelectedTheme();
        openWindow.Click += (_, _) => ShowLunePanelWindow();
        if (restore && lunePanelOptions.StudioEnabled) ApplyLunePanelOptions();
        else lunePanelStatus.Text = T("外部表示 OFF／Windows表示は利用できます",
            "External display OFF / Windows display is available");
        return page;
    }

    private void UpdateLunePanelLanguage()
    {
        UpdateStudioLanguage();
        if (!lunePanelOptions.Enabled)
            lunePanelStatus.Text = T("外部表示 OFF／Windows表示は利用できます", "External display OFF / Windows display is available");
        if (lunePanelOrientation.IsDisposed) return;
        var selected = lunePanelOrientation.SelectedIndex;
        updatingLunePanelUi = true;
        lunePanelOrientation.Items.Clear();
        lunePanelOrientation.Items.AddRange(isEnglish
            ? ["Landscape 480×320", "Portrait 320×480"]
            : ["横向き 480×320", "縦向き 320×480"]);
        lunePanelOrientation.SelectedIndex = selected >= 0
            ? selected : (lunePanelOptions.Landscape ? 0 : 1);
        updatingLunePanelUi = false;
    }

    private void ReloadThemeChoices(string? preferredFile)
    {
        updatingLunePanelUi = true;
        lunePanelTheme.Items.Clear();
        lunePanelTheme.Items.Add(new ThemeChoice("LUNE", LunePanelTheme.Lune));
        lunePanelTheme.Items.Add(new ThemeChoice("CYBER HUD", LunePanelTheme.Cyber));
        foreach (var file in LuneThemeStore.FindThemes()) {
            try { lunePanelTheme.Items.Add(new ThemeChoice(LuneThemeStore.Load(file).Name, LunePanelTheme.Custom, file)); }
            catch { }
        }
        var match = !string.IsNullOrWhiteSpace(preferredFile)
            ? lunePanelTheme.Items.Cast<ThemeChoice>().FirstOrDefault(item =>
                item.Theme == LunePanelTheme.Custom &&
                string.Equals(item.File, preferredFile, StringComparison.OrdinalIgnoreCase))
            : null;
        match ??= lunePanelTheme.Items.Cast<ThemeChoice>().FirstOrDefault(item =>
            item.Theme == lunePanelOptions.Theme);
        lunePanelTheme.SelectedItem = match ?? lunePanelTheme.Items[0];
        if (lunePanelTheme.SelectedItem is ThemeChoice { Theme: LunePanelTheme.Custom } custom) {
            try {
                var selectedTheme = LuneThemeStore.Load(custom.File);
                lunePanelOrientation.SelectedIndex = selectedTheme.Landscape ? 0 : 1;
                lunePanelReverse.Checked = selectedTheme.Reverse;
                lunePanelBrightness.Value = selectedTheme.Brightness;
            } catch { }
        }
        updatingLunePanelUi = false;
        RebuildThemeGallery();
    }

    private void ThemeSelectionChanged()
    {
        if (updatingLunePanelUi || lunePanelTheme.SelectedItem is not ThemeChoice choice) return;
        if (choice.Theme == LunePanelTheme.Custom) {
            try {
                var selectedTheme = LuneThemeStore.Load(choice.File);
                updatingLunePanelUi = true;
                lunePanelOrientation.SelectedIndex = selectedTheme.Landscape ? 0 : 1;
                lunePanelReverse.Checked = selectedTheme.Reverse;
                lunePanelBrightness.Value = selectedTheme.Brightness;
                updatingLunePanelUi = false;
            } catch (Exception ex) {
                MessageBox.Show(this, ex.Message, "テーマ読込", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        ApplyLunePanelOptions();
    }

    private void ReloadLunePanelPorts()
    {
        var selected = lunePanelOptions.Port;
        lunePanelPort.BeginUpdate();
        try {
            lunePanelPort.Items.Clear(); lunePanelPort.Items.Add("AUTO");
            foreach (var port in SerialPort.GetPortNames().Order()) lunePanelPort.Items.Add(port);
            if (!lunePanelPort.Items.Contains(selected)) lunePanelPort.Items.Add(selected);
            lunePanelPort.SelectedItem = selected;
        } finally { lunePanelPort.EndUpdate(); }
    }

    private void ApplyLunePanelOptions()
    {
        if (!studioEnabled.Checked || updatingLunePanelUi || lunePanelPort.SelectedItem is not string port ||
            lunePanelOrientation.SelectedIndex < 0 || lunePanelTheme.SelectedItem is not ThemeChoice choice) return;
        lunePanelOptions = lunePanelOptions with {
            StudioEnabled = studioEnabled.Checked, DesktopAutoShow = desktopAutoShow.Checked,
            Enabled = lunePanelEnabled.Checked, Landscape = lunePanelOrientation.SelectedIndex == 0,
            Reverse = lunePanelReverse.Checked, Animate = false, Theme = choice.Theme,
            ThemeFile = choice.File, Brightness = (int)lunePanelBrightness.Value, Port = port
        };
        LoadCustomTheme(choice.File);
        lunePanelTimer.Interval = choice.Theme == LunePanelTheme.Cyber
            ? 700
            : customTheme?.AnimationFrameIntervalMs() ?? 1000;
        try { if (!studioTestMode) lunePanelOptions.Save(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, T("LUNE Studio設定", "LUNE Studio Settings")); }
        StartLunePanel(); lunePanelTimer.Start();
        if (lunePanelWindow is { IsDisposed: false })
            lunePanelWindow.ApplyTheme(choice.Theme, lunePanelOptions.Landscape, customTheme);
    }

    private void LoadCustomTheme(string file)
    {
        if (string.Equals(file, loadedThemeFile, StringComparison.OrdinalIgnoreCase) && customThemeRenderer is not null) return;
        customThemeRenderer?.Dispose(); customThemeRenderer = null; customTheme = null;
        loadedThemeFile = file; customAnimationFrame = 0;
        if (string.IsNullOrWhiteSpace(file)) return;
        try { customTheme = LuneThemeStore.Load(file); customThemeRenderer = new LuneThemeRenderer(customTheme); }
        catch (Exception ex) { lunePanelStatus.Text = "テーマ読込エラー: " + ex.Message; }
    }

    private void StartLunePanel()
    {
        lunePanel ??= new LunePanelDisplay();
        lunePanel.Configure(lunePanelOptions);
        if (lunePanelOptions.Enabled) lunePanelScene ??= new LuneForm();
    }

    private void RefreshLunePanel()
    {
        if (applicationClosing) return;
        lunePanelStatus.Text = lunePanel?.Status ?? "外部表示 OFF";
        if (!lunePanelOptions.Enabled || lunePanelScene is null || lunePanel is null) return;
        UpdateLuneTelemetry();
        lunePanelScene.SetDisplayLanguage(isEnglish);
        using var image = lunePanelOptions.Theme switch {
            LunePanelTheme.Cyber => lunePanelScene.RenderLunePanelCyber(
                lunePanelOptions.Landscape, cyberAnimationFrame++),
            LunePanelTheme.Custom when customThemeRenderer is not null => customThemeRenderer.Render(
                CurrentLuneTelemetry(), customAnimationFrame++,
                new Size(lunePanelOptions.Width, lunePanelOptions.Height)),
            _ => lunePanelScene.RenderLunePanel(lunePanelOptions.Landscape, false)
        };
        lunePanel.Submit(image, lunePanelOptions);
    }

    private LuneTelemetrySnapshot CurrentLuneTelemetry() => new(
        client.IsConnected && !sensorFault ? currentTemperature : null,
        currentCpuTemperature, currentGpuTemperature, (float)warningEditor.Value,
        client.IsConnected, sensorFault, warningAlarmActive, lowColor, warningColor,
        currentCpuUsage, currentMemoryUsage, currentFan1Duty, currentFan1Rpm,
        currentFan2Duty, currentFan2Rpm, currentCpuName, currentGpuName, currentGpuUsage);

    private void EditSelectedTheme()
    {
        LuneThemeDocument initial;
        if (lunePanelTheme.SelectedItem is ThemeChoice { Theme: LunePanelTheme.Custom } choice) {
            try { initial = LuneThemeStore.Load(choice.File); } catch { initial = LuneThemeDocument.CreateDefault(); }
        } else initial = LuneThemeDocument.CreateDefault();
        using var editor = new LuneThemeEditorForm(initial, CurrentLuneTelemetry(), isEnglish);
        var keepMainOnTop = TopMost;
        try {
            // A modal editor must sit above the main window even when "always on top" is enabled.
            TopMost = false;
            editor.TopMost = keepMainOnTop;
            editor.Shown += (_, _) => { editor.BringToFront(); editor.Activate(); };
            editor.ShowDialog(this);
        }
        finally {
            editor.TopMost = false;
            TopMost = keepMainOnTop;
            if (Visible) Activate();
        }
        if (editor.SavedPath is null) return;
        loadedThemeFile = string.Empty;
        ReloadThemeChoices(editor.SavedPath); ApplyLunePanelOptions();
    }

    private void ImportTheme()
    {
        using var dialog = new OpenFileDialog { Filter = T("LUNEテーマ|*.lunetheme", "LUNE theme|*.lunetheme"), Title = T("テーマファイルを読み込む", "Import Theme File") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try {
            var imported = LuneThemeStore.Load(dialog.FileName);
            var installed = LuneThemeStore.Install(imported);
            loadedThemeFile = string.Empty;
            ReloadThemeChoices(installed); ApplyLunePanelOptions();
        } catch (Exception ex) { MessageBox.Show(this, ex.Message, "テーマ読込", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void DeleteSelectedTheme()
    {
        if (lunePanelTheme.SelectedItem is not ThemeChoice {
            Theme: LunePanelTheme.Custom
        } choice) return;

        var answer = MessageBox.Show(this,
            T($"テーマ「{choice.Name}」を削除します。\nこの操作は元に戻せません。",
              $"Delete theme \"{choice.Name}\"?\nThis cannot be undone."),
            T("テーマの削除", "Delete Theme"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try {
            LuneThemeStore.DeleteInstalled(choice.File);
            customThemeRenderer?.Dispose(); customThemeRenderer = null;
            customTheme = null; loadedThemeFile = string.Empty;
            lunePanelOptions = lunePanelOptions with {
                Theme = LunePanelTheme.Lune, ThemeFile = string.Empty
            };
            ReloadThemeChoices(null);
            ApplyLunePanelOptions();
            lunePanelStatus.Text = T("テーマを削除しました。", "Theme deleted.");
        }
        catch (Exception ex) {
            MessageBox.Show(this, ex.Message,
                T("テーマ削除エラー", "Theme Delete Error"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowLunePanelWindow()
    {
        if (!studioEnabled.Checked || lunePanelTheme.SelectedItem is not ThemeChoice choice) return;
        ApplyLunePanelOptions();
        if (lunePanelWindow is null || lunePanelWindow.IsDisposed) {
            lunePanelWindow = new LunePanelWindow(choice.Theme, lunePanelOptions.Landscape, customTheme);
            var window = lunePanelWindow;
            if (lunePanelOptions.RestoreWindowPosition && lunePanelOptions.WindowX is int x && lunePanelOptions.WindowY is int y) {
                window.StartPosition = FormStartPosition.Manual;
                var area = Screen.FromRectangle(new Rectangle(x, y, window.Width, window.Height)).WorkingArea;
                window.Location = ClampPanelPosition(new Point(x, y), window.Size, area);
            }
            window.PositionSettled += (_, _) => SavePanelPosition(window);
            window.FormClosing += (_, _) => SavePanelPosition(window);
            window.FormClosed += (_, _) => lunePanelWindow = null;
        }
        lunePanelWindow.ApplyTheme(choice.Theme, lunePanelOptions.Landscape, customTheme);
        lunePanelWindow.UpdateTelemetry(CurrentLuneTelemetry());
        lunePanelWindow.Show(); lunePanelWindow.Activate();
    }

    internal static Point ClampPanelPosition(Point location, Size size, Rectangle area) => new(
        Math.Clamp(location.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
        Math.Clamp(location.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));

    private void SavePanelPosition(LunePanelWindow window)
    {
        if (window.WindowState != FormWindowState.Normal) return;
        lunePanelOptions = lunePanelOptions with { WindowX = window.Left, WindowY = window.Top };
        try { if (!studioTestMode) lunePanelOptions.Save(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    internal void EnableLunePanelForSession() { lunePanelEnabled.Checked = true; StartLunePanel(); lunePanelTimer.Start(); }

    private void StopLunePanel()
    {
        lunePanelTimer.Stop(); lunePanelTimer.Dispose(); lunePanel?.Dispose();
        lunePanelScene?.Dispose(); lunePanelScene = null;
        customThemeRenderer?.Dispose(); customThemeRenderer = null;
        if (lunePanelWindow is { IsDisposed: false }) lunePanelWindow.Close();
    }
}
