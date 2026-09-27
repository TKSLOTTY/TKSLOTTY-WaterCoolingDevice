using System.Diagnostics;

namespace WaterCoolingDevice;

internal sealed class GameOverlayForm : Form
{
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private static readonly IntPtr HwndTopMost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private readonly System.Windows.Forms.Timer visibilityTimer = new() { Interval = 250 };
    private Font overlayFont = new("Segoe UI Semibold", 13f, FontStyle.Bold);
    private string overlayText = "FPS -- | CPU -- °C | GPU -- °C | HOTSPOT -- °C | WATER -- °C";
    private bool enabledForGames;
    private GameOverlayDisplayOptions options = new(
        false, Color.FromArgb(255, 145, 0), Color.Black,
        true, true, true, true, true, false, false, 13f, 2);
    private float? fps;
    private float? cpu;
    private float? gpu;
    private float? hotSpot;
    private float? water;
    private float? fan1Duty;
    private float? fan2Duty;

    public GameOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        visibilityTimer.Tick += (_, _) => RefreshVisibility();
        visibilityTimer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            // WinForms adds WS_EX_LAYERED itself when TransparencyKey is active.
            // Forcing it here also made a filled-background overlay fully
            // invisible because no layered alpha/color-key attributes existed.
            parameters.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    public void SetEnabledForGames(bool enabled)
    {
        enabledForGames = enabled;
        RefreshVisibility();
    }

    public void ApplyOptions(GameOverlayDisplayOptions value)
    {
        options = value;
        var requestedSize = Math.Clamp(value.FontSize, 8f, 32f);
        if (Math.Abs(overlayFont.Size - requestedSize) > 0.01f)
        {
            var oldFont = overlayFont;
            overlayFont = new Font("Segoe UI Semibold", requestedSize, FontStyle.Bold);
            oldFont.Dispose();
        }
        BackColor = value.FilledBackground ? value.BackgroundColor : Color.Fuchsia;
        TransparencyKey = value.FilledBackground ? Color.Empty : Color.Fuchsia;
        RebuildText();
    }

    public void UpdateTelemetry(float? fpsValue, float? cpuValue, float? gpuValue,
        float? hotSpotValue, float? waterValue, float? fan1DutyValue, float? fan2DutyValue)
    {
        fps = fpsValue;
        cpu = cpuValue;
        gpu = gpuValue;
        hotSpot = hotSpotValue;
        water = waterValue;
        fan1Duty = fan1DutyValue;
        fan2Duty = fan2DutyValue;
        RebuildText();
    }

    private void RebuildText()
    {
        var values = new List<string>();
        if (options.ShowFps) values.Add($"FPS {Format(fps, "0")}");
        if (options.ShowCpu) values.Add($"CPU {Format(cpu, "0")} °C");
        if (options.ShowGpu) values.Add($"GPU {Format(gpu, "0")} °C");
        if (options.ShowHotSpot) values.Add($"HOTSPOT {Format(hotSpot, "0")} °C");
        if (options.ShowWater) values.Add($"WATER {Format(water, "0.0")} °C");
        if (options.ShowFan1Duty) values.Add($"FAN1 {Format(fan1Duty, "0")}%");
        if (options.ShowFan2Duty) values.Add($"FAN2 {Format(fan2Duty, "0")}%");
        overlayText = string.Join(" | ", values);
        using var graphics = CreateGraphics();
        var measured = TextRenderer.MeasureText(graphics, overlayText, overlayFont,
            Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        Size = new Size(measured.Width + 8, measured.Height + 6);
        Invalidate();
        RefreshVisibility();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(e.Graphics, overlayText, overlayFont, new Point(4, 3),
            Color.Black, flags);
        TextRenderer.DrawText(e.Graphics, overlayText, overlayFont, new Point(2, 1),
            options.TextColor, flags);
    }

    private void RefreshVisibility()
    {
        if (IsDisposed || Disposing) return;
        var foreground = NativeMethods.GetForegroundWindow();
        if (!enabledForGames || !IsFullscreenApplication(foreground, out var screen))
        {
            if (Visible) Hide();
            return;
        }

        Location = new Point(
            screen.Bounds.Right - Width - 4,
            screen.Bounds.Top + Math.Clamp(options.TopMargin, 0, 50));
        if (!Visible) Show();
        // Some borderless games promote their swap-chain window after our
        // overlay was shown. Reassert the topmost z-order without activating
        // the overlay, so keyboard and mouse focus stay in the game.
        NativeMethods.SetWindowPos(Handle, HwndTopMost, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    private static bool IsFullscreenApplication(IntPtr window, out Screen screen)
    {
        screen = Screen.PrimaryScreen!;
        if (window == IntPtr.Zero || !NativeMethods.GetWindowRect(window, out var rectangle))
            return false;
        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (processId == Environment.ProcessId || processId == 0) return false;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                process.ProcessName.Equals("dwm", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        catch { return false; }

        screen = Screen.FromHandle(window);
        var bounds = screen.Bounds;
        const int tolerance = 4;
        return rectangle.Left <= bounds.Left + tolerance &&
            rectangle.Top <= bounds.Top + tolerance &&
            rectangle.Right >= bounds.Right - tolerance &&
            rectangle.Bottom >= bounds.Bottom - tolerance;
    }

    private static string Format(float? value, string format) =>
        value is { } number && float.IsFinite(number)
            ? number.ToString(format) : "--";

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            visibilityTimer.Dispose();
            overlayFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
