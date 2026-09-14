using System.Drawing.Imaging;
using System.Text.Json;

namespace WaterCoolingDevice;

internal static class LunePanelSelfTest
{
    public static void Run()
    {
        var checks = new List<string>();
        void Check(bool condition, string name) {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        Check(LunePanelProtocol.Header(197, 0, 0, 319, 479).SequenceEqual(new byte[] { 0, 0, 4, 253, 223, 197 }), "Portrait full-frame header");
        Check(LunePanelProtocol.Header(110, 255).SequenceEqual(new byte[] { 63, 192, 0, 0, 0, 110 }), "Darkest brightness header");
        for (var rotation = 0; rotation < 4; rotation++) {
            var options = new LunePanelOptions { Landscape = rotation >= 2, Reverse = rotation % 2 == 1 };
            var packet = LunePanelProtocol.Orientation(options);
            Check(packet.Length == 16 && packet[5] == 121 && packet[6] == 100 + rotation
                && ((packet[7] << 8) | packet[8]) == options.Width
                && ((packet[9] << 8) | packet[10]) == options.Height, $"Orientation {rotation}");
        }
        using (var pixels = new Bitmap(3, 2, PixelFormat.Format32bppArgb)) {
            pixels.SetPixel(0, 0, Color.Red); pixels.SetPixel(1, 0, Color.Lime); pixels.SetPixel(2, 0, Color.Blue);
            pixels.SetPixel(0, 1, Color.White); pixels.SetPixel(1, 1, Color.Black); pixels.SetPixel(2, 1, Color.Red);
            Check(LunePanelProtocol.Encode(pixels).SequenceEqual(new byte[] { 0, 248, 224, 7, 31, 0, 255, 255, 0, 0, 0, 248 }), "RGB565 little endian and odd-width row handling");
        }
        var original = new byte[480 * 320 * 2]; var changed = (byte[])original.Clone();
        changed[(37 * 480 + 21) * 2] = 255; changed[(71 * 480 + 87) * 2 + 1] = 255;
        var bounds = LunePanelProtocol.ChangedBounds(changed, original, 480, 320);
        Check(bounds == new Rectangle(21, 37, 67, 35), "Partial update bounding rectangle");
        var rectanglePixels = LunePanelDisplay.ExtractRectangle(changed, 480, bounds);
        Check(rectanglePixels.Length == 67 * 35 * 2 && rectanglePixels[0] == 255 &&
            rectanglePixels[^1] == 255 && rectanglePixels.Count(b => b != 0) == 2,
            "Contiguous odd-width rectangle without intervening row padding");
        for (var y = bounds.Top; y < bounds.Bottom; y++)
            Array.Copy(changed, (y * 480 + bounds.X) * 2, original, (y * 480 + bounds.X) * 2, bounds.Width * 2);
        Check(original.SequenceEqual(changed), "Applying partial update reproduces full frame");
        Check(LunePanelProtocol.ChangedBounds(changed, original, 480, 320).IsEmpty, "Unchanged frame sends no image");
        Check(LunePanelProtocol.ChangedBounds(changed, null, 480, 320) == new Rectangle(0, 0, 480, 320), "Reconnect forces full frame");
        var separated = new byte[480 * 320 * 2];
        separated[(40 * 480 + 30) * 2] = 1;
        separated[(240 * 480 + 430) * 2] = 1;
        var regions = LunePanelProtocol.ChangedRegions(separated, new byte[separated.Length], 480, 320);
        Check(regions.Count == 2 && regions.Sum(region => region.Width * region.Height) <= 512,
            "Separated widgets stay separate dirty regions");
        Check(!new LunePanelOptions().Animate, "Large animation disabled by default");
        var invalid = false;
        try { LunePanelProtocol.Header(197, -1); } catch (ArgumentOutOfRangeException) { invalid = true; }
        Check(invalid, "Reject invalid coordinates");
        using (var scene = new LuneForm()) {
            using var frame1 = scene.RenderLunePanel(true);
            Thread.Sleep(60);
            using var frame2 = scene.RenderLunePanel(true);
            Check(LunePanelProtocol.Encode(frame1).SequenceEqual(LunePanelProtocol.Encode(frame2)),
                "Static scene does not redraw due to elapsed time");
            var snapshot = new LuneTelemetrySnapshot(29.5f, null, null, 45, true, false, false,
                Color.Cyan, Color.Red);
            scene.UpdateTelemetry(snapshot);
            using var before = scene.RenderLunePanel(true);
            scene.UpdateTelemetry(snapshot with { WaterTemperature = 29.6f });
            using var after = scene.RenderLunePanel(true);
            var delta = LunePanelProtocol.ChangedBounds(LunePanelProtocol.Encode(after), LunePanelProtocol.Encode(before), 480, 320);
            Check(!delta.IsEmpty && delta.Left >= 254 && delta.Width * delta.Height < 10000,
                "Temperature-only change leaves mascot and background untouched");
            scene.UpdateTelemetry(snapshot with { CpuUsage = 37, MemoryUsage = 52 });
            using var cyber1 = scene.RenderLunePanelCyber(true);
            scene.UpdateTelemetry(snapshot with { CpuUsage = 38, MemoryUsage = 53 });
            using var cyber2 = scene.RenderLunePanelCyber(true);
            var cyberRegions = LunePanelProtocol.ChangedRegions(
                LunePanelProtocol.Encode(cyber2), LunePanelProtocol.Encode(cyber1), 480, 320);
            Check(cyberRegions.Count is >= 1 and <= 4 &&
                cyberRegions.Sum(region => region.Width * region.Height) < 24000,
                "Cyber telemetry uses compact dirty regions");
            using var cyberMotion1 = scene.RenderLunePanelCyber(true, 1);
            using var cyberMotion2 = scene.RenderLunePanelCyber(true, 2);
            var motionRegions = LunePanelProtocol.ChangedRegions(
                LunePanelProtocol.Encode(cyberMotion2), LunePanelProtocol.Encode(cyberMotion1), 480, 320);
            Check(motionRegions.Count is >= 1 and <= 6 &&
                motionRegions.Sum(region => region.Width * region.Height) < 8000,
                "Cyber scanner animation stays inside compact dirty regions");
        }
        var theme = LuneThemeDocument.CreateDefault();
        theme.Name = "Self Test Theme";
        theme.Reverse = true;
        theme.Brightness = 73;
        static string TinyGif(Color color) {
            using var bitmap = new Bitmap(3, 3);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(color);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Gif);
            return Convert.ToBase64String(stream.ToArray());
        }
        theme.Animations.Add(new LuneThemeAnimation {
            Name = "fan-left.gif", GifBase64 = TinyGif(Color.Cyan),
            X = 10, Y = 10, Width = 24, Height = 24, FrameIntervalMs = 300
        });
        theme.Animations.Add(new LuneThemeAnimation {
            Name = "fan-right.gif", GifBase64 = TinyGif(Color.Magenta),
            X = 440, Y = 10, Width = 24, Height = 24, FrameIntervalMs = 700
        });
        theme.Elements.Add(new LuneThemeElement { Metric = LuneMetric.Fan1Rpm,
            Label = "FAN 1", X = 170, Y = 220, Width = 140, Height = 60,
            LabelFontSize = 13, ValueColorArgb = Color.Lime.ToArgb() });
        var themePath = Path.Combine(AppContext.BaseDirectory, "self-test.lunetheme");
        try {
            LuneThemeStore.Save(themePath, theme);
            var loaded = LuneThemeStore.Load(themePath);
            Check(loaded.Name == theme.Name && loaded.Reverse && loaded.Brightness == 73 &&
                loaded.Animations.Count == 2 && loaded.AnimationFrameIntervalMs() == 300 &&
                loaded.Elements.Any(item => item.Metric == LuneMetric.Fan1Rpm &&
                    item.LabelFontSize == 13 &&
                    item.ValueColorArgb == Color.Lime.ToArgb()),
                "Theme file round trip preserves layout and display settings");
            using var renderer = new LuneThemeRenderer(loaded);
            var values = new LuneTelemetrySnapshot(29.5f, 46, 42, 55, true, false, false,
                Color.Cyan, Color.DeepPink, 37, 52, 45, 1380, 60, 2200);
            using var custom1 = renderer.Render(values);
            using var custom2 = renderer.Render(values with { Fan1Rpm = 1420 });
            Check(custom1.Size == new Size(480, 320), "Custom theme renders at landscape panel size");
            using var portraitCrop = renderer.Render(values, 0, new Size(320, 480));
            Check(portraitCrop.Size == new Size(320, 480) &&
                LunePanelProtocol.Encode(portraitCrop).Length == 320 * 480 * 2,
                "Landscape theme clips to exact portrait transfer dimensions");
            var customRegions = LunePanelProtocol.ChangedRegions(
                LunePanelProtocol.Encode(custom2), LunePanelProtocol.Encode(custom1), 480, 320);
            Check(customRegions.Count > 0 && customRegions.Sum(region => region.Width * region.Height) < 12000,
                "Custom metric change stays inside a compact dirty region");
            Check(LuneThemeRenderer.MetricText(theme.Elements[^1], values) == "1380 rpm",
                "RPM data binding formats live telemetry");
            var namedValues = values with {
                CpuName = "AMD Ryzen 9 9950X", GpuName = "NVIDIA GeForce RTX 5090"
            };
            Check(LuneThemeRenderer.MetricText(
                    new LuneThemeElement { Metric = LuneMetric.CpuName }, namedValues) ==
                  "AMD Ryzen 9 9950X" &&
                  LuneThemeRenderer.MetricText(
                    new LuneThemeElement { Metric = LuneMetric.GpuName }, namedValues) ==
                  "NVIDIA GeForce RTX 5090",
                "CPU and GPU device names bind to theme elements");
        } finally { if (File.Exists(themePath)) File.Delete(themePath); }
        using (var display = new LunePanelDisplay()) {
            var options = new LunePanelOptions { Enabled = true, Port = "INVALID_TEST_PORT" };
            display.Configure(options);
            using var bitmap = new Bitmap(480, 320);
            display.Submit(bitmap, options);
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!display.Status.Contains("Retrying") && DateTime.UtcNow < deadline) Thread.Sleep(20);
            Check(display.Status.Contains("Retrying"), "Connection failure contained in display worker");
            display.Dispose();
            Check(display.Completion.IsCompletedSuccessfully, "Worker releases resources on shutdown");
        }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "lune-panel-test-results.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
