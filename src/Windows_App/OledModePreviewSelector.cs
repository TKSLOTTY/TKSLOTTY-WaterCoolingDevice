using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace WaterCoolingDevice;

internal sealed class OledModePreviewSelector : Control
{
    private const int ModeCount = 6;
    private static readonly Color OledPixelColor = Color.FromArgb(45, 170, 255);
    private readonly System.Windows.Forms.Timer previewTimer = new() { Interval = 500 };
    private int selectedMode = 5;
    private int pageIntervalSeconds = 8;
    private int hoverMode = -1;
    private bool english;
    private bool hardwareTemperaturesEnabled = true;
    private float? waterTemperature;
    private float? cpuTemperature;
    private float? gpuTemperature;
    private float? cpuUsage;
    private float? memoryUsage;
    private int? duty1;
    private int? duty2;
    private int? rpm1;
    private int? rpm2;
    private bool pumpMode;
    private int[] duty1Table = { 10, 20, 30, 50, 70 };
    private int[] duty2Table = { 10, 20, 30, 50, 70 };

    public event EventHandler? SelectedModeChanged;

    public OledModePreviewSelector()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = true;
        BackColor = Color.FromArgb(30, 33, 39);
        ForeColor = Color.Gainsboro;
        previewTimer.Tick += (_, _) => Invalidate();
        previewTimer.Start();
        Disposed += (_, _) => previewTimer.Dispose();
    }

    public int SelectedMode
    {
        get => selectedMode;
        set
        {
            var next = Math.Clamp(value, 0, ModeCount - 1);
            if (!IsModeSelectable(next)) next = 0;
            if (selectedMode == next) return;
            selectedMode = next;
            Invalidate();
            SelectedModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool English
    {
        get => english;
        set { english = value; Invalidate(); }
    }

    public bool HardwareTemperaturesEnabled
    {
        get => hardwareTemperaturesEnabled;
        set
        {
            if (hardwareTemperaturesEnabled == value) return;
            hardwareTemperaturesEnabled = value;
            if (!IsModeSelectable(selectedMode)) SelectedMode = 0;
            hoverMode = -1;
            Invalidate();
        }
    }

    public int PageIntervalSeconds
    {
        get => pageIntervalSeconds;
        set
        {
            pageIntervalSeconds = Math.Clamp(value, 3, 30);
            Invalidate();
        }
    }

    public void SetDeviceValues(float? water, int? fan1Duty, int? fan2Duty,
                                int? fan1Rpm, int? fan2Rpm, bool isPump)
    {
        waterTemperature = water;
        duty1 = fan1Duty;
        duty2 = fan2Duty;
        rpm1 = fan1Rpm;
        rpm2 = fan2Rpm;
        pumpMode = isPump;
        Invalidate();
    }

    public void SetHostTemperatures(float? cpu, float? gpu)
    {
        cpuTemperature = cpu;
        gpuTemperature = gpu;
        Invalidate();
    }

    public void SetSystemUsage(float? cpu, float? memory)
    {
        cpuUsage = cpu;
        memoryUsage = memory;
        Invalidate();
    }

    public void SetDutyTables(int[] fan1, int[] fan2)
    {
        duty1Table = (int[])fan1.Clone();
        duty2Table = (int[])fan2.Clone();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        for (var mode = 0; mode < ModeCount; mode++)
            DrawModeTile(e.Graphics, mode, GetTileRectangle(mode));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left) return;
        var mode = HitTest(e.Location);
        if (mode < 0 || !IsModeSelectable(mode)) return;
        Focus();
        SelectedMode = mode;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var next = Enabled ? HitTest(e.Location) : -1;
        if (!IsModeSelectable(next)) next = -1;
        if (hoverMode == next) return;
        hoverMode = next;
        Cursor = hoverMode >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hoverMode = -1;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down ||
        base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Enabled) return;
        var next = selectedMode;
        if (e.KeyCode == Keys.Left) next--;
        else if (e.KeyCode == Keys.Right) next++;
        else if (e.KeyCode == Keys.Up) next -= 3;
        else if (e.KeyCode == Keys.Down) next += 3;
        else return;
        next = Math.Clamp(next, 0, ModeCount - 1);
        if (IsModeSelectable(next)) SelectedMode = next;
        e.Handled = true;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    private Rectangle GetTileRectangle(int mode)
    {
        const int gap = 10;
        var tileWidth = Math.Max(1, (ClientSize.Width - gap * 2) / 3);
        var tileHeight = Math.Max(1, (ClientSize.Height - gap) / 2);
        var column = mode % 3;
        var row = mode / 3;
        return new Rectangle(column * (tileWidth + gap), row * (tileHeight + gap),
                             tileWidth, tileHeight);
    }

    private int HitTest(Point location)
    {
        for (var mode = 0; mode < ModeCount; mode++)
            if (GetTileRectangle(mode).Contains(location)) return mode;
        return -1;
    }

    private bool IsModeSelectable(int mode) =>
        mode >= 0 && mode < ModeCount &&
        (hardwareTemperaturesEnabled || mode != 5);

    private void DrawModeTile(Graphics graphics, int mode, Rectangle tile)
    {
        var selected = mode == selectedMode;
        var hovered = mode == hoverMode;
        var borderColor = selected ? Color.DeepSkyBlue
            : hovered ? Color.Silver : Color.FromArgb(75, 82, 94);
        var fillColor = selected ? Color.FromArgb(38, 53, 66)
            : hovered ? Color.FromArgb(43, 47, 55) : Color.FromArgb(35, 38, 45);

        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(borderColor, selected ? 3f : 1f);
        graphics.FillRectangle(fill, tile);
        graphics.DrawRectangle(border, Rectangle.Inflate(tile, -1, -1));

        const int labelHeight = 25;
        var available = new Rectangle(tile.X + 8, tile.Y + 7,
            Math.Max(1, tile.Width - 16), Math.Max(1, tile.Height - labelHeight - 12));
        var previewWidth = Math.Min(available.Width, available.Height * 2);
        var previewHeight = previewWidth / 2;
        var preview = new Rectangle(
            available.X + (available.Width - previewWidth) / 2,
            available.Y + (available.Height - previewHeight) / 2,
            previewWidth, previewHeight);

        using var oledBorder = new Pen(Color.FromArgb(95, 105, 120));
        graphics.FillRectangle(Brushes.Black, preview);
        graphics.DrawRectangle(oledBorder, preview);
        DrawOledScreen(graphics, preview, mode);

        var namesJa = new[] {
            "自動：全画面切替", "総合表示", "FAN1カーブ",
            "FAN2／PUMPカーブ", "CPU／メモリ使用率", "CPU／GPU温度"
        };
        var namesEn = new[] {
            "Auto: All Available", "Status Overview", "Fan 1 Curve",
            "Fan 2 / Pump Curve", "CPU / Memory Usage", "CPU / GPU Temp"
        };
        var label = english ? namesEn[mode] : namesJa[mode];
        var labelRect = new Rectangle(tile.X + 4, tile.Bottom - labelHeight,
                                      tile.Width - 8, labelHeight - 2);
        var modeEnabled = Enabled && IsModeSelectable(mode);
        var labelColor = modeEnabled ? (selected ? Color.White : Color.Gainsboro) : Color.Gray;
        TextRenderer.DrawText(graphics, label, Font, labelRect, labelColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

        if (selected)
        {
            using var checkBrush = new SolidBrush(Color.DeepSkyBlue);
            using var checkFont = new Font("Segoe UI", 8f, FontStyle.Bold);
            graphics.FillEllipse(checkBrush, tile.Right - 21, tile.Y + 6, 14, 14);
            TextRenderer.DrawText(graphics, "✓", checkFont,
                new Rectangle(tile.Right - 22, tile.Y + 4, 16, 17), Color.Black,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        if (!modeEnabled)
        {
            using var disabled = new SolidBrush(Color.FromArgb(105, BackColor));
            graphics.FillRectangle(disabled, tile);
            if (Enabled)
            {
                var message = english ? "Temperature acquisition OFF" : "温度取得 OFF";
                TextRenderer.DrawText(graphics, message, Font, tile, Color.LightGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine);
            }
        }
    }

    private void DrawOledScreen(Graphics graphics, Rectangle target, int mode)
    {
        var state = graphics.Save();
        graphics.SetClip(target);
        graphics.TranslateTransform(target.X, target.Y);
        graphics.ScaleTransform(target.Width / 128f, target.Height / 64f);
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        var pageDurationMilliseconds = (long)pageIntervalSeconds * 1000L;
        var page = mode;
        if (mode == 0)
        {
            var pages = new List<int> { 1, 2, 3 };
            if (cpuUsage.HasValue && memoryUsage.HasValue)
            {
                pages.Add(6);
                pages.Add(4);
                if (hardwareTemperaturesEnabled &&
                    (cpuTemperature.HasValue || gpuTemperature.HasValue))
                    pages.Add(5);
            }
            page = pages[(int)((Environment.TickCount64 / pageDurationMilliseconds) %
                               pages.Count)];
        }

        if (page == 6) DrawWaterTemperature(graphics);
        else if (page == 1) DrawSummary(graphics);
        else if (page == 2) DrawFan(graphics, false);
        else if (page == 3) DrawFan(graphics, true);
        else if (page == 4) DrawSystemUsage(graphics);
        else if (page == 5) DrawPcTemperatures(graphics);

        graphics.Restore(state);
    }

    private void DrawSummary(Graphics graphics)
    {
        DrawText(graphics, $"TEMP: {TemperatureText(waterTemperature)} °C", 0, 0, 7);
        DrawText(graphics,
            $"FAN1:{ValueText(duty1, 3)} % {(pumpMode ? "PUMP" : "FAN2")}:{ValueText(duty2, 3)} %",
            0, 9, 6.2f);
        DrawText(graphics, $"RPM1 {ValueText(rpm1, 1)}", 0, 25, 14);
        DrawText(graphics, $"RPM2 {ValueText(rpm2, 1)}", 0, 45, 14);
    }

    private void DrawWaterTemperature(Graphics graphics)
    {
        DrawCentered(graphics, "WATER TEMPERATURE", 2, 7);
        DrawLargeTemperature(graphics, waterTemperature);
    }

    private void DrawSystemUsage(Graphics graphics)
    {
        DrawCentered(graphics, "SYSTEM USAGE", 0, 7);
        if (cpuUsage is not null)
        {
            DrawText(graphics, "CPU", 0, 21, 7);
            DrawText(graphics, $"{cpuUsage:0.0}", 32, 16, 15, FontStyle.Bold);
            DrawText(graphics, "%", 91, 21, 7);
        }
        if (memoryUsage is not null)
        {
            DrawText(graphics, "MEM", 0, 47, 7);
            DrawText(graphics, $"{memoryUsage:0.0}", 32, 42, 15, FontStyle.Bold);
            DrawText(graphics, "%", 91, 47, 7);
        }
    }

    private void DrawFanOverview(Graphics graphics)
    {
        DrawText(graphics, $"WATER {TemperatureText(waterTemperature)} C", 0, 0, 7);
        DrawText(graphics, "DUTY", 19, 12, 7);
        DrawText(graphics, "RPM", 82, 12, 7);
        DrawText(graphics, "F1", 0, 27, 7);
        DrawText(graphics, ValueText(duty1, 1), 18, 22, 15, FontStyle.Bold);
        DrawText(graphics, "%", duty1 is >= 100 ? 55 : duty1 is >= 10 ? 43 : 31,
            27, 7);
        DrawText(graphics, ValueText(rpm1, 4), 70, 22, 15, FontStyle.Bold);
        DrawText(graphics, pumpMode ? "P" : "F2", 0, 50, 7);
        DrawText(graphics, ValueText(duty2, 1), 18, 45, 15, FontStyle.Bold);
        DrawText(graphics, "%", duty2 is >= 100 ? 55 : duty2 is >= 10 ? 43 : 31,
            50, 7);
        DrawText(graphics, ValueText(rpm2, 4), 70, 45, 15, FontStyle.Bold);
    }

    private static void DrawHostTemperature(
        Graphics graphics, string name, float? temperature)
    {
        DrawCentered(graphics, $"{name} TEMPERATURE", 0, 7);
        using var pixelPen = new Pen(OledPixelColor);
        graphics.DrawLine(pixelPen, 0, 10, 128, 10);
        if (temperature is null) return;
        DrawLargeTemperature(graphics, temperature);
    }

    private static void DrawLargeTemperature(Graphics graphics, float? temperature)
    {
        var value = TemperatureText(temperature);
        using var font = new Font(FontFamily.GenericMonospace, 21, FontStyle.Bold,
                                  GraphicsUnit.Pixel);
        var valueWidth = graphics.MeasureString(value, font, PointF.Empty,
            StringFormat.GenericTypographic).Width;
        var x = Math.Max(0, (128 - valueWidth - 24) / 2);
        DrawText(graphics, value, x, 20, 21, FontStyle.Bold);
        using var pixelPen = new Pen(OledPixelColor);
        graphics.DrawEllipse(pixelPen, x + valueWidth + 4, 23, 4, 4);
        DrawText(graphics, "C", x + valueWidth + 13, 26, 14, FontStyle.Bold);
    }

    private void DrawFanSpeeds(Graphics graphics)
    {
        DrawCentered(graphics, "FAN SPEED", 0, 7);
        using var pixelPen = new Pen(OledPixelColor);
        graphics.DrawLine(pixelPen, 0, 10, 128, 10);
        DrawText(graphics, "FAN1", 0, 18, 7);
        DrawText(graphics, ValueText(rpm1, 4), 30, 12, 15, FontStyle.Bold);
        DrawText(graphics, "RPM", 84, 18, 7);
        DrawText(graphics, pumpMode ? "PUMP" : "FAN2", 0, 44, 7);
        DrawText(graphics, ValueText(rpm2, 4), 30, 38, 15, FontStyle.Bold);
        DrawText(graphics, "RPM", 84, 44, 7);
    }

    private void DrawPcTemperatures(Graphics graphics)
    {
        DrawCentered(graphics, "PC TEMPERATURE", 0, 7);
        using var pixelPen = new Pen(OledPixelColor);
        graphics.DrawLine(pixelPen, 0, 10, 128, 10);
        if (cpuTemperature is not null && gpuTemperature is not null)
        {
            DrawTemperatureRow(graphics, "CPU", 16, cpuTemperature);
            DrawTemperatureRow(graphics, "GPU", 41, gpuTemperature);
        }
        else if (cpuTemperature is not null)
        {
            DrawTemperatureRow(graphics, "CPU", 28, cpuTemperature);
        }
        else if (gpuTemperature is not null)
        {
            DrawTemperatureRow(graphics, "GPU", 28, gpuTemperature);
        }
    }

    private static void DrawTemperatureRow(Graphics graphics, string label, int y, float? value)
    {
        if (value is null) return;
        DrawText(graphics, label, 0, y + 4, 7);
        DrawText(graphics, TemperatureText(value), 40, y, 15, FontStyle.Bold);
        using var pixelPen = new Pen(OledPixelColor);
        graphics.DrawEllipse(pixelPen, 89, y + 1, 3, 3);
        DrawText(graphics, "C", 94, y + 4, 7);
    }

    private void DrawFan(Graphics graphics, bool second)
    {
        var name = second ? (pumpMode ? "PUMP" : "FAN2") : "FAN1";
        var duty = second ? duty2 : duty1;
        DrawText(graphics, $"{name} TEMP: {TemperatureText(waterTemperature)} °C", 0, 0, 6.5f);
        DrawText(graphics, $"     DUTY:  {ValueText(duty, 3)}  %", 0, 9, 6.5f);

        if (second && pumpMode)
        {
            return;
        }

        DrawCurve(graphics, second ? duty2Table : duty1Table, duty);
    }

    private void DrawCurve(Graphics graphics, int[] table, int? currentDuty)
    {
        const int originX = 6;
        const int originY = 51;
        const int width = 112;
        const int height = 30;
        var temperatures = new[] { 20, 30, 40, 50, 60 };
        var points = new Point[5];
        for (var i = 0; i < points.Length; i++)
        {
            var tableDuty = i < table.Length ? Math.Clamp(table[i], 0, 100) : 0;
            points[i] = new Point(originX + i * width / 4,
                                  originY - tableDuty * height / 100);
        }

        using var pixelPen = new Pen(OledPixelColor);
        using var pixelBrush = new SolidBrush(OledPixelColor);
        graphics.DrawLines(pixelPen, points);
        foreach (var point in points) graphics.FillEllipse(pixelBrush, point.X - 1, point.Y - 1, 3, 3);
        DrawText(graphics, "%", 0, 18, 6);
        DrawText(graphics, "° C", 113, 47, 6);
        for (var i = 0; i < temperatures.Length; i++)
            DrawText(graphics, temperatures[i].ToString(), points[i].X - 5, 55, 5.5f);

        if (currentDuty is not null)
        {
            var temperature = Math.Clamp(waterTemperature ?? 32f, 20f, 60f);
            var x = originX + (int)Math.Round((temperature - 20f) * width / 40f);
            var y = originY - Math.Clamp(currentDuty.Value, 0, 100) * height / 100;
            using var guide = new Pen(Color.FromArgb(190, OledPixelColor));
            graphics.DrawLine(guide, x, originY - height, x, originY);
            graphics.DrawLine(guide, 0, y, 128, y);
            if ((Environment.TickCount64 / 500) % 2 == 0)
                graphics.DrawEllipse(pixelPen, x - 3, y - 3, 6, 6);
        }
    }

    private static string TemperatureText(float? value) =>
        value is { } temperature ? temperature.ToString("0.0") : "--.-";

    private static string ValueText(int? value, int width) =>
        value is { } number ? number.ToString().PadLeft(width) : new string('-', width);

    private static void DrawCentered(Graphics graphics, string text, float y, float size)
    {
        using var font = new Font(FontFamily.GenericMonospace, size, FontStyle.Regular,
                                  GraphicsUnit.Pixel);
        var width = graphics.MeasureString(text, font, PointF.Empty,
            StringFormat.GenericTypographic).Width;
        DrawText(graphics, text, Math.Max(0, (128 - width) / 2), y, size);
    }

    private static void DrawText(Graphics graphics, string text, float x, float y,
                                 float size, FontStyle style = FontStyle.Regular)
    {
        using var font = new Font(FontFamily.GenericMonospace, size, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(OledPixelColor);
        graphics.DrawString(text, font, brush, x, y, StringFormat.GenericTypographic);
    }
}
