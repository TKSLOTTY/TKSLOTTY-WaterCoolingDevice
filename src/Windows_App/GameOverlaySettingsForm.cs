namespace WaterCoolingDevice;

internal sealed record GameOverlayDisplayOptions(
    bool FilledBackground, Color TextColor, Color BackgroundColor,
    bool ShowFps, bool ShowCpu, bool ShowGpu, bool ShowHotSpot, bool ShowWater,
    bool ShowFan1Duty, bool ShowFan2Duty, float FontSize, int TopMargin)
{
    public static GameOverlayDisplayOptions From(AppSettings settings) => new(
        settings.GameOverlayFilledBackground,
        Color.FromArgb(settings.GameOverlayTextColorArgb),
        Color.FromArgb(settings.GameOverlayBackgroundColorArgb),
        settings.GameOverlayShowFps, settings.GameOverlayShowCpu,
        settings.GameOverlayShowGpu, settings.GameOverlayShowHotSpot,
        settings.GameOverlayShowWater, settings.GameOverlayShowFan1Duty,
        settings.GameOverlayShowFan2Duty,
        Math.Clamp(settings.GameOverlayFontSize, 8f, 32f),
        Math.Clamp(settings.GameOverlayTopMargin, 0, 50));

    public void ApplyTo(AppSettings settings)
    {
        settings.GameOverlayFilledBackground = FilledBackground;
        settings.GameOverlayTextColorArgb = TextColor.ToArgb();
        settings.GameOverlayBackgroundColorArgb = BackgroundColor.ToArgb();
        settings.GameOverlayShowFps = ShowFps;
        settings.GameOverlayShowCpu = ShowCpu;
        settings.GameOverlayShowGpu = ShowGpu;
        settings.GameOverlayShowHotSpot = ShowHotSpot;
        settings.GameOverlayShowWater = ShowWater;
        settings.GameOverlayShowFan1Duty = ShowFan1Duty;
        settings.GameOverlayShowFan2Duty = ShowFan2Duty;
        settings.GameOverlayFontSize = Math.Clamp(FontSize, 8f, 32f);
        settings.GameOverlayTopMargin = Math.Clamp(TopMargin, 0, 50);
    }
}

internal sealed class GameOverlaySettingsForm : Form
{
    private readonly bool english;
    private readonly RadioButton transparentBackground = new() { AutoSize = true };
    private readonly RadioButton filledBackground = new() { AutoSize = true };
    private readonly Button textColorButton = new() { AutoSize = true };
    private readonly Button backgroundColorButton = new() { AutoSize = true };
    private readonly CheckBox showFps = new() { Text = "FPS", AutoSize = true };
    private readonly CheckBox showCpu = new() { Text = "CPU温度", AutoSize = true };
    private readonly CheckBox showGpu = new() { Text = "GPU温度", AutoSize = true };
    private readonly CheckBox showHotSpot = new() { Text = "HOTSPOT", AutoSize = true };
    private readonly CheckBox showWater = new() { Text = "水温", AutoSize = true };
    private readonly CheckBox showFan1Duty = new() { Text = "FAN1 DUTY", AutoSize = true };
    private readonly CheckBox showFan2Duty = new() { Text = "FAN2 DUTY", AutoSize = true };
    private readonly NumericUpDown fontSize = new() {
        Minimum = 8, Maximum = 32, DecimalPlaces = 1, Increment = 0.5m,
        Width = 72, TextAlign = HorizontalAlignment.Center
    };
    private readonly NumericUpDown topMargin = new() {
        Minimum = 0, Maximum = 50, Width = 72, TextAlign = HorizontalAlignment.Center
    };
    private readonly Panel preview = new() { Height = 58, Dock = DockStyle.Fill, Padding = new Padding(12) };
    private readonly Label previewText = new() {
        AutoSize = true, Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold)
    };
    private Color selectedTextColor;
    private Color selectedBackgroundColor;

    public GameOverlayDisplayOptions SelectedOptions { get; private set; }

    public GameOverlaySettingsForm(GameOverlayDisplayOptions options, bool english)
    {
        this.english = english;
        SelectedOptions = options;
        selectedTextColor = options.TextColor;
        selectedBackgroundColor = options.BackgroundColor;
        Text = english ? "Game Overlay Settings" : "ゲームオーバーレイ詳細設定";
        ClientSize = new Size(550, 475);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(30, 33, 39);
        ForeColor = Color.Gainsboro;

        transparentBackground.Text = english ? "Transparent background" : "背景を透明にする";
        filledBackground.Text = english ? "Fill background" : "背景を塗りつぶす";
        textColorButton.Text = english ? "Choose text color" : "文字色を選択";
        backgroundColorButton.Text = english ? "Choose background color" : "背景色を選択";
        showCpu.Text = english ? "CPU temperature" : "CPU温度";
        showGpu.Text = english ? "GPU temperature" : "GPU温度";
        showWater.Text = english ? "Coolant temperature" : "水温";

        transparentBackground.Checked = !options.FilledBackground;
        filledBackground.Checked = options.FilledBackground;
        showFps.Checked = options.ShowFps;
        showCpu.Checked = options.ShowCpu;
        showGpu.Checked = options.ShowGpu;
        showHotSpot.Checked = options.ShowHotSpot;
        showWater.Checked = options.ShowWater;
        showFan1Duty.Checked = options.ShowFan1Duty;
        showFan2Duty.Checked = options.ShowFan2Duty;
        fontSize.Value = (decimal)Math.Clamp(options.FontSize, 8f, 32f);
        topMargin.Value = Math.Clamp(options.TopMargin, 0, 50);

        var root = new TableLayoutPanel {
            Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 9
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(Header(english ? "Displayed items" : "表示する項目"), 0, 0);
        var items = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        items.Controls.AddRange(new Control[] {
            showFps, showCpu, showGpu, showHotSpot, showWater, showFan1Duty, showFan2Duty
        });
        root.Controls.Add(items, 0, 1);
        root.Controls.Add(Header(english ? "Background" : "背景"), 0, 2);
        var background = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        background.Controls.Add(transparentBackground);
        background.Controls.Add(filledBackground);
        background.Controls.Add(backgroundColorButton);
        root.Controls.Add(background, 0, 3);
        var colors = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        colors.Controls.Add(textColorButton);
        root.Controls.Add(colors, 0, 4);
        root.Controls.Add(Header(english ? "Size and position" : "文字サイズと位置"), 0, 5);
        var sizeAndPosition = new FlowLayoutPanel {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = false
        };
        sizeAndPosition.Controls.Add(new Label {
            Text = english ? "Font size:" : "文字サイズ:", AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        });
        sizeAndPosition.Controls.Add(fontSize);
        sizeAndPosition.Controls.Add(new Label {
            Text = english ? "Top margin:" : "上端余白:", AutoSize = true,
            Padding = new Padding(16, 6, 0, 0)
        });
        sizeAndPosition.Controls.Add(topMargin);
        sizeAndPosition.Controls.Add(new Label {
            Text = "px", AutoSize = true, Padding = new Padding(0, 6, 0, 0)
        });
        root.Controls.Add(sizeAndPosition, 0, 6);
        preview.Controls.Add(previewText);
        root.Controls.Add(preview, 0, 7);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.None, AutoSize = true };
        var cancel = new Button {
            Text = english ? "Cancel" : "キャンセル", DialogResult = DialogResult.Cancel, AutoSize = true
        };
        var actions = new FlowLayoutPanel {
            Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft
        };
        actions.Controls.Add(cancel);
        actions.Controls.Add(ok);
        root.Controls.Add(actions, 0, 8);
        Controls.Add(root);
        CancelButton = cancel;

        textColorButton.Click += (_, _) => ChooseColor(true);
        backgroundColorButton.Click += (_, _) => ChooseColor(false);
        transparentBackground.CheckedChanged += (_, _) => UpdatePreview();
        filledBackground.CheckedChanged += (_, _) => UpdatePreview();
        fontSize.ValueChanged += (_, _) => UpdatePreview();
        topMargin.ValueChanged += (_, _) => UpdatePreview();
        foreach (var item in new[] { showFps, showCpu, showGpu, showHotSpot, showWater, showFan1Duty, showFan2Duty })
            item.CheckedChanged += (_, _) => UpdatePreview();
        ok.Click += (_, _) => SaveAndClose();
        UpdatePreview();
    }

    private Label Header(string text) => new() {
        Text = text, AutoSize = true, ForeColor = Color.LightSkyBlue,
        Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 8, 0, 4)
    };

    private void ChooseColor(bool text)
    {
        using var dialog = new ColorDialog {
            Color = text ? selectedTextColor : selectedBackgroundColor,
            FullOpen = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (text) selectedTextColor = dialog.Color;
        else selectedBackgroundColor = dialog.Color;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        backgroundColorButton.Enabled = filledBackground.Checked;
        preview.BackColor = filledBackground.Checked
            ? selectedBackgroundColor : Color.FromArgb(55, 60, 68);
        previewText.ForeColor = selectedTextColor;
        previewText.Font = new Font("Segoe UI Semibold", (float)fontSize.Value, FontStyle.Bold);
        previewText.Text = BuildExampleText();
        previewText.Location = new Point(12, 17);
    }

    private string BuildExampleText()
    {
        var values = new List<string>();
        if (showFps.Checked) values.Add("FPS 60");
        if (showCpu.Checked) values.Add("CPU 55 °C");
        if (showGpu.Checked) values.Add("GPU 43 °C");
        if (showHotSpot.Checked) values.Add("HOTSPOT 52 °C");
        if (showWater.Checked) values.Add("WATER 34.7 °C");
        if (showFan1Duty.Checked) values.Add("FAN1 45%");
        if (showFan2Duty.Checked) values.Add("FAN2 60%");
        return values.Count == 0 ? "(no items)" : string.Join(" | ", values);
    }

    private void SaveAndClose()
    {
        if (!new[] { showFps, showCpu, showGpu, showHotSpot, showWater, showFan1Duty, showFan2Duty }
            .Any(item => item.Checked))
        {
            MessageBox.Show(this,
                english ? "Select at least one item." : "表示項目を1つ以上選んでください。",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SelectedOptions = new GameOverlayDisplayOptions(
            filledBackground.Checked, selectedTextColor, selectedBackgroundColor,
            showFps.Checked, showCpu.Checked, showGpu.Checked, showHotSpot.Checked,
            showWater.Checked, showFan1Duty.Checked, showFan2Duty.Checked,
            (float)fontSize.Value, (int)topMargin.Value);
        DialogResult = DialogResult.OK;
        Close();
    }
}
