namespace WaterCoolingDevice;

internal sealed class LuneThemeCanvas : Control
{
    private LuneThemeDocument theme = LuneThemeDocument.CreateDefault();
    private LuneThemeElement? selected;
    private LuneThemeAnimation? selectedAnimation;
    private Point dragStart;
    private Point elementStart;
    private bool dragging;
    public event EventHandler? SelectionChanged;
    public event EventHandler? AnimationSelectionChanged;
    public event EventHandler? ThemeChanged;
    public LuneThemeDocument Theme { get => theme; set { theme = value; selected = null; selectedAnimation = null; Invalidate(); } }
    public LuneThemeElement? SelectedElement {
        get => selected;
        set {
            selected = value;
            if (value is not null) selectedAnimation = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            AnimationSelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }
    public LuneThemeAnimation? SelectedAnimation {
        get => selectedAnimation;
        set {
            selectedAnimation = value;
            if (value is not null) selected = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            AnimationSelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    private LuneTelemetrySnapshot sample = new(29.5f, 46, 42, 55, true, false, false,
        Color.Cyan, Color.DeepPink, 37, 52, 45, 1380, 60, 2200,
        "CPU DEVICE", "GPU DEVICE");
    public LuneTelemetrySnapshot Sample {
        get => sample;
        set { sample = value; Invalidate(); }
    }

    public LuneThemeCanvas()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(20, 22, 28);
        MinimumSize = new Size(360, 300);
        MouseDown += BeginDrag;
        MouseMove += ContinueDrag;
        MouseUp += (_, _) => dragging = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var target = CanvasBounds();
        using var renderer = new LuneThemeRenderer(theme);
        using var preview = renderer.Render(sample);
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(preview, target);
        var scale = target.Width / (float)theme.Width;
        RectangleF selectedBounds;
        var selectionColor = Color.Yellow;
        if (selected is not null) {
            selectedBounds = new RectangleF(target.X + selected.X * scale, target.Y + selected.Y * scale,
                selected.Width * scale, selected.Height * scale);
        } else if (selectedAnimation is not null) {
            selectedBounds = new RectangleF(target.X + selectedAnimation.X * scale, target.Y + selectedAnimation.Y * scale,
                selectedAnimation.Width * scale, selectedAnimation.Height * scale);
            selectionColor = Color.Lime;
        } else return;
        using var pen = new Pen(selectionColor, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        e.Graphics.DrawRectangle(pen, selectedBounds.X, selectedBounds.Y, selectedBounds.Width, selectedBounds.Height);
    }

    private Rectangle CanvasBounds()
    {
        const int margin = 18;
        var availableWidth = Math.Max(1, ClientSize.Width - margin * 2);
        var availableHeight = Math.Max(1, ClientSize.Height - margin * 2);
        var scale = Math.Min(availableWidth / (float)theme.Width, availableHeight / (float)theme.Height);
        var width = Math.Max(1, (int)(theme.Width * scale));
        var height = Math.Max(1, (int)(theme.Height * scale));
        return new Rectangle((ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);
    }

    private void BeginDrag(object? sender, MouseEventArgs e)
    {
        var point = ToThemePoint(e.Location);
        var animation = selectedAnimation is not null && ItemBounds(selectedAnimation).Contains(point)
            ? selectedAnimation
            : null;
        var element = animation is null
            ? theme.Elements.LastOrDefault(item => ItemBounds(item).Contains(point))
            : null;
        animation ??= element is null
            ? theme.Animations.LastOrDefault(item => ItemBounds(item).Contains(point))
            : null;
        selected = element;
        selectedAnimation = animation;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        AnimationSelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
        if (selected is null && selectedAnimation is null) return;
        dragging = true;
        dragStart = point;
        elementStart = selected is not null
            ? new Point(selected.X, selected.Y)
            : new Point(selectedAnimation!.X, selectedAnimation.Y);
    }

    private void ContinueDrag(object? sender, MouseEventArgs e)
    {
        if (!dragging || e.Button != MouseButtons.Left) return;
        var point = ToThemePoint(e.Location);
        if (selected is not null) {
            selected.X = Math.Clamp(elementStart.X + point.X - dragStart.X, 0, Math.Max(0, theme.Width - selected.Width));
            selected.Y = Math.Clamp(elementStart.Y + point.Y - dragStart.Y, 0, Math.Max(0, theme.Height - selected.Height));
        } else if (selectedAnimation is not null) {
            selectedAnimation.X = Math.Clamp(elementStart.X + point.X - dragStart.X, 0, Math.Max(0, theme.Width - selectedAnimation.Width));
            selectedAnimation.Y = Math.Clamp(elementStart.Y + point.Y - dragStart.Y, 0, Math.Max(0, theme.Height - selectedAnimation.Height));
        } else return;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private static Rectangle ItemBounds(LuneThemeElement item) => new(item.X, item.Y, item.Width, item.Height);
    private static Rectangle ItemBounds(LuneThemeAnimation item) => new(item.X, item.Y, item.Width, item.Height);

    private Point ToThemePoint(Point point)
    {
        var target = CanvasBounds();
        var scale = target.Width / (float)theme.Width;
        return new Point((int)((point.X - target.X) / scale), (int)((point.Y - target.Y) / scale));
    }
}

internal sealed class LuneThemeEditorForm : Form
{
    private sealed record MetricChoice(LuneMetric Value, string Name)
    {
        public override string ToString() => Name;
    }
    private LuneThemeDocument theme;
    private readonly LuneThemeCanvas canvas = new() { Dock = DockStyle.Fill };
    private readonly TextBox name = new() { Width = 220 };
    private readonly ComboBox orientation = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox reverse = new() { Text = "外部パネルを180°反転", AutoSize = true };
    private readonly NumericUpDown brightness = Number(0, 100, 50);
    private readonly ListBox elements = new() { Width = 245, Height = 125 };
    private readonly ComboBox metric = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox label = new() { Width = 220 };
    private readonly TextBox staticText = new() { Width = 220 };
    private readonly NumericUpDown x = Number(0, 480, 20);
    private readonly NumericUpDown y = Number(0, 480, 20);
    private readonly NumericUpDown width = Number(12, 480, 150);
    private readonly NumericUpDown height = Number(12, 480, 70);
    private readonly NumericUpDown fontSize = Number(8, 80, 28);
    private readonly NumericUpDown labelFontSize = Number(8, 80, 10);
    private readonly CheckBox showPanel = new() { Text = "背景パネル", AutoSize = true };
    private readonly CheckBox centerValue = new() { Text = "値を中央揃え", AutoSize = true };
    private readonly Button color = new() { Text = "ラベル・枠色", AutoSize = true };
    private readonly Button valueColor = new() { Text = "数値色", AutoSize = true };
    private readonly ListBox animations = new() { Width = 245, Height = 82 };
    private readonly Label animationName = new() { AutoSize = true, MaximumSize = new Size(220, 0), Text = "GIFなし" };
    private readonly NumericUpDown gifX = Number(0, 480, 190);
    private readonly NumericUpDown gifY = Number(0, 480, 105);
    private readonly NumericUpDown gifWidth = Number(8, 480, 100);
    private readonly NumericUpDown gifHeight = Number(8, 480, 100);
    private readonly NumericUpDown gifInterval = Number(250, 5000, 500, 50);
    private readonly LuneTelemetrySnapshot previewTelemetry;
    private readonly bool english;
    private LunePanelWindow? previewWindow;
    private bool updating;
    public string? SavedPath { get; private set; }

    public LuneThemeEditorForm(
        LuneThemeDocument? existing = null,
        LuneTelemetrySnapshot? previewSnapshot = null,
        bool english = false)
    {
        this.english = english;
        theme = existing ?? LuneThemeDocument.CreateDefault();
        if (previewSnapshot.HasValue) canvas.Sample = previewSnapshot.Value;
        previewTelemetry = canvas.Sample;
        Text = "LUNE Display Studio";
        ClientSize = new Size(1060, 720);
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(30, 33, 39);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 10);
        reverse.Text = T("外部パネルを180°反転", "Rotate external panel 180°");
        showPanel.Text = T("背景パネル", "Background panel");
        centerValue.Text = T("値を中央揃え", "Center value");
        color.Text = T("ラベル・枠色", "Label / border color");
        valueColor.Text = T("数値色", "Value color");
        animationName.Text = T("GIFなし", "No GIF");

        orientation.Items.AddRange([T("横向き 480×320", "Landscape 480×320"),
            T("縦向き 320×480", "Portrait 320×480")]);
        metric.Items.AddRange([
            new MetricChoice(LuneMetric.Text, T("固定文字", "Static text")),
            new MetricChoice(LuneMetric.WaterTemperature, T("水温", "Coolant temperature")),
            new MetricChoice(LuneMetric.CpuTemperature, T("CPU温度", "CPU temperature")),
            new MetricChoice(LuneMetric.GpuTemperature, T("GPU温度", "GPU temperature")),
            new MetricChoice(LuneMetric.CpuName, T("CPUデバイス名", "CPU device name")),
            new MetricChoice(LuneMetric.GpuName, T("GPUデバイス名", "GPU device name")),
            new MetricChoice(LuneMetric.CpuUsage, T("CPU使用率", "CPU usage")),
            new MetricChoice(LuneMetric.GpuUsage, T("GPU使用率", "GPU usage")),
            new MetricChoice(LuneMetric.MemoryUsage, T("メモリ使用率", "Memory usage")),
            new MetricChoice(LuneMetric.Fan1Duty, T("FAN1 出力", "FAN1 duty")),
            new MetricChoice(LuneMetric.Fan1Rpm, "FAN1 RPM"),
            new MetricChoice(LuneMetric.Fan2Duty, T("FAN2/PUMP 出力", "FAN2/PUMP duty")),
            new MetricChoice(LuneMetric.Fan2Rpm, "FAN2/PUMP RPM"),
            new MetricChoice(LuneMetric.Clock, T("現在時刻", "Current time")),
            new MetricChoice(LuneMetric.Date, T("日付", "Date"))
        ]);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(13) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var canvasHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        canvasHost.Controls.Add(canvas);
        layout.Controls.Add(canvasHost, 0, 0);
        layout.Controls.Add(tools, 1, 0);
        Controls.Add(layout);

        AddSection(tools, T("テーマ", "Theme")); AddRow(tools, T("名前", "Name"), name);
        AddRow(tools, T("向き", "Layout"), orientation);
        AddRow(tools, T("表示", "Display"), reverse); AddRow(tools, T("明るさ", "Brightness"), brightness);
        var backgroundButton = new Button { Text = T("背景画像を選択", "Choose background image"), AutoSize = true };
        var clearBackground = new Button { Text = T("背景を解除", "Clear background"), AutoSize = true };
        AddRow(tools, T("背景", "Background"), backgroundButton, clearBackground);

        AddSection(tools, T("数値・文字", "Values and text")); tools.Controls.Add(elements);
        var add = new Button { Text = T("項目を追加", "Add item"), AutoSize = true };
        var remove = new Button { Text = T("削除", "Delete"), AutoSize = true };
        AddRow(tools, "", add, remove); AddRow(tools, T("内容", "Content"), metric);
        AddRow(tools, T("ラベル", "Label"), label); AddRow(tools, T("固定文字", "Static text"), staticText);
        AddRow(tools, "X / Y", x, y); AddRow(tools, T("幅 / 高さ", "Width / Height"), width, height);
        AddRow(tools, T("ラベルサイズ", "Label size"), labelFontSize);
        AddRow(tools, T("値サイズ", "Value size"), fontSize);
        AddRow(tools, T("配置", "Alignment"), centerValue);
        AddRow(tools, T("装飾", "Style"), showPanel);
        AddRow(tools, T("色", "Color"), color, valueColor);

        AddSection(tools, T("部分アニメーション", "Partial animation"));
        tools.Controls.Add(animations);
        var chooseGif = new Button { Text = T("GIFを追加", "Add GIF"), AutoSize = true };
        var clearGif = new Button { Text = T("選択GIFを削除", "Delete selected GIF"), AutoSize = true };
        AddRow(tools, "GIF", chooseGif, clearGif); tools.Controls.Add(animationName);
        AddRow(tools, "X / Y", gifX, gifY); AddRow(tools, T("幅 / 高さ", "Width / Height"), gifWidth, gifHeight);
        AddRow(tools, T("間隔ms", "Interval ms"), gifInterval);

        var save = new Button { Text = T("テーマ一覧へ保存", "Save to theme list"), AutoSize = true, BackColor = Color.FromArgb(0, 105, 140), ForeColor = Color.White };
        var export = new Button { Text = T("ファイルへ書き出し", "Export to file"), AutoSize = true };
        var preview = new Button { Text = T("Windowsプレビュー", "Windows preview"), AutoSize = true };
        AddRow(tools, T("プレビュー", "Preview"), preview);
        AddRow(tools, "", save, export);

        canvas.SelectionChanged += (_, _) => SelectElement(canvas.SelectedElement);
        canvas.AnimationSelectionChanged += (_, _) => SelectAnimation(canvas.SelectedAnimation);
        canvas.ThemeChanged += (_, _) => {
            if (canvas.SelectedAnimation is not null) LoadAnimationValues();
            else LoadElementValues();
            RefreshLivePreview();
        };
        elements.SelectedIndexChanged += (_, _) => { if (!updating) canvas.SelectedElement = elements.SelectedItem as LuneThemeElement; };
        add.Click += (_, _) => AddElement(); remove.Click += (_, _) => RemoveElement();
        backgroundButton.Click += (_, _) => ChooseBackground(); clearBackground.Click += (_, _) => { theme.BackgroundImageBase64 = ""; canvas.Invalidate(); RefreshLivePreview(true); };
        animations.SelectedIndexChanged += (_, _) => {
            if (!updating) canvas.SelectedAnimation = animations.SelectedItem as LuneThemeAnimation;
            LoadAnimationValues();
        };
        chooseGif.Click += (_, _) => ChooseGif();
        clearGif.Click += (_, _) => RemoveSelectedGif();
        color.Click += (_, _) => ChooseColor();
        valueColor.Click += (_, _) => ChooseValueColor();
        save.Click += (_, _) => SaveInstalled(); export.Click += (_, _) => ExportTheme();
        preview.Click += (_, _) => ShowLivePreview();
        name.TextChanged += (_, _) => { if (!updating) theme.Name = name.Text; };
        orientation.SelectedIndexChanged += (_, _) => ChangeOrientation();
        reverse.CheckedChanged += (_, _) => { if (!updating) theme.Reverse = reverse.Checked; };
        brightness.ValueChanged += (_, _) => { if (!updating) theme.Brightness = (int)brightness.Value; };
        foreach (var control in new Control[] { metric, label, staticText, x, y, width, height,
                     labelFontSize, fontSize, centerValue, showPanel })
            WireElementChange(control);
        foreach (var control in new Control[] { gifX, gifY, gifWidth, gifHeight, gifInterval })
            if (control is NumericUpDown numeric) numeric.ValueChanged += (_, _) => UpdateAnimation();
        LoadThemeValues();
        FormClosed += (_, _) => {
            if (previewWindow is { IsDisposed: false }) previewWindow.Close();
            previewWindow = null;
        };
    }

    private string T(string japanese, string englishText) => english ? englishText : japanese;

    private static NumericUpDown Number(decimal min, decimal max, decimal value, decimal increment = 1) => new() {
        Minimum = min, Maximum = max, Value = value, Increment = increment, Width = 66
    };
    private static void AddSection(Control parent, string text) => parent.Controls.Add(new Label {
        Text = text, AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold),
        ForeColor = Color.LightSkyBlue, Margin = new Padding(0, 12, 0, 3)
    });
    private static void AddRow(Control parent, string text, params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 2, 0, 2) };
        if (text.Length > 0) row.Controls.Add(new Label { Text = text, Width = 72, Padding = new Padding(0, 5, 0, 0) });
        row.Controls.AddRange(controls); parent.Controls.Add(row);
    }

    private void LoadThemeValues()
    {
        updating = true;
        name.Text = theme.Name; orientation.SelectedIndex = theme.Landscape ? 0 : 1;
        reverse.Checked = theme.Reverse; brightness.Value = theme.Brightness;
        updating = false;
        canvas.Theme = theme; ReloadElements(); ReloadAnimations();
    }
    private void ReloadElements()
    {
        updating = true; elements.Items.Clear();
        foreach (var item in theme.Elements) elements.Items.Add(item);
        elements.DisplayMember = nameof(LuneThemeElement.Label);
        updating = false;
        if (elements.Items.Count > 0) elements.SelectedIndex = 0;
    }
    private void SelectElement(LuneThemeElement? item)
    {
        updating = true; elements.SelectedItem = item; updating = false; LoadElementValues();
    }
    private void SelectAnimation(LuneThemeAnimation? item)
    {
        updating = true; animations.SelectedItem = item; updating = false; LoadAnimationValues();
    }
    private void LoadElementValues()
    {
        var item = canvas.SelectedElement;
        updating = true;
        foreach (var control in new Control[] { metric, label, staticText, x, y, width, height,
                     labelFontSize, fontSize, centerValue, showPanel, color, valueColor })
            control.Enabled = item is not null;
        if (item is not null) {
            metric.SelectedItem = metric.Items.Cast<MetricChoice>().First(choice => choice.Value == item.Metric);
            label.Text = item.Label; staticText.Text = item.Text;
            x.Value = item.X; y.Value = item.Y; width.Value = item.Width; height.Value = item.Height;
            labelFontSize.Value = (decimal)(item.LabelFontSize ?? Math.Max(8, item.FontSize * .36f));
            centerValue.Checked = item.CenterValue; fontSize.Value = (decimal)item.FontSize; showPanel.Checked = item.ShowPanel;
            color.BackColor = Color.FromArgb(item.ColorArgb);
            valueColor.BackColor = Color.FromArgb(item.ValueColorArgb);
        }
        updating = false;
    }
    private void WireElementChange(Control control)
    {
        switch (control) {
            case NumericUpDown n: n.ValueChanged += (_, _) => UpdateElement(); break;
            case ComboBox c: c.SelectedIndexChanged += (_, _) => UpdateElement(); break;
            case TextBox t: t.TextChanged += (_, _) => UpdateElement(); break;
            case CheckBox c: c.CheckedChanged += (_, _) => UpdateElement(); break;
        }
    }
    private void UpdateElement()
    {
        if (updating || canvas.SelectedElement is not { } item) return;
        item.Metric = metric.SelectedItem is MetricChoice choice ? choice.Value : item.Metric;
        item.Label = label.Text; item.Text = staticText.Text;
        item.Width = Math.Min((int)width.Value, theme.Width); item.Height = Math.Min((int)height.Value, theme.Height);
        item.X = Math.Clamp((int)x.Value, 0, theme.Width - item.Width);
        item.Y = Math.Clamp((int)y.Value, 0, theme.Height - item.Height);
        item.LabelFontSize = (float)labelFontSize.Value;
        item.CenterValue = centerValue.Checked; item.FontSize = (float)fontSize.Value; item.ShowPanel = showPanel.Checked;
        ReloadElementNames(item); canvas.Invalidate(); RefreshLivePreview();
    }
    private void ReloadElementNames(LuneThemeElement selected)
    {
        var index = theme.Elements.IndexOf(selected);
        updating = true; elements.Items.Clear(); foreach (var item in theme.Elements) elements.Items.Add(item);
        elements.SelectedIndex = index; updating = false;
    }
    private void AddElement()
    {
        var item = new LuneThemeElement { X = 30 + theme.Elements.Count * 8, Y = 30 + theme.Elements.Count * 8 };
        theme.Elements.Add(item); ReloadElements(); elements.SelectedItem = item; canvas.SelectedElement = item;
        RefreshLivePreview();
    }
    private void RemoveElement()
    {
        if (canvas.SelectedElement is not { } item) return;
        theme.Elements.Remove(item); canvas.SelectedElement = null; ReloadElements(); canvas.Invalidate();
        RefreshLivePreview();
    }
    private void ChangeOrientation()
    {
        if (updating || orientation.SelectedIndex < 0) return;
        theme.Landscape = orientation.SelectedIndex == 0;
        foreach (var item in theme.Elements) {
            item.Width = Math.Min(item.Width, theme.Width); item.Height = Math.Min(item.Height, theme.Height);
            item.X = Math.Clamp(item.X, 0, theme.Width - item.Width); item.Y = Math.Clamp(item.Y, 0, theme.Height - item.Height);
        }
        foreach (var animation in theme.Animations) {
            animation.Width = Math.Min(animation.Width, theme.Width);
            animation.Height = Math.Min(animation.Height, theme.Height);
            animation.X = Math.Clamp(animation.X, 0, theme.Width - animation.Width);
            animation.Y = Math.Clamp(animation.Y, 0, theme.Height - animation.Height);
        }
        canvas.Theme = theme; ReloadElements(); RefreshLivePreview(true);
    }
    private void ChooseBackground()
    {
        using var dialog = new OpenFileDialog {
            Filter = T("画像|*.png;*.jpg;*.jpeg;*.bmp", "Images|*.png;*.jpg;*.jpeg;*.bmp"),
            Title = T("背景画像を選択", "Choose background image"),
            InitialDirectory = Path.Combine(LuneThemeStore.WriteAssets(theme, false), "Backgrounds")
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        using var original = Image.FromFile(dialog.FileName);
        using var bitmap = new Bitmap(original, theme.Width, theme.Height);
        using var stream = new MemoryStream(); bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        theme.BackgroundImageBase64 = Convert.ToBase64String(stream.ToArray()); canvas.Invalidate();
        RefreshLivePreview(true);
    }
    private void ChooseGif()
    {
        using var dialog = new OpenFileDialog {
            Filter = T("GIFアニメーション|*.gif", "GIF animation|*.gif"),
            Title = T("小さなGIFを選択", "Choose a small GIF"),
            InitialDirectory = Path.Combine(LuneThemeStore.WriteAssets(theme, false), "GIFs")
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var offset = theme.Animations.Count * 12;
        var animation = new LuneThemeAnimation {
            Name = Path.GetFileName(dialog.FileName),
            GifBase64 = Convert.ToBase64String(File.ReadAllBytes(dialog.FileName)),
            X = Math.Clamp(190 + offset, 0, Math.Max(0, theme.Width - 100)),
            Y = Math.Clamp(105 + offset, 0, Math.Max(0, theme.Height - 100))
        };
        theme.Animations.Add(animation);
        ReloadAnimations(animation);
        canvas.SelectedAnimation = animation;
        canvas.Invalidate();
        RefreshLivePreview(true);
    }

    private void RemoveSelectedGif()
    {
        if (animations.SelectedItem is not LuneThemeAnimation animation) return;
        theme.Animations.Remove(animation);
        if (ReferenceEquals(canvas.SelectedAnimation, animation)) canvas.SelectedAnimation = null;
        ReloadAnimations();
        canvas.Invalidate();
        RefreshLivePreview(true);
    }

    private void ReloadAnimations(LuneThemeAnimation? selected = null)
    {
        updating = true;
        animations.Items.Clear();
        foreach (var animation in theme.Animations) animations.Items.Add(animation);
        updating = false;
        if (animations.Items.Count > 0)
            animations.SelectedItem = selected ?? animations.Items[0];
        else {
            canvas.SelectedAnimation = null;
            LoadAnimationValues();
        }
    }

    private void LoadAnimationValues()
    {
        var animation = animations.SelectedItem as LuneThemeAnimation;
        updating = true;
        foreach (var control in new Control[] { gifX, gifY, gifWidth, gifHeight, gifInterval })
            control.Enabled = animation is not null;
        if (animation is null) {
            animationName.Text = T("GIFなし", "No GIF");
        } else {
            animationName.Text = animation.Name;
            gifX.Value = animation.X; gifY.Value = animation.Y;
            gifWidth.Value = animation.Width; gifHeight.Value = animation.Height;
            gifInterval.Value = animation.FrameIntervalMs;
        }
        updating = false;
    }
    private void UpdateAnimation()
    {
        if (updating || animations.SelectedItem is not LuneThemeAnimation animation) return;
        animation.X = (int)gifX.Value; animation.Y = (int)gifY.Value;
        animation.Width = (int)gifWidth.Value; animation.Height = (int)gifHeight.Value;
        animation.FrameIntervalMs = (int)gifInterval.Value; canvas.Invalidate();
        RefreshLivePreview();
    }
    private void ChooseColor()
    {
        if (canvas.SelectedElement is not { } item) return;
        using var dialog = new ColorDialog { Color = Color.FromArgb(item.ColorArgb), FullOpen = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        item.ColorArgb = dialog.Color.ToArgb(); color.BackColor = dialog.Color; canvas.Invalidate();
        RefreshLivePreview();
    }
    private void ChooseValueColor()
    {
        if (canvas.SelectedElement is not { } item) return;
        using var dialog = new ColorDialog { Color = Color.FromArgb(item.ValueColorArgb), FullOpen = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        item.ValueColorArgb = dialog.Color.ToArgb(); valueColor.BackColor = dialog.Color; canvas.Invalidate();
        RefreshLivePreview();
    }

    private void ShowLivePreview()
    {
        if (previewWindow is null || previewWindow.IsDisposed) {
            previewWindow = new LunePanelWindow(LunePanelTheme.Custom, theme.Landscape, theme) {
                TopMost = TopMost
            };
            previewWindow.FormClosed += (_, _) => previewWindow = null;
            previewWindow.Show(this);
        }
        RefreshLivePreview(true);
        previewWindow.Activate();
    }

    private void RefreshLivePreview(bool reloadRenderer = false)
    {
        if (previewWindow is null || previewWindow.IsDisposed) return;
        if (reloadRenderer)
            previewWindow.ApplyTheme(LunePanelTheme.Custom, theme.Landscape, theme);
        else
            previewWindow.RefreshThemeLayout();
        previewWindow.UpdateTelemetry(previewTelemetry);
    }
    private void SaveInstalled()
    {
        try {
            theme.Name = name.Text.Trim();
            SavedPath = LuneThemeStore.Install(theme);
            MessageBox.Show(this, T("テーマ一覧へ保存しました。", "Saved to the theme list."), "LUNE Display Studio",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        } catch (Exception ex) {
            MessageBox.Show(this, ex.Message, T("テーマ保存", "Save Theme"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    private void ExportTheme()
    {
        theme.Name = name.Text.Trim();
        using var dialog = new SaveFileDialog { Filter = T("LUNEテーマ|*.lunetheme", "LUNE theme|*.lunetheme"), DefaultExt = "lunetheme",
            FileName = theme.Name + ".lunetheme", InitialDirectory = LuneThemeStore.ThemeFolder };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { LuneThemeStore.Save(dialog.FileName, theme); SavedPath = dialog.FileName; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, T("テーマ保存", "Save Theme"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

