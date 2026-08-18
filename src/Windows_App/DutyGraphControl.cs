using System.Drawing.Drawing2D;

namespace WaterCoolingDevice;

internal sealed class DutyGraphControl : Control
{
    private readonly int[] temperatures = { 20, 30, 40, 50, 60 };
    private int[] duty1 = { 10, 20, 25, 40, 70 };
    private int[] duty2 = { 10, 20, 25, 40, 70 };
    private float waterTemperature = 25;
    private int currentDuty1;
    private int currentDuty2;
    private bool pumpMode;
    private int pumpDuty = 60;
    private int dragFan;
    private int dragPoint = -1;

    public bool Editable { get; set; }
    public event EventHandler<DutyPointChangedEventArgs>? DutyPointChanged;

    public DutyGraphControl()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24, 27, 32);
        ForeColor = Color.Gainsboro;
        MinimumSize = new Size(500, 300);
        MouseDown += OnGraphMouseDown;
        MouseMove += OnGraphMouseMove;
        MouseUp += (_, _) => { dragFan = 0; dragPoint = -1; Cursor = Cursors.Default; };
    }

    public void SetTables(int[] table1, int[] table2)
    {
        duty1 = (int[])table1.Clone();
        duty2 = (int[])table2.Clone();
        Invalidate();
    }

    public void SetCurrent(float temperature, int fan1Duty, int fan2Duty)
    {
        waterTemperature = temperature;
        currentDuty1 = fan1Duty;
        currentDuty2 = fan2Duty;
        Invalidate();
    }

    public void SetPumpMode(bool enabled, int duty)
    {
        pumpMode = enabled;
        pumpDuty = Math.Clamp(duty, 35, 100);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        const int left = 64, top = 28, right = 28, bottom = 52;
        var plot = new Rectangle(left, top,
            Math.Max(1, Width - left - right), Math.Max(1, Height - top - bottom));

        using var gridPen = new Pen(Color.FromArgb(60, 68, 78));
        using var axisPen = new Pen(Color.FromArgb(150, 160, 170), 1.5f);
        using var labelBrush = new SolidBrush(ForeColor);
        using var fan1Pen = new Pen(Color.FromArgb(0, 190, 255), 3f);
        using var fan2Pen = new Pen(Color.FromArgb(255, 110, 90), 3f) {
            DashStyle = DashStyle.Dash
        };
        using var currentTempPen = new Pen(Color.FromArgb(255, 220, 80), 1.5f) { DashStyle = DashStyle.Dash };

        for (var duty = 0; duty <= 100; duty += 20)
        {
            var y = MapY(duty, plot);
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            g.DrawString($"{duty}%", Font, labelBrush, 12, y - Font.Height / 2f);
        }
        foreach (var temp in temperatures)
        {
            var x = MapX(temp, plot);
            g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
            var text = $"{temp}°C";
            var size = g.MeasureString(text, Font);
            g.DrawString(text, Font, labelBrush, x - size.Width / 2, plot.Bottom + 10);
        }

        g.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
        g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        DrawCurve(g, plot, duty1, fan1Pen, Color.FromArgb(0, 190, 255), false);
        var displayedDuty2 = pumpMode
            ? Enumerable.Repeat(pumpDuty, temperatures.Length).ToArray()
            : duty2;
        DrawCurve(g, plot, displayedDuty2, fan2Pen, Color.FromArgb(255, 110, 90), true);

        if (!Editable && float.IsFinite(waterTemperature)) {
            var currentX = MapX(Math.Clamp(waterTemperature, 20, 60), plot);
            g.DrawLine(currentTempPen, currentX, plot.Top, currentX, plot.Bottom);
            var fan1Y = MapY(currentDuty1, plot);
            var fan2Y = MapY(currentDuty2, plot);
            var blinkPhase = (Environment.TickCount64 / 500) % 2 == 0;
            var markersOverlap = Math.Abs(fan1Y - fan2Y) <= 12f;

            if (markersOverlap) {
                // Only overlapping markers alternate, so both colors can be
                // identified without one marker permanently hiding the other.
                if (blinkPhase)
                    DrawCurrentPoint(g, currentX, fan1Y, Color.FromArgb(0, 190, 255));
                else
                    DrawCurrentPoint(g, currentX, fan2Y, Color.FromArgb(255, 110, 90));
            } else if (blinkPhase) {
                // Separate markers blink together to avoid distracting
                // left/right alternating flicker.
                DrawCurrentPoint(g, currentX, fan1Y, Color.FromArgb(0, 190, 255));
                DrawCurrentPoint(g, currentX, fan2Y, Color.FromArgb(255, 110, 90));
            }
        }

        g.FillRectangle(new SolidBrush(Color.FromArgb(0, 190, 255)), plot.Right - 185, 8, 18, 4);
        g.DrawString("FAN1", Font, labelBrush, plot.Right - 160, 1);
        g.FillRectangle(new SolidBrush(Color.FromArgb(255, 110, 90)), plot.Right - 90, 8, 18, 4);
        g.DrawString(pumpMode ? "PUMP" : "FAN2", Font, labelBrush, plot.Right - 65, 1);
    }

    private void DrawCurve(Graphics g, Rectangle plot, int[] values, Pen pen,
                           Color color, bool squareMarkers)
    {
        var points = temperatures.Select((temp, i) =>
            new PointF(MapX(temp, plot), MapY(values[i], plot))).ToArray();
        g.DrawLines(pen, points);
        using var brush = new SolidBrush(color);
        foreach (var point in points) {
            if (squareMarkers) g.FillRectangle(brush, point.X - 4, point.Y - 4, 8, 8);
            else g.FillEllipse(brush, point.X - 4, point.Y - 4, 8, 8);
        }
    }

    private static void DrawCurrentPoint(Graphics g, float x, float y, Color color)
    {
        using var brush = new SolidBrush(color);
        using var border = new Pen(Color.White, 2);
        g.FillEllipse(brush, x - 6, y - 6, 12, 12);
        g.DrawEllipse(border, x - 6, y - 6, 12, 12);
    }

    private static float MapX(float value, Rectangle plot) =>
        plot.Left + (value - 20f) / 40f * plot.Width;
    private static float MapY(float value, Rectangle plot) =>
        plot.Bottom - Math.Clamp(value, 0, 100) / 100f * plot.Height;

    private static float Interpolate(int[] table, float temperature)
    {
        var temp = Math.Clamp(temperature, 20, 60);
        var segment = Math.Min(3, (int)((temp - 20) / 10));
        var ratio = (temp - (20 + segment * 10)) / 10f;
        return table[segment] + (table[segment + 1] - table[segment]) * ratio;
    }

    private Rectangle GetPlotRectangle() => new(64, 28,
        Math.Max(1, Width - 64 - 28), Math.Max(1, Height - 28 - 52));

    private void OnGraphMouseDown(object? sender, MouseEventArgs e)
    {
        if (!Editable || (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right)) return;
        var plot = GetPlotRectangle();
        if (!plot.Contains(e.Location)) return;

        dragFan = e.Button == MouseButtons.Left ? 1 : 2;
        dragPoint = Enumerable.Range(0, temperatures.Length)
            .OrderBy(i => Math.Abs(MapX(temperatures[i], plot) - e.X)).First();
        if (Math.Abs(MapX(temperatures[dragPoint], plot) - e.X) > 24) {
            dragFan = 0; dragPoint = -1; return;
        }
        Cursor = Cursors.SizeNS;
        ApplyMouseDuty(e.Y, plot);
    }

    private void OnGraphMouseMove(object? sender, MouseEventArgs e)
    {
        if (!Editable || dragFan == 0 || dragPoint < 0) return;
        ApplyMouseDuty(e.Y, GetPlotRectangle());
    }

    private void ApplyMouseDuty(int mouseY, Rectangle plot)
    {
        var duty = (int)Math.Round((plot.Bottom - mouseY) * 100.0 / plot.Height);
        duty = Math.Clamp(duty, 0, 100);
        var table = dragFan == 1 ? duty1 : duty2;
        if (table[dragPoint] == duty) return;
        table[dragPoint] = duty;
        DutyPointChanged?.Invoke(this, new DutyPointChangedEventArgs(dragFan, dragPoint, duty));
        Invalidate();
    }

}

internal sealed class DutyPointChangedEventArgs(int fan, int point, int duty) : EventArgs
{
    public int Fan { get; } = fan;
    public int Point { get; } = point;
    public int Duty { get; } = duty;
}
