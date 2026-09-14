using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WaterCoolingDevice;

internal sealed partial class LuneForm
{
    internal void SetDisplayLanguage(bool english) => isEnglish = english;
    public Bitmap RenderLunePanel(bool landscape, bool animate = false)
    {
        if (animate) UpdateScene();
        else blinkClosed = false;
        var image = new Bitmap(landscape ? 480 : 320, landscape ? 320 : 480, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        var state = GetState(telemetry);
        // In the quiet mode, small sensor fluctuations must not recolor every pixel.
        var accent = animate ? GetAccent(telemetry, state) : state.Mood switch {
            LuneMood.Waiting => Color.FromArgb(120, 165, 195),
            LuneMood.Critical => telemetry.WarningColor,
            LuneMood.Warm => Blend(telemetry.LowTemperatureColor, telemetry.WarningColor, .55f),
            LuneMood.Good => Blend(telemetry.LowTemperatureColor, telemetry.WarningColor, .15f),
            _ => telemetry.LowTemperatureColor
        };
        using (var background = new LinearGradientBrush(new Rectangle(0, 0, image.Width, image.Height),
            Blend(Color.FromArgb(5, 13, 31), accent, .12f),
            Blend(Color.FromArgb(8, 32, 52), accent, .28f), 90f))
            g.FillRectangle(background, 0, 0, image.Width, image.Height);
        using var white = new SolidBrush(Color.FromArgb(242, 245, 252));
        using var muted = new SolidBrush(Color.FromArgb(155, 188, 210));
        using var color = new SolidBrush(accent);
        using var titleFont = new Font("Segoe UI", 19, FontStyle.Bold, GraphicsUnit.Pixel);
        using var smallFont = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        using var tempFont = new Font("Segoe UI", landscape ? 43 : 38, FontStyle.Bold, GraphicsUnit.Pixel);
        using var bodyFont = new Font("Yu Gothic UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString("WCD-01  LUNE", titleFont, white, 18, 13);
        g.DrawString("COOLING NAVIGATOR", smallFont, muted, 19, 39);

        var saved = g.Save();
        if (landscape) { g.TranslateTransform(-12, -7); g.ScaleTransform(.79f, .79f); }
        else { g.TranslateTransform(16, -2); g.ScaleTransform(.8f, .8f); }
        var seconds = animate ? clock.Elapsed.TotalSeconds : 0;
        var bob = (float)Math.Sin(seconds * 2) * 3;
        DrawAura(g, accent, seconds, state.Mood);
        DrawMascot(g, bob, 0, seconds, state.Mood);
        DrawHeatMotion(g, seconds, state.Mood, accent);
        g.Restore(saved);

        var panel = landscape ? new Rectangle(254, 76, 210, 224) : new Rectangle(16, 346, 288, 119);
        using var fill = new SolidBrush(Color.FromArgb(220, 7, 19, 39));
        g.FillRoundedRectangle(fill, panel, 17);
        using var border = new Pen(Color.FromArgb(100, accent));
        g.DrawRoundedRectangle(border, panel, 17);
        var px = panel.X + 14;
        g.DrawString("WATER TEMP", smallFont, muted, px, panel.Y + 12);
        var water = telemetry.IsConnected && !telemetry.SensorFault && telemetry.WaterTemperature.HasValue
            ? $"{telemetry.WaterTemperature.Value:0.0} °C" : "--.- °C";
        g.DrawString(water, tempFont, white, px - 3, panel.Y + 30);
        var label = isEnglish ? state.LabelEnglish : state.LabelJapanese;
        if (landscape) {
            g.DrawString(label, bodyFont, color, px, panel.Y + 94);
            g.DrawString($"CPU   {Host(telemetry.CpuTemperature)}", bodyFont, white, px, panel.Y + 139);
            g.DrawString($"GPU   {Host(telemetry.GpuTemperature)}", bodyFont, white, px, panel.Y + 170);
            g.DrawString(telemetry.IsConnected ? "LIVE" : "DISCONNECTED", smallFont, muted, px, panel.Y + 203);
        } else {
            g.DrawString(label, smallFont, color, px, panel.Y + 91);
            g.DrawString($"CPU {Host(telemetry.CpuTemperature)}", smallFont, white, panel.X + 185, panel.Y + 40);
            g.DrawString($"GPU {Host(telemetry.GpuTemperature)}", smallFont, white, panel.X + 185, panel.Y + 64);
        }
        return image;
    }
    private static string Host(float? value) => value.HasValue ? $"{value.Value:0} °C" : "-- °C";
}
