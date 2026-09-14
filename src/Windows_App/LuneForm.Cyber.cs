using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WaterCoolingDevice;

internal sealed partial class LuneForm
{
    private static readonly Color CyberCyan = Color.FromArgb(0, 232, 255);
    private static readonly Color CyberBlue = Color.FromArgb(22, 94, 255);
    private static readonly Color CyberPink = Color.FromArgb(255, 28, 168);
    private static readonly Color CyberText = Color.FromArgb(220, 248, 255);
    private static readonly Color CyberMuted = Color.FromArgb(92, 151, 176);

    public Bitmap RenderLunePanelCyber(bool landscape, int animationFrame = 0)
    {
        var image = new Bitmap(landscape ? 480 : 320, landscape ? 320 : 480,
            PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        DrawCyberBackground(g, image.Size, landscape);
        if (landscape) DrawCyberLandscape(g, animationFrame);
        else DrawCyberPortrait(g, animationFrame);
        return image;
    }

    private static void DrawCyberBackground(Graphics g, Size size, bool landscape)
    {
        using var background = new LinearGradientBrush(
            new Rectangle(Point.Empty, size), Color.FromArgb(2, 8, 28),
            Color.FromArgb(4, 23, 51), landscape ? 20f : 70f);
        g.FillRectangle(background, 0, 0, size.Width, size.Height);

        using var grid = new Pen(Color.FromArgb(20, CyberCyan));
        for (var x = 0; x < size.Width; x += 24) g.DrawLine(grid, x, 0, x, size.Height);
        for (var y = 0; y < size.Height; y += 24) g.DrawLine(grid, 0, y, size.Width, y);

        using var diagonal = new Pen(Color.FromArgb(35, CyberBlue));
        for (var x = -size.Height; x < size.Width; x += 72)
            g.DrawLine(diagonal, x, size.Height, x + size.Height, 0);

        using var glow = new Pen(Color.FromArgb(80, CyberCyan), 1.5f);
        g.DrawLine(glow, 8, 7, size.Width - 34, 7);
        g.DrawLine(glow, 8, 7, 8, 28);
        g.DrawLine(glow, size.Width - 8, size.Height - 7, 34, size.Height - 7);
        g.DrawLine(glow, size.Width - 8, size.Height - 7, size.Width - 8, size.Height - 28);
    }

    private void DrawCyberLandscape(Graphics g, int animationFrame)
    {
        DrawCyberHeader(g, new Rectangle(15, 10, 450, 39), "THERMAL CONTROL // LUNE");

        DrawCyberPanel(g, new Rectangle(15, 61, 132, 96), "CPU CORE",
            TemperatureText(telemetry.CpuTemperature), "TEMPERATURE", CyberCyan);
        DrawCyberPanel(g, new Rectangle(15, 167, 132, 92), "CPU LOAD",
            PercentText(telemetry.CpuUsage), "SYSTEM UTILIZATION", CyberPink,
            telemetry.CpuUsage);

        DrawCyberPanel(g, new Rectangle(333, 61, 132, 96), "GPU CORE",
            TemperatureText(telemetry.GpuTemperature), "TEMPERATURE", CyberPink);
        DrawCyberPanel(g, new Rectangle(333, 167, 132, 92), "MEMORY",
            PercentText(telemetry.MemoryUsage), "PHYSICAL LOAD", CyberCyan,
            telemetry.MemoryUsage);

        DrawCyberCore(g, new PointF(240, 160), 81, animationFrame);
        DrawCyberFooter(g, new Rectangle(15, 263, 450, 50), centered: true);
    }

    private void DrawCyberPortrait(Graphics g, int animationFrame)
    {
        DrawCyberHeader(g, new Rectangle(13, 12, 294, 43), "THERMAL CONTROL");
        DrawCyberCore(g, new PointF(160, 159), 91, animationFrame);

        DrawCyberPanel(g, new Rectangle(13, 267, 141, 86), "CPU CORE",
            TemperatureText(telemetry.CpuTemperature), "TEMPERATURE", CyberCyan);
        DrawCyberPanel(g, new Rectangle(166, 267, 141, 86), "GPU CORE",
            TemperatureText(telemetry.GpuTemperature), "TEMPERATURE", CyberPink);
        DrawCyberPanel(g, new Rectangle(13, 365, 141, 74), "CPU LOAD",
            PercentText(telemetry.CpuUsage), "UTILIZATION", CyberPink,
            telemetry.CpuUsage);
        DrawCyberPanel(g, new Rectangle(166, 365, 141, 74), "MEMORY",
            PercentText(telemetry.MemoryUsage), "PHYSICAL", CyberCyan,
            telemetry.MemoryUsage);
        DrawCyberFooter(g, new Rectangle(13, 448, 294, 25));
    }

    private void DrawCyberHeader(Graphics g, Rectangle bounds, string title)
    {
        using var font = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel);
        using var small = new Font("Consolas", 9, FontStyle.Regular, GraphicsUnit.Pixel);
        using var text = new SolidBrush(CyberText);
        using var cyan = new SolidBrush(CyberCyan);
        g.DrawString(title, font, text, bounds.X + 12, bounds.Y + 4);
        g.DrawString("SYS:ONLINE", small, cyan, bounds.Right - 76, bounds.Y + 8);
        using var pen = new Pen(Color.FromArgb(150, CyberCyan));
        g.DrawLine(pen, bounds.X, bounds.Bottom - 3, bounds.Right, bounds.Bottom - 3);
        g.DrawLine(pen, bounds.X, bounds.Bottom - 3, bounds.X + 22, bounds.Bottom - 10);
    }

    private void DrawCyberCore(Graphics g, PointF center, float radius, int animationFrame)
    {
        var outer = RectangleF.FromLTRB(center.X - radius, center.Y - radius,
            center.X + radius, center.Y + radius);
        var middle = RectangleF.Inflate(outer, -9, -9);
        var inner = RectangleF.Inflate(outer, -25, -25);
        using var dim = new Pen(Color.FromArgb(70, CyberCyan), 7);
        using var blue = new Pen(Color.FromArgb(190, CyberBlue), 4);
        using var pink = new Pen(CyberPink, 5);
        using var cyan = new Pen(CyberCyan, 3);
        g.DrawArc(dim, outer, -160, 285);
        g.DrawArc(blue, outer, -145, 82);
        g.DrawArc(pink, outer, -47, 67);
        g.DrawArc(cyan, middle, 38, 194);
        using var innerFill = new SolidBrush(Color.FromArgb(218, 3, 16, 39));
        g.FillEllipse(innerFill, inner);
        using var innerPen = new Pen(Color.FromArgb(130, CyberCyan), 1.5f);
        g.DrawEllipse(innerPen, inner);

        for (var i = 0; i < 24; i++) {
            var angle = (float)(i * Math.PI * 2 / 24);
            var a = radius - (i % 3 == 0 ? 3 : 1);
            var b = radius + (i % 3 == 0 ? 5 : 3);
            using var tick = new Pen(i % 6 == 0 ? CyberPink : Color.FromArgb(115, CyberCyan),
                i % 6 == 0 ? 2 : 1);
            g.DrawLine(tick,
                center.X + MathF.Cos(angle) * a, center.Y + MathF.Sin(angle) * a,
                center.X + MathF.Cos(angle) * b, center.Y + MathF.Sin(angle) * b);
        }

        // Keep motion local: a short scanner advances one tick at a time, so the
        // external panel only needs the adjacent dirty tiles instead of the full ring.
        var scannerStep = ((animationFrame % 24) + 24) % 24;
        var scannerAngle = scannerStep * 15f - 90f;
        using (var scannerGlow = new Pen(Color.FromArgb(75, CyberCyan), 9f) {
            StartCap = LineCap.Round, EndCap = LineCap.Round
        })
        using (var scanner = new Pen(Color.FromArgb(245, 225, 252, 255), 2.6f) {
            StartCap = LineCap.Round, EndCap = LineCap.Round
        }) {
            g.DrawArc(scannerGlow, outer, scannerAngle, 13f);
            g.DrawArc(scanner, outer, scannerAngle, 13f);
        }
        var scannerRadians = (scannerAngle + 6.5f) * MathF.PI / 180f;
        var dotCenter = new PointF(center.X + MathF.Cos(scannerRadians) * radius,
            center.Y + MathF.Sin(scannerRadians) * radius);
        using (var dotGlow = new SolidBrush(Color.FromArgb(85, CyberCyan)))
            g.FillEllipse(dotGlow, dotCenter.X - 7, dotCenter.Y - 7, 14, 14);
        using (var dot = new SolidBrush(Color.White))
            g.FillEllipse(dot, dotCenter.X - 2.4f, dotCenter.Y - 2.4f, 4.8f, 4.8f);

        using var labelFont = new Font("Consolas", 10, FontStyle.Bold, GraphicsUnit.Pixel);
        using var valueFont = new Font("Segoe UI", radius > 85 ? 38 : 34,
            FontStyle.Bold, GraphicsUnit.Pixel);
        using var unitFont = new Font("Segoe UI", 12, FontStyle.Bold, GraphicsUnit.Pixel);
        using var text = new SolidBrush(CyberText);
        using var muted = new SolidBrush(CyberMuted);
        using var accent = new SolidBrush(telemetry.WarningActive ? CyberPink : CyberCyan);
        DrawCentered(g, "COOLANT", labelFont, muted, center.X, center.Y - 32);
        DrawCentered(g, WaterText(telemetry.WaterTemperature), valueFont, accent,
            center.X - 3, center.Y - 13);
        g.DrawString("°C", unitFont, text, center.X + 33, center.Y + 4);
        DrawCentered(g, telemetry.IsConnected ? "WCD-01 LINK" : "LINK LOST",
            labelFont, telemetry.IsConnected ? muted : accent, center.X, center.Y + 35);
    }

    private static void DrawCyberPanel(Graphics g, Rectangle bounds, string title,
        string value, string caption, Color accent, float? percent = null)
    {
        using var path = CutCornerPath(bounds, 9);
        using var fill = new SolidBrush(Color.FromArgb(218, 3, 15, 36));
        using var border = new Pen(Color.FromArgb(145, accent), 1.4f);
        g.FillPath(fill, path);
        g.DrawPath(border, path);
        using var titleFont = new Font("Consolas", 12, FontStyle.Bold, GraphicsUnit.Pixel);
        using var valueFont = new Font("Segoe UI", 25, FontStyle.Bold, GraphicsUnit.Pixel);
        using var smallFont = new Font("Consolas", 9, FontStyle.Regular, GraphicsUnit.Pixel);
        using var titleBrush = new SolidBrush(accent);
        using var valueBrush = new SolidBrush(CyberText);
        using var mutedBrush = new SolidBrush(CyberMuted);
        g.DrawString(title, titleFont, titleBrush, bounds.X + 9, bounds.Y + 6);
        g.DrawString(value, valueFont, valueBrush, bounds.X + 8, bounds.Y + 27);
        g.DrawString(caption, smallFont, mutedBrush, bounds.X + 9, bounds.Bottom - 19);
        if (percent.HasValue) {
            var bar = new Rectangle(bounds.X + 8, bounds.Bottom - 9, bounds.Width - 17, 3);
            using var track = new SolidBrush(Color.FromArgb(32, 70, 90));
            using var active = new SolidBrush(accent);
            g.FillRectangle(track, bar);
            g.FillRectangle(active, bar.X, bar.Y,
                (int)(bar.Width * Math.Clamp(percent.Value, 0, 100) / 100f), bar.Height);
        }
    }

    private void DrawCyberFooter(Graphics g, Rectangle bounds, bool centered = false)
    {
        using var font = new Font("Consolas", 9, FontStyle.Bold, GraphicsUnit.Pixel);
        using var cyan = new SolidBrush(CyberCyan);
        using var alert = new SolidBrush(CyberPink);
        using var muted = new SolidBrush(CyberMuted);
        var status = telemetry.SensorFault ? "SENSOR FAULT" :
            telemetry.WarningActive ? "THERMAL WARNING" : "COOLING NOMINAL";
        const string right = "LIVE TELEMETRY";
        var statusBrush = telemetry.WarningActive || telemetry.SensorFault ? alert : cyan;
        if (centered) {
            var statusSize = g.MeasureString(status, font);
            var rightSize = g.MeasureString(right, font);
            var textY = bounds.Y + (bounds.Height - statusSize.Height) / 2f;
            g.DrawString(status, font, statusBrush, bounds.X + 7, textY);
            g.DrawString(right, font, muted, bounds.Right - rightSize.Width - 6, textY);
            return;
        }
        g.DrawString(status, font, statusBrush, bounds.X + 7, bounds.Y + 7);
        var size = g.MeasureString(right, font);
        g.DrawString(right, font, muted, bounds.Right - size.Width - 6, bounds.Y + 7);
    }

    private static GraphicsPath CutCornerPath(Rectangle r, int cut)
    {
        var path = new GraphicsPath();
        path.AddPolygon(new Point[] {
            new Point(r.Left + cut, r.Top), new Point(r.Right, r.Top),
            new Point(r.Right, r.Bottom - cut), new Point(r.Right - cut, r.Bottom),
            new Point(r.Left, r.Bottom), new Point(r.Left, r.Top + cut) });
        path.CloseFigure();
        return path;
    }

    private static void DrawCentered(Graphics g, string value, Font font, Brush brush,
        float centerX, float y)
    {
        var size = g.MeasureString(value, font);
        g.DrawString(value, font, brush, centerX - size.Width / 2, y);
    }

    private static string TemperatureText(float? value) =>
        value.HasValue ? $"{value.Value:0}°C" : "--°C";
    private static string WaterText(float? value) =>
        value.HasValue ? $"{value.Value:0.0}" : "--.-";
    private static string PercentText(float? value) =>
        value.HasValue ? $"{value.Value:0}%" : "--%";
}
