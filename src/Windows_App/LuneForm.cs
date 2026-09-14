using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WaterCoolingDevice;

internal readonly record struct LuneTelemetrySnapshot(
    float? WaterTemperature,
    float? CpuTemperature,
    float? GpuTemperature,
    float WarningTemperature,
    bool IsConnected,
    bool SensorFault,
    bool WarningActive,
    Color LowTemperatureColor,
    Color WarningColor,
    float? CpuUsage = null,
    float? MemoryUsage = null,
    float? Fan1Duty = null,
    float? Fan1Rpm = null,
    float? Fan2Duty = null,
    float? Fan2Rpm = null,
    string? CpuName = null,
    string? GpuName = null, float? GpuUsage = null);

internal sealed partial class LuneForm : Form
{
    private const int WindowWidth = 360;
    private const int WindowHeight = 560;
    private static readonly RectangleF BackButtonBounds = new(213, 507, 116, 24);

    private readonly System.Windows.Forms.Timer animationTimer = new() { Interval = 33 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Bitmap mascotNormal;
    private readonly Bitmap mascotWarm;
    private readonly Bitmap mascotCritical;
    private readonly Bitmap mascotNormalBlink;
    private readonly Bitmap mascotWarmBlink;
    private readonly Bitmap mascotCriticalBlink;
    private readonly List<Bubble> bubbles = [];
    private readonly Random random = new(2205);
    private LuneTelemetrySnapshot telemetry = new(
        null, null, null, 45f, false, false, false,
        Color.FromArgb(40, 170, 255), Color.FromArgb(255, 70, 60));
    private float? smoothedCpuTemperature;
    private float? smoothedGpuTemperature;
    private DateTime lastHostSmoothingUtc = DateTime.MinValue;
    private LuneMood hostMood = LuneMood.Good;
    private double nextBlinkAtSeconds = 2.6;
    private double blinkEndsAtSeconds;
    private int pendingBlinkCount;
    private bool blinkClosed;
    private bool repeatBlinkStarting;
    private bool isEnglish;
    private bool applicationClosing;

    public LuneForm()
    {
        mascotNormal = LoadMascot("LuneNormal.png");
        mascotWarm = LoadMascot("LuneWarm.png");
        mascotCritical = LoadMascot("LuneCritical.png", removeGreenBackground: true);
        mascotNormalBlink = LoadMascot("LuneNormalBlink.png");
        mascotWarmBlink = LoadMascot("LuneWarmBlink.png");
        mascotCriticalBlink = LoadMascot("LuneCriticalBlink.png", removeGreenBackground: true);

        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(WindowWidth, WindowHeight);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        KeyPreview = true;
        Text = "WCD-01 LUNE";
        BackColor = Color.FromArgb(5, 12, 28);
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint, true);

        for (var i = 0; i < 11; i++)
            bubbles.Add(NewBubble(random.NextDouble() * ClientSize.Height));

        animationTimer.Tick += (_, _) => {
            UpdateScene();
            Invalidate();
        };
        MouseDown += HandleMouseDown;
        KeyDown += (_, e) => {
            if (e.KeyCode != Keys.Escape) return;
            RequestReturn();
            e.Handled = true;
        };
        Resize += (_, _) => UpdateWindowRegion();
        VisibleChanged += (_, _) => animationTimer.Enabled = Visible;
        BuildContextMenu();
    }

    public event EventHandler? ReturnRequested;
    public event EventHandler? ExitRequested;

    public void UpdateTelemetry(LuneTelemetrySnapshot snapshot)
    {
        telemetry = snapshot;
        UpdateSmoothedHostTemperatures(snapshot);
        UpdateHostMood();
        Invalidate();
    }

    public void SetLanguage(bool english)
    {
        isEnglish = english;
        BuildContextMenu();
        Invalidate();
    }

    public void CloseForApplication()
    {
        applicationClosing = true;
        Close();
    }

    public void SavePreview(
        string outputPath, LuneTelemetrySnapshot snapshot, bool showBlink = false)
    {
        using var bitmap = new Bitmap(WindowWidth, WindowHeight);
        using var graphics = Graphics.FromImage(bitmap);
        var previous = telemetry;
        var previousCpu = smoothedCpuTemperature;
        var previousGpu = smoothedGpuTemperature;
        var previousHostMood = hostMood;
        var previousSmoothingTime = lastHostSmoothingUtc;
        var previousBlinkClosed = blinkClosed;
        telemetry = snapshot;
        UpdateSmoothedHostTemperatures(snapshot, immediate: true);
        UpdateHostMood();
        blinkClosed = showBlink;
        try { DrawScene(graphics, new Rectangle(0, 0, WindowWidth, WindowHeight)); }
        finally
        {
            telemetry = previous;
            smoothedCpuTemperature = previousCpu;
            smoothedGpuTemperature = previousGpu;
            hostMood = previousHostMood;
            lastHostSmoothingUtc = previousSmoothingTime;
            blinkClosed = previousBlinkClosed;
        }
        bitmap.Save(outputPath, ImageFormat.Png);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - 24, area.Bottom - Height - 24);
        UpdateWindowRegion();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!applicationClosing && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            RequestReturn();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawScene(e.Graphics, ClientRectangle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animationTimer.Dispose();
            mascotNormal.Dispose();
            mascotWarm.Dispose();
            mascotCritical.Dispose();
            mascotNormalBlink.Dispose();
            mascotWarmBlink.Dispose();
            mascotCriticalBlink.Dispose();
        }
        base.Dispose(disposing);
    }

    private void UpdateScene()
    {
        var now = clock.Elapsed.TotalSeconds;
        UpdateBlink(now, GetState(telemetry).Mood);
        var activity = GetActivityLevel();
        var speed = 0.45 + activity * 1.35;
        foreach (var bubble in bubbles)
        {
            bubble.Y -= bubble.Speed * speed;
            bubble.X += Math.Sin(now * 1.2 + bubble.Phase) * 0.12;
            if (bubble.Y >= -bubble.Size) continue;
            var replacement = NewBubble(ClientSize.Height + bubble.Size);
            bubble.X = replacement.X;
            bubble.Y = replacement.Y;
            bubble.Size = replacement.Size;
            bubble.Speed = replacement.Speed;
            bubble.Phase = replacement.Phase;
        }
    }

    private void UpdateBlink(double now, LuneMood mood)
    {
        if (blinkClosed)
        {
            if (now < blinkEndsAtSeconds) return;

            blinkClosed = false;
            if (pendingBlinkCount > 0)
            {
                pendingBlinkCount--;
                repeatBlinkStarting = true;
                nextBlinkAtSeconds = now + 0.12;
            }
            else
            {
                repeatBlinkStarting = false;
                ScheduleNextBlink(now, mood);
            }
            return;
        }

        if (now < nextBlinkAtSeconds) return;

        blinkClosed = true;
        blinkEndsAtSeconds = now + (mood == LuneMood.Critical ? 0.13 : 0.10);
        if (!repeatBlinkStarting && pendingBlinkCount == 0 && random.NextDouble() < 0.16)
            pendingBlinkCount = 1;
    }

    private void ScheduleNextBlink(double now, LuneMood mood)
    {
        var (minimum, maximum) = mood switch {
            LuneMood.Critical => (1.8, 3.4),
            LuneMood.Warm => (2.4, 4.2),
            _ => (3.0, 5.2)
        };
        nextBlinkAtSeconds = now + minimum + random.NextDouble() * (maximum - minimum);
    }

    private void DrawScene(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var state = GetState(telemetry);
        var accent = GetAccent(telemetry, state);
        var backgroundTop = Blend(Color.FromArgb(5, 13, 31), accent, 0.12f);
        var backgroundBottom = Blend(Color.FromArgb(8, 32, 52), accent, 0.28f);
        using (var background = new LinearGradientBrush(
                   bounds, backgroundTop, backgroundBottom, 90f))
            graphics.FillRectangle(background, bounds);

        DrawGrid(graphics, bounds, accent);
        DrawBubbles(graphics, accent);
        DrawHeader(graphics, state, accent);

        var heat = GetActivityLevel();
        var seconds = clock.Elapsed.TotalSeconds;
        var bob = (float)Math.Sin(seconds * (1.8 + heat)) * (4f + heat * 3f);
        var shake = state.Mood == LuneMood.Critical
            ? (float)Math.Sin(seconds * 18.0) * 2.2f : 0f;

        DrawAura(graphics, accent, seconds, state.Mood);
        DrawMascot(graphics, bob, shake, seconds, state.Mood);
        DrawHeatMotion(graphics, seconds, state.Mood, accent);
        DrawSpeechBubble(graphics, state, bob);
        DrawTemperaturePanel(graphics, state, accent);
        DrawCloseButton(graphics);

        using var borderPen = new Pen(Color.FromArgb(135, accent), 2f);
        graphics.DrawRoundedRectangle(
            borderPen, new RectangleF(1, 1, bounds.Width - 3, bounds.Height - 3), 24f);
    }

    private static void DrawGrid(Graphics graphics, Rectangle bounds, Color accent)
    {
        using var pen = new Pen(Color.FromArgb(18, accent), 1f);
        for (var x = 18; x < bounds.Width; x += 28)
            graphics.DrawLine(pen, x, 70, x, bounds.Height - 112);
        for (var y = 84; y < bounds.Height - 112; y += 28)
            graphics.DrawLine(pen, 0, y, bounds.Width, y);
    }

    private void DrawBubbles(Graphics graphics, Color accent)
    {
        foreach (var bubble in bubbles)
        {
            using var fill = new SolidBrush(Color.FromArgb(22, accent));
            using var outline = new Pen(Color.FromArgb(90, accent), 1.2f);
            graphics.FillEllipse(fill, (float)bubble.X, (float)bubble.Y, bubble.Size, bubble.Size);
            graphics.DrawEllipse(outline, (float)bubble.X, (float)bubble.Y, bubble.Size, bubble.Size);
        }
    }

    private void DrawHeader(Graphics graphics, TemperatureState state, Color accent)
    {
        using var nameFont = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
        using var tinyFont = new Font("Segoe UI", 7.5f);
        using var white = new SolidBrush(Color.FromArgb(242, 245, 252));
        using var muted = new SolidBrush(Color.FromArgb(155, 188, 210));
        graphics.DrawString("WCD-01  LUNE", nameFont, white, 18, 16);
        graphics.DrawString("COOLING NAVIGATOR", tinyFont, muted, 20, 39);

        var badge = new RectangleF(236, 19, 88, 25);
        using var badgeFill = new SolidBrush(Color.FromArgb(38, accent));
        graphics.FillRoundedRectangle(badgeFill, badge, 12f);
        using var dot = new SolidBrush(accent);
        graphics.FillEllipse(dot, 247, 28, 7, 7);
        using var badgeFont = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
        graphics.DrawString(state.ShortLabel, badgeFont, white, 260, 25);
    }

    private static void DrawAura(
        Graphics graphics, Color accent, double seconds, LuneMood mood)
    {
        var pulseRate = mood == LuneMood.Critical ? 5.0 : 2.0;
        var pulse = 0.5f + 0.5f * (float)Math.Sin(seconds * pulseRate);
        var aura = new RectangleF(57 - pulse * 3, 91 - pulse * 3, 247 + pulse * 6, 337 + pulse * 6);
        using var auraPath = EllipsePath(aura);
        using var auraFill = new PathGradientBrush(auraPath) {
            CenterColor = Color.FromArgb(40 + (int)(pulse * 20), accent),
            SurroundColors = [Color.FromArgb(0, accent)]
        };
        graphics.FillEllipse(auraFill, aura);
        using var ringPen = new Pen(Color.FromArgb(45 + (int)(pulse * 40), accent), 2f);
        graphics.DrawEllipse(ringPen, 76, 116, 208, 285);
    }

    private void DrawMascot(
        Graphics graphics, float bob, float shake, double seconds, LuneMood mood)
    {
        var sprite = (mood, blinkClosed) switch {
            (LuneMood.Critical, true) => mascotCriticalBlink,
            (LuneMood.Warm, true) => mascotWarmBlink,
            (_, true) => mascotNormalBlink,
            (LuneMood.Critical, false) => mascotCritical,
            (LuneMood.Warm, false) => mascotWarm,
            _ => mascotNormal
        };
        var motionRate = mood == LuneMood.Critical ? 6.5f : mood == LuneMood.Warm ? 3.2f : 1.8f;
        var breath = (float)Math.Sin(seconds * motionRate);
        var scaleX = 1f + breath * (mood == LuneMood.Critical ? 0.018f : 0.008f);
        var scaleY = 1f - breath * (mood == LuneMood.Critical ? 0.012f : 0.006f);
        var width = 210f * scaleX;
        var height = 315f * scaleY;
        var destination = new RectangleF(
            180f - width / 2f + shake, 251.5f - height / 2f + bob, width, height);
        var saved = graphics.Save();
        var rotation = (float)Math.Sin(seconds * 1.2) *
                       (mood is LuneMood.Warm or LuneMood.Critical ? 1.25f : 0.8f);
        if (mood == LuneMood.Critical)
            rotation += (float)Math.Sin(seconds * 12.0) * 0.9f;
        graphics.TranslateTransform(destination.Left + destination.Width / 2f,
            destination.Top + destination.Height / 2f);
        graphics.RotateTransform(rotation);
        graphics.TranslateTransform(-(destination.Left + destination.Width / 2f),
            -(destination.Top + destination.Height / 2f));
        graphics.DrawImage(sprite, destination);
        graphics.Restore(saved);
    }

    private static void DrawHeatMotion(
        Graphics graphics, double seconds, LuneMood mood, Color accent)
    {
        if (mood is LuneMood.Cool or LuneMood.Good or LuneMood.Waiting) return;
        var dropCount = mood == LuneMood.Critical ? 4 : 2;
        for (var i = 0; i < dropCount; i++)
        {
            var phase = (seconds * (mood == LuneMood.Critical ? 0.9 : 0.55) + i * 0.31) % 1.0;
            var x = i % 2 == 0 ? 62f + i * 7f : 292f - i * 5f;
            var y = 158f + (float)phase * 150f;
            var size = mood == LuneMood.Critical ? 7f : 5f;
            using var drop = new SolidBrush(Color.FromArgb(
                (int)(190 * (1.0 - phase)), 92, 220, 255));
            graphics.FillEllipse(drop, x, y, size, size * 1.55f);
        }
        if (mood != LuneMood.Critical) return;
        var pulse = 0.5f + 0.5f * (float)Math.Sin(seconds * 5.5);
        using var warningPen = new Pen(Color.FromArgb(60 + (int)(pulse * 80), accent), 2f);
        graphics.DrawArc(warningPen, 48, 105, 264, 314, 205, 130);
        graphics.DrawArc(warningPen, 55, 112, 250, 300, 25, 130);
    }

    private void DrawSpeechBubble(Graphics graphics, TemperatureState state, float bob)
    {
        var bubble = new RectangleF(224, 84 + bob * 0.35f, 108, 48);
        using var shadow = new SolidBrush(Color.FromArgb(55, 0, 0, 0));
        graphics.FillRoundedRectangle(
            shadow, new RectangleF(bubble.X + 3, bubble.Y + 4, bubble.Width, bubble.Height), 16f);
        using var fill = new SolidBrush(Color.FromArgb(235, 248, 252, 255));
        graphics.FillRoundedRectangle(fill, bubble, 16f);
        PointF[] tail = [
            new(bubble.X + 18, bubble.Bottom - 3),
            new(bubble.X + 9, bubble.Bottom + 13),
            new(bubble.X + 37, bubble.Bottom - 1)
        ];
        graphics.FillPolygon(fill, tail);
        using var font = new Font(isEnglish ? "Segoe UI" : "Yu Gothic UI", 9.5f, FontStyle.Bold);
        using var text = new SolidBrush(Color.FromArgb(18, 42, 64));
        using var format = new StringFormat {
            Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(isEnglish ? state.MessageEnglish : state.MessageJapanese,
            font, text, bubble, format);
    }

    private void DrawTemperaturePanel(Graphics graphics, TemperatureState state, Color accent)
    {
        var panel = new RectangleF(17, 433, 326, 105);
        using var panelFill = new SolidBrush(Color.FromArgb(218, 7, 19, 39));
        graphics.FillRoundedRectangle(panelFill, panel, 20f);
        using var panelBorder = new Pen(Color.FromArgb(100, accent), 1.4f);
        graphics.DrawRoundedRectangle(panelBorder, panel, 20f);

        using var labelFont = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
        using var tempFont = new Font("Segoe UI", 27f, FontStyle.Bold);
        using var hostLabelFont = new Font("Segoe UI Semibold", 7.2f, FontStyle.Bold);
        using var hostValueFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var statusFont = new Font(isEnglish ? "Segoe UI" : "Yu Gothic UI", 8.5f, FontStyle.Bold);
        using var hintFont = new Font("Segoe UI", 7.2f);
        using var backFont = new Font(isEnglish ? "Segoe UI" : "Yu Gothic UI", 7.5f, FontStyle.Bold);
        using var white = new SolidBrush(Color.FromArgb(247, 250, 255));
        using var muted = new SolidBrush(Color.FromArgb(150, 184, 205));
        using var accentBrush = new SolidBrush(accent);

        graphics.DrawString("WATER TEMP", labelFont, muted, 32, 447);
        var temperatureText = telemetry.IsConnected && !telemetry.SensorFault &&
                              telemetry.WaterTemperature.HasValue
            ? $"{telemetry.WaterTemperature.Value:0.0}℃" : "--.-℃";
        graphics.DrawString(temperatureText, tempFont, white, 29, 462);

        var hostTemperatures = new List<(string Label, float Value)>(2);
        if (telemetry.CpuTemperature is { } cpu)
            hostTemperatures.Add(("CPU", cpu));
        if (telemetry.GpuTemperature is { } gpu)
            hostTemperatures.Add(("GPU", gpu));
        var hostY = hostTemperatures.Count == 1 ? 454f : 445f;
        foreach (var hostTemperature in hostTemperatures)
        {
            graphics.DrawString(hostTemperature.Label, hostLabelFont, muted, 211, hostY + 2);
            graphics.DrawString($"{hostTemperature.Value:0}℃", hostValueFont, white, 247, hostY);
            hostY += 17f;
        }

        graphics.DrawString(isEnglish ? state.LabelEnglish : state.LabelJapanese,
            statusFont, accentBrush, 211, 480);
        graphics.DrawString("USB HID  •  LIVE", hintFont, muted, 31, 515);

        using var backFill = new SolidBrush(Color.FromArgb(45, accent));
        using var backBorder = new Pen(Color.FromArgb(120, accent), 1f);
        graphics.FillRoundedRectangle(backFill, BackButtonBounds, 10f);
        graphics.DrawRoundedRectangle(backBorder, BackButtonBounds, 10f);
        using var centered = new StringFormat {
            Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(isEnglish ? "Standard view" : "通常画面へ",
            backFont, white, BackButtonBounds, centered);

        const float meterX = 207;
        const float meterY = 499;
        const float meterWidth = 108;
        using var track = new SolidBrush(Color.FromArgb(45, 190, 218, 230));
        graphics.FillRoundedRectangle(track, new RectangleF(meterX, meterY, meterWidth, 6), 3f);
        var warning = Math.Max(21f, telemetry.WarningTemperature);
        var ratio = telemetry.WaterTemperature.HasValue
            ? Math.Clamp((telemetry.WaterTemperature.Value - 18f) / (warning - 18f), 0f, 1f)
            : 0f;
        using var meter = new SolidBrush(accent);
        graphics.FillRoundedRectangle(
            meter, new RectangleF(meterX, meterY, meterWidth * ratio, 6), 3f);
    }

    private static void DrawCloseButton(Graphics graphics)
    {
        using var fill = new SolidBrush(Color.FromArgb(35, 220, 235, 245));
        graphics.FillEllipse(fill, 325, 16, 21, 21);
        using var pen = new Pen(Color.FromArgb(185, 225, 238, 247), 1.5f);
        graphics.DrawLine(pen, 332, 23, 339, 30);
        graphics.DrawLine(pen, 339, 23, 332, 30);
    }

    private void HandleMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left &&
            ((e.X >= 319 && e.Y <= 44) || BackButtonBounds.Contains(e.Location)))
        {
            RequestReturn();
            return;
        }
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0x00A1, 0x0002, 0);
    }

    private void RequestReturn()
    {
        Hide();
        ReturnRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuildContextMenu()
    {
        ContextMenuStrip?.Dispose();
        var menu = new ContextMenuStrip();
        var backItem = new ToolStripMenuItem(
            isEnglish ? "Return to standard view" : "通常画面へ戻る");
        backItem.Click += (_, _) => RequestReturn();
        var topMostItem = new ToolStripMenuItem(
            isEnglish ? "Always on top" : "常に手前に表示") {
            Checked = TopMost, CheckOnClick = true
        };
        topMostItem.CheckedChanged += (_, _) => TopMost = topMostItem.Checked;
        var exitItem = new ToolStripMenuItem(isEnglish ? "Exit application" : "アプリを終了");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(backItem);
        menu.Items.Add(topMostItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        ContextMenuStrip = menu;
    }

    private void UpdateWindowRegion()
    {
        using var path = RoundedRectanglePath(new RectangleF(0, 0, Width, Height), 25f);
        Region?.Dispose();
        Region = new Region(path);
    }

    private Bubble NewBubble(double y) => new() {
        X = random.NextDouble() * Math.Max(1, ClientSize.Width - 20),
        Y = y,
        Size = 4f + (float)random.NextDouble() * 12f,
        Speed = 0.45 + random.NextDouble(),
        Phase = random.NextDouble() * Math.PI * 2.0
    };

    private void UpdateSmoothedHostTemperatures(
        LuneTelemetrySnapshot snapshot, bool immediate = false)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = lastHostSmoothingUtc == DateTime.MinValue
            ? 0.0 : Math.Clamp((now - lastHostSmoothingUtc).TotalSeconds, 0.0, 3.0);
        lastHostSmoothingUtc = now;
        var alpha = immediate || elapsedSeconds <= 0.0
            ? 1f : (float)(1.0 - Math.Exp(-elapsedSeconds / 6.0));
        smoothedCpuTemperature = SmoothTemperature(
            smoothedCpuTemperature, snapshot.CpuTemperature, alpha);
        smoothedGpuTemperature = SmoothTemperature(
            smoothedGpuTemperature, snapshot.GpuTemperature, alpha);
    }

    private static float? SmoothTemperature(float? previous, float? current, float alpha)
    {
        if (!current.HasValue || current.Value is < 1f or > 130f) return null;
        if (!previous.HasValue) return current.Value;
        return previous.Value + (current.Value - previous.Value) * alpha;
    }

    private bool HasHostThermalData =>
        smoothedCpuTemperature.HasValue || smoothedGpuTemperature.HasValue;

    private bool HostAtOrAbove(float cpuThreshold, float gpuThreshold) =>
        (smoothedCpuTemperature is { } cpu && cpu >= cpuThreshold) ||
        (smoothedGpuTemperature is { } gpu && gpu >= gpuThreshold);

    private bool HostBelow(float cpuThreshold, float gpuThreshold) =>
        (!smoothedCpuTemperature.HasValue || smoothedCpuTemperature.Value < cpuThreshold) &&
        (!smoothedGpuTemperature.HasValue || smoothedGpuTemperature.Value < gpuThreshold);

    private void UpdateHostMood()
    {
        if (!HasHostThermalData)
        {
            hostMood = LuneMood.Good;
            return;
        }

        if (hostMood == LuneMood.Critical)
        {
            if (HostAtOrAbove(78f, 76f)) return;
            hostMood = HostAtOrAbove(60f, 57f) ? LuneMood.Warm : LuneMood.Good;
            return;
        }

        if (HostAtOrAbove(82f, 80f))
        {
            hostMood = LuneMood.Critical;
            return;
        }

        if (hostMood == LuneMood.Warm)
        {
            if (HostAtOrAbove(60f, 57f)) return;
            hostMood = HostBelow(45f, 43f) ? LuneMood.Cool : LuneMood.Good;
            return;
        }

        if (HostAtOrAbove(65f, 62f))
        {
            hostMood = LuneMood.Warm;
            return;
        }

        if (hostMood == LuneMood.Cool && !HostAtOrAbove(50f, 48f)) return;
        hostMood = HostBelow(45f, 43f) ? LuneMood.Cool : LuneMood.Good;
    }

    private float GetActivityLevel()
    {
        if (!telemetry.IsConnected) return 0.1f;
        if (telemetry.SensorFault || telemetry.WarningActive) return 1f;
        if (HasHostThermalData)
        {
            var cpuLevel = smoothedCpuTemperature.HasValue
                ? Math.Clamp((smoothedCpuTemperature.Value - 40f) / 45f, 0f, 1f) : 0f;
            var gpuLevel = smoothedGpuTemperature.HasValue
                ? Math.Clamp((smoothedGpuTemperature.Value - 38f) / 44f, 0f, 1f) : 0f;
            return Math.Max(cpuLevel, gpuLevel);
        }

        if (!telemetry.WaterTemperature.HasValue) return 0f;
        var upper = Math.Max(36f, telemetry.WarningTemperature);
        return Math.Clamp((telemetry.WaterTemperature.Value - 28f) / (upper - 28f), 0f, 1f);
    }

    private TemperatureState GetState(LuneTelemetrySnapshot snapshot)
    {
        if (!snapshot.IsConnected)
            return new("WAIT", "本体を待ってるよ", "Waiting for device", "接続待ち", "Waiting", LuneMood.Waiting);
        if (snapshot.SensorFault)
            return new("ERROR", "センサーを確認して", "Check the sensor", "センサー故障", "Sensor fault", LuneMood.Critical);
        if (!snapshot.WaterTemperature.HasValue)
            return new("WAIT", "水温を確認中…", "Reading coolant…", "取得中", "Reading", LuneMood.Waiting);
        var temperature = snapshot.WaterTemperature.Value;
        if (snapshot.WarningActive || temperature >= Math.Min(snapshot.WarningTemperature, 40f))
            return new("HOT!", "あつい！", "Too hot!", "冷却を確認！", "Check cooling!", LuneMood.Critical);

        if (HasHostThermalData)
        {
            return hostMood switch
            {
                LuneMood.Critical => new("PC HOT", "PCが熱いよ！", "PC is hot!",
                    "CPU/GPU高温", "CPU/GPU hot", LuneMood.Critical),
                LuneMood.Warm => new("BUSY", "がんばってるね", "Working hard",
                    "PC温度上昇", "PC warming", LuneMood.Warm),
                LuneMood.Cool => new("COOL", "のんびり運転中", "Taking it easy",
                    "PC低温", "PC cool", LuneMood.Cool),
                _ => new("GOOD", "いい感じ♪", "Looking good",
                    "安定しています", "Stable", LuneMood.Good)
            };
        }

        if (temperature >= 36f)
            return new("WARM", "少し熱いかも", "Getting warm", "少し高めです", "A little warm", LuneMood.Warm);
        if (temperature < 28f)
            return new("COOL", "よく冷えてるよ", "Nice and cool", "ひんやり", "Cool", LuneMood.Cool);
        return new("GOOD", "いい感じ♪", "Looking good", "安定しています", "Stable", LuneMood.Good);
    }

    private static Color GetAccent(LuneTelemetrySnapshot snapshot, TemperatureState state)
    {
        if (!snapshot.IsConnected) return Color.FromArgb(120, 165, 195);
        if (snapshot.SensorFault) return Color.FromArgb(255, 82, 112);
        if (!snapshot.WaterTemperature.HasValue) return Color.FromArgb(83, 211, 255);
        var upper = Math.Max(20.1f, snapshot.WarningTemperature);
        var ratio = Math.Clamp((snapshot.WaterTemperature.Value - 20f) / (upper - 20f), 0f, 1f);
        var accent = Blend(snapshot.LowTemperatureColor, snapshot.WarningColor, ratio);
        var coolantCritical = snapshot.WarningActive ||
            snapshot.WaterTemperature.Value >= Math.Min(snapshot.WarningTemperature, 40f);
        return coolantCritical
            ? Blend(accent, snapshot.WarningColor, 0.72f) : accent;
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            255,
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }

    private static Bitmap LoadMascot(string fileName, bool removeGreenBackground = false)
    {
        var resourceName = $"WaterCoolingDevice.Assets.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded image not found: {resourceName}");
        using var source = new Bitmap(stream);
        if (!removeGreenBackground) return new Bitmap(source);

        var transparent = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(transparent);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        using var attributes = new ImageAttributes();
        attributes.SetColorKey(
            Color.FromArgb(0, 165, 0), Color.FromArgb(165, 255, 175), ColorAdjustType.Bitmap);
        graphics.DrawImage(source,
            new Rectangle(0, 0, transparent.Width, transparent.Height),
            0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        return transparent;
    }

    private static GraphicsPath EllipsePath(RectangleF rectangle)
    {
        var path = new GraphicsPath();
        path.AddEllipse(rectangle);
        return path;
    }

    private static GraphicsPath RoundedRectanglePath(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2f;
        var arc = new RectangleF(rectangle.Location, new SizeF(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = rectangle.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rectangle.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, int wParam, int lParam);

    private sealed class Bubble
    {
        public double X { get; set; }
        public double Y { get; set; }
        public float Size { get; set; }
        public double Speed { get; set; }
        public double Phase { get; set; }
    }

    private enum LuneMood { Waiting, Cool, Good, Warm, Critical }

    private readonly record struct TemperatureState(
        string ShortLabel,
        string MessageJapanese,
        string MessageEnglish,
        string LabelJapanese,
        string LabelEnglish,
        LuneMood Mood);
}

internal static class LuneGraphicsExtensions
{
    public static void FillRoundedRectangle(
        this Graphics graphics, Brush brush, RectangleF rectangle, float radius)
    {
        if (rectangle.Width <= 0f || rectangle.Height <= 0f) return;
        using var path = CreateRoundedRectanglePath(rectangle, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(
        this Graphics graphics, Pen pen, RectangleF rectangle, float radius)
    {
        if (rectangle.Width <= 0f || rectangle.Height <= 0f) return;
        using var path = CreateRoundedRectanglePath(rectangle, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(0.1f,
            Math.Min(radius * 2f, Math.Min(rectangle.Width, rectangle.Height)));
        var arc = new RectangleF(rectangle.X, rectangle.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rectangle.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rectangle.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
