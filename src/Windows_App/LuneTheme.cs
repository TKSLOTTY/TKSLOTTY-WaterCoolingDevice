using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;

namespace WaterCoolingDevice;

internal enum LuneMetric
{
    Text,
    WaterTemperature,
    CpuTemperature,
    GpuTemperature,
    CpuUsage,
    MemoryUsage,
    Fan1Duty,
    Fan1Rpm,
    Fan2Duty,
    Fan2Rpm,
    Clock,
    Date,
    CpuName,
    GpuName,
    GpuUsage
}

internal sealed class LuneThemeElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public LuneMetric Metric { get; set; } = LuneMetric.WaterTemperature;
    public string Label { get; set; } = "WATER TEMP";
    public string Text { get; set; } = "SYSTEM ONLINE";
    public int X { get; set; } = 20;
    public int Y { get; set; } = 20;
    public int Width { get; set; } = 150;
    public int Height { get; set; } = 70;
    public float FontSize { get; set; } = 28;
    public float? LabelFontSize { get; set; }
    public int ColorArgb { get; set; } = Color.Cyan.ToArgb();
    public int ValueColorArgb { get; set; } = Color.FromArgb(232, 248, 255).ToArgb();
    public bool ShowPanel { get; set; } = true;
    public bool CenterValue { get; set; }
    public bool ShowProgressBar { get; set; } = true;
    public int PanelColorArgb { get; set; } = Color.FromArgb(205, 2, 13, 32).ToArgb();
}

internal sealed class LuneThemeAnimation
{
    public string Name { get; set; } = "GIF";
    public string GifBase64 { get; set; } = string.Empty;
    public int X { get; set; } = 190;
    public int Y { get; set; } = 105;
    public int Width { get; set; } = 100;
    public int Height { get; set; } = 100;
    public int FrameIntervalMs { get; set; } = 500;
    public bool Enabled => !string.IsNullOrWhiteSpace(GifBase64);
    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? "GIF" : Name;
}

internal sealed class LuneThemeDocument
{
    public int FormatVersion { get; set; } = 1;
    public string Name { get; set; } = "My Theme";
    public bool Landscape { get; set; } = true;
    public bool Reverse { get; set; }
    public int Brightness { get; set; } = 50;
    public int BackgroundColorArgb { get; set; } = Color.FromArgb(2, 10, 30).ToArgb();
    public string BackgroundImageBase64 { get; set; } = string.Empty;
    // Animation is retained only to migrate themes created before multi-GIF support.
    public LuneThemeAnimation Animation { get; set; } = new();
    public List<LuneThemeAnimation> Animations { get; set; } = [];
    public List<LuneThemeElement> Elements { get; set; } = [];
    public int Width => Landscape ? 480 : 320;
    public int Height => Landscape ? 320 : 480;

    public IReadOnlyList<LuneThemeAnimation> ActiveAnimations() =>
        Animations.Where(item => item.Enabled).ToArray();

    public int AnimationFrameIntervalMs()
    {
        var intervals = ActiveAnimations().Select(item => item.FrameIntervalMs).ToArray();
        return intervals.Length == 0 ? 1000 : Math.Clamp(intervals.Min(), 250, 5000);
    }

    public static LuneThemeDocument CreateDefault() => new() {
        Name = "My Cyber Theme",
        Elements = [
            new() { Metric = LuneMetric.WaterTemperature, Label = "COOLANT", X = 165, Y = 105, Width = 150, Height = 90, FontSize = 34 },
            new() { Metric = LuneMetric.CpuTemperature, Label = "CPU CORE", X = 18, Y = 45, Width = 130, Height = 75, FontSize = 25 },
            new() { Metric = LuneMetric.GpuTemperature, Label = "GPU CORE", X = 332, Y = 45, Width = 130, Height = 75, FontSize = 25, ColorArgb = Color.DeepPink.ToArgb() },
            new() { Metric = LuneMetric.CpuUsage, Label = "CPU LOAD", X = 18, Y = 205, Width = 130, Height = 75, FontSize = 25 },
            new() { Metric = LuneMetric.MemoryUsage, Label = "MEMORY", X = 332, Y = 205, Width = 130, Height = 75, FontSize = 25, ColorArgb = Color.DeepPink.ToArgb() }
        ]
    };
}

internal static class LuneThemeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string ThemeFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WaterCoolingDevice", "LuneThemes");

    public static IReadOnlyList<string> FindThemes()
    {
        Directory.CreateDirectory(ThemeFolder);
        InstallBundledThemes();
        return Directory.GetFiles(ThemeFolder, "*.lunetheme")
            .Concat(Directory.GetDirectories(ThemeFolder).SelectMany(folder => Directory.GetFiles(folder, "*.lunetheme")))
            .GroupBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(path => path.Length).First())
            .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static void InstallBundledThemes()
    {
        var bundle = Path.Combine(AppContext.BaseDirectory, "Themes");
        if (!Directory.Exists(bundle)) return;
        var marker = Path.Combine(ThemeFolder, ".bundled-installed.json");
        HashSet<string> imported;
        try { imported = JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(marker)) ?? []; }
        catch { imported = []; }
        foreach (var file in Directory.GetFiles(bundle, "*.lunetheme")) {
            var name = Path.GetFileName(file);
            if (imported.Contains(name)) continue;
            // Preserve user edits, and do not resurrect a deleted bundled theme.
            var exists = Directory.GetFiles(ThemeFolder, "*.lunetheme", SearchOption.AllDirectories)
                .Any(path => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase));
            if (!exists) Install(Load(file));
            imported.Add(name);
            File.WriteAllText(marker, JsonSerializer.Serialize(imported));
        }
    }

    public static LuneThemeDocument Load(string path)
    {
        var theme = JsonSerializer.Deserialize<LuneThemeDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("テーマファイルを読み込めません。");
        Validate(theme);
        return theme;
    }

    public static void Save(string path, LuneThemeDocument theme)
    {
        Validate(theme);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Environment.ProcessId}.tmp";
        try {
            File.WriteAllText(temporary, JsonSerializer.Serialize(theme, JsonOptions));
            File.Move(temporary, path, true);
        } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string Install(LuneThemeDocument theme)
    {
        Directory.CreateDirectory(ThemeFolder);
        var invalid = Path.GetInvalidFileNameChars();
        var safeName = string.Concat(theme.Name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "My Theme";
        var folder = GetAssetFolder(theme);
        var path = Path.Combine(folder, safeName + ".lunetheme");
        Save(path, theme);
        WriteAssets(theme);
        return path;
    }

    public static void DeleteInstalled(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var themeFolder = Path.GetFullPath(ThemeFolder);
        if (!(string.Equals(Path.GetDirectoryName(fullPath), themeFolder, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(fullPath)), themeFolder, StringComparison.OrdinalIgnoreCase)) ||
            !string.Equals(Path.GetExtension(fullPath), ".lunetheme",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("テーマ保存フォルダー外のファイルは削除できません。");
        if (File.Exists(fullPath)) File.Delete(fullPath);
        var legacy = Path.Combine(themeFolder, Path.GetFileName(fullPath));
        if (File.Exists(legacy)) File.Delete(legacy);
    }

    public static string GetAssetFolder(LuneThemeDocument theme)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = string.Concat(theme.Name.Select(c => invalid.Contains(c) ? '_' : c)).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(name)) name = "My Theme";
        var folder = Path.Combine(ThemeFolder, name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string WriteAssets(LuneThemeDocument theme, bool overwrite = true)
    {
        var folder = GetAssetFolder(theme);
        var backgrounds = Path.Combine(folder, "Backgrounds");
        var gifs = Path.Combine(folder, "GIFs");
        Directory.CreateDirectory(backgrounds);
        Directory.CreateDirectory(gifs);
        if (!string.IsNullOrWhiteSpace(theme.BackgroundImageBase64) && (overwrite || !File.Exists(Path.Combine(backgrounds, "background.png"))))
            File.WriteAllBytes(Path.Combine(backgrounds, "background.png"), Convert.FromBase64String(theme.BackgroundImageBase64));
        for (var i = 0; i < theme.Animations.Count; i++) {
            var animation = theme.Animations[i];
            if (animation.Enabled && (overwrite || !File.Exists(Path.Combine(gifs, $"animation-{i + 1:00}.gif"))))
                File.WriteAllBytes(Path.Combine(gifs, $"animation-{i + 1:00}.gif"), Convert.FromBase64String(animation.GifBase64));
        }
        // Layout is separately readable; the portable theme retains embedded assets.
        File.WriteAllText(Path.Combine(folder, "layout.json"), JsonSerializer.Serialize(new {
            theme.Name, theme.Landscape, theme.Reverse, theme.Brightness, theme.Elements,
            Animations = theme.Animations.Select((a, i) => new {
                File = $"GIFs/animation-{i + 1:00}.gif", a.Name, a.X, a.Y, a.Width, a.Height, a.FrameIntervalMs
            }), Background = "Backgrounds/background.png"
        }, JsonOptions));
        return folder;
    }

    private static void Validate(LuneThemeDocument theme)
    {
        if (theme.FormatVersion != 1) throw new InvalidDataException("未対応のテーマ形式です。");
        if (string.IsNullOrWhiteSpace(theme.Name)) throw new InvalidDataException("テーマ名を入力してください。");
        theme.Brightness = Math.Clamp(theme.Brightness, 0, 100);
        foreach (var item in theme.Elements) {
            item.Width = Math.Clamp(item.Width, 12, theme.Width);
            item.Height = Math.Clamp(item.Height, 12, theme.Height);
            item.X = Math.Clamp(item.X, 0, theme.Width - item.Width);
            item.Y = Math.Clamp(item.Y, 0, theme.Height - item.Height);
            item.FontSize = Math.Clamp(item.FontSize, 8, 80);
            if (item.LabelFontSize.HasValue)
                item.LabelFontSize = Math.Clamp(item.LabelFontSize.Value, 8, 80);
        }
        theme.Animation ??= new();
        theme.Animations ??= [];
        if (theme.Animations.Count == 0 && theme.Animation.Enabled) {
            theme.Animations.Add(theme.Animation);
            theme.Animation = new();
        }
        foreach (var animation in theme.Animations) {
            animation.Name = string.IsNullOrWhiteSpace(animation.Name) ? "GIF" : animation.Name.Trim();
            animation.Width = Math.Clamp(animation.Width, 8, theme.Width);
            animation.Height = Math.Clamp(animation.Height, 8, theme.Height);
            animation.X = Math.Clamp(animation.X, 0, theme.Width - animation.Width);
            animation.Y = Math.Clamp(animation.Y, 0, theme.Height - animation.Height);
            animation.FrameIntervalMs = Math.Clamp(animation.FrameIntervalMs, 250, 5000);
        }
    }
}

internal sealed class LuneThemeRenderer : IDisposable
{
    private sealed class AnimationResource : IDisposable
    {
        public LuneThemeAnimation Definition { get; }
        public MemoryStream Stream { get; }
        public Image Image { get; }
        public FrameDimension Dimension { get; }
        public int FrameCount { get; }

        public AnimationResource(LuneThemeAnimation definition)
        {
            Definition = definition;
            Stream = new MemoryStream(Convert.FromBase64String(definition.GifBase64));
            Image = Image.FromStream(Stream);
            Dimension = new FrameDimension(Image.FrameDimensionsList[0]);
            FrameCount = Math.Max(1, Image.GetFrameCount(Dimension));
        }

        public void Dispose() { Image.Dispose(); Stream.Dispose(); }
    }

    private readonly LuneThemeDocument theme;
    private Bitmap? background;
    private readonly List<AnimationResource> animations = [];

    public LuneThemeRenderer(LuneThemeDocument theme)
    {
        this.theme = theme;
        background = DecodeBitmap(theme.BackgroundImageBase64);
        foreach (var definition in theme.ActiveAnimations())
            animations.Add(new AnimationResource(definition));
    }

    public Bitmap Render(LuneTelemetrySnapshot values, int animationFrame = 0, Size? outputSize = null)
    {
        var size = outputSize ?? new Size(theme.Width, theme.Height);
        var result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.FromArgb(theme.BackgroundColorArgb));
        // Preserve the authored coordinate system. Anything outside the selected
        // panel orientation is clipped by the target bitmap instead of changing row width.
        if (background is not null) g.DrawImageUnscaled(background, 0, 0);
        else DrawDefaultBackground(g, size);

        var baseInterval = theme.AnimationFrameIntervalMs();
        foreach (var animation in animations) {
            var definition = animation.Definition;
            var elapsed = (long)Math.Abs(animationFrame) * baseInterval;
            var frame = (int)(elapsed / Math.Max(250, definition.FrameIntervalMs) % animation.FrameCount);
            animation.Image.SelectActiveFrame(animation.Dimension, frame);
            g.DrawImage(animation.Image, new Rectangle(definition.X, definition.Y,
                definition.Width, definition.Height));
        }
        foreach (var item in theme.Elements) DrawElement(g, item, values);
        return result;
    }

    private static void DrawDefaultBackground(Graphics g, Size size)
    {
        using var gradient = new LinearGradientBrush(new Rectangle(Point.Empty, size),
            Color.FromArgb(2, 8, 26), Color.FromArgb(4, 30, 55), 25f);
        g.FillRectangle(gradient, 0, 0, size.Width, size.Height);
        using var grid = new Pen(Color.FromArgb(18, 0, 220, 255));
        for (var x = 0; x < size.Width; x += 24) g.DrawLine(grid, x, 0, x, size.Height);
        for (var y = 0; y < size.Height; y += 24) g.DrawLine(grid, 0, y, size.Width, y);
    }

    private static void DrawElement(Graphics g, LuneThemeElement item, LuneTelemetrySnapshot values)
    {
        var bounds = new Rectangle(item.X, item.Y, item.Width, item.Height);
        var color = Color.FromArgb(item.ColorArgb);
        if (item.ShowPanel) {
            using var fill = new SolidBrush(Color.FromArgb(item.PanelColorArgb));
            using var border = new Pen(Color.FromArgb(170, color), 1.2f);
            g.FillRectangle(fill, bounds);
            g.DrawRectangle(border, bounds);
        }
        var labelSize = item.LabelFontSize ?? Math.Max(8, item.FontSize * .36f);
        using var labelFont = new Font("Consolas", labelSize,
            FontStyle.Bold, GraphicsUnit.Pixel);
        using var accent = new SolidBrush(color);
        using var valueBrush = new SolidBrush(Color.FromArgb(item.ValueColorArgb));
        if (!string.IsNullOrWhiteSpace(item.Label)) g.DrawString(item.Label, labelFont, accent, bounds.X + 6, bounds.Y + 5);
        var value = MetricText(item, values);
        // Anchor the value to the label, not to box height. Resizing the box must
        // only change its clipping area; it must never move the text.
        var valueY = string.IsNullOrWhiteSpace(item.Label)
            ? bounds.Y + 4
            : bounds.Y + 7 + labelFont.GetHeight(g);
        var valueArea = new RectangleF(bounds.X + 5, valueY,
            Math.Max(1, bounds.Width - 10), Math.Max(1, bounds.Bottom - valueY - 4));
        // FontSize is the actual requested size. The box only clips/trims text and
        // never changes its scale, so resizing a box cannot resize its contents.
        using var valueFont = new Font("Segoe UI", item.FontSize,
            FontStyle.Bold, GraphicsUnit.Pixel);
        using var valueFormat = new StringFormat {
            Alignment = item.CenterValue ? StringAlignment.Center : StringAlignment.Near,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        g.DrawString(value, valueFont, valueBrush, valueArea, valueFormat);
        if (item.ShowProgressBar && item.Metric is (LuneMetric.GpuUsage or LuneMetric.CpuUsage or LuneMetric.MemoryUsage or LuneMetric.Fan1Duty or LuneMetric.Fan2Duty)) {
            var percent = MetricValue(item.Metric, values);
            using var track = new SolidBrush(Color.FromArgb(45, 80, 100));
            g.FillRectangle(track, bounds.X + 6, bounds.Bottom - 7, bounds.Width - 12, 3);
            if (percent.HasValue) g.FillRectangle(accent, bounds.X + 6, bounds.Bottom - 7,
                (int)((bounds.Width - 12) * Math.Clamp(percent.Value, 0, 100) / 100), 3);
        }
    }

    internal static string MetricText(LuneThemeElement item, LuneTelemetrySnapshot v) => item.Metric switch {
        LuneMetric.Text => item.Text,
        LuneMetric.WaterTemperature => Temperature(v.WaterTemperature, 1),
        LuneMetric.CpuTemperature => Temperature(v.CpuTemperature, 0),
        LuneMetric.GpuTemperature => Temperature(v.GpuTemperature, 0),
        LuneMetric.CpuUsage => Percent(v.CpuUsage),
        LuneMetric.GpuUsage => Percent(v.GpuUsage),
        LuneMetric.MemoryUsage => Percent(v.MemoryUsage),
        LuneMetric.Fan1Duty => v.IsConnected ? Percent(v.Fan1Duty) : "--%",
        LuneMetric.Fan1Rpm => v.IsConnected ? Rpm(v.Fan1Rpm) : "-- rpm",
        LuneMetric.Fan2Duty => v.IsConnected ? Percent(v.Fan2Duty) : "--%",
        LuneMetric.Fan2Rpm => v.IsConnected ? Rpm(v.Fan2Rpm) : "-- rpm",
        LuneMetric.Clock => DateTime.Now.ToString("HH:mm"),
        LuneMetric.Date => DateTime.Now.ToString("yyyy/MM/dd"),
        LuneMetric.CpuName => string.IsNullOrWhiteSpace(v.CpuName) ? "CPU --" : v.CpuName,
        LuneMetric.GpuName => string.IsNullOrWhiteSpace(v.GpuName) ? "GPU --" : v.GpuName,
        _ => "--"
    };

    private static float? MetricValue(LuneMetric metric, LuneTelemetrySnapshot v) => metric switch {
        LuneMetric.CpuUsage => v.CpuUsage,
        LuneMetric.GpuUsage => v.GpuUsage,
        LuneMetric.MemoryUsage => v.MemoryUsage,
        LuneMetric.Fan1Duty => v.Fan1Duty,
        LuneMetric.Fan2Duty => v.Fan2Duty,
        _ => null
    };
    private static string Temperature(float? value, int decimals) => value.HasValue ?
        $"{value.Value.ToString(decimals == 1 ? "0.0" : "0")}°C" : "--°C";
    private static string Percent(float? value) => value.HasValue ? $"{value.Value:0}%" : "--%";
    private static string Rpm(float? value) => value.HasValue ? $"{value.Value:0} rpm" : "-- rpm";

    private static Bitmap? DecodeBitmap(string base64)
    {
        var image = DecodeImage(base64);
        if (image is null) return null;
        using (image) return new Bitmap(image);
    }
    private static Image? DecodeImage(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        var bytes = Convert.FromBase64String(base64);
        using var stream = new MemoryStream(bytes);
        using var temporary = Image.FromStream(stream);
        return (Image)temporary.Clone();
    }
    public void Dispose()
    {
        background?.Dispose();
        foreach (var animation in animations) animation.Dispose();
        animations.Clear();
    }
}

