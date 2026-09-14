namespace WaterCoolingDevice;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(argument => string.Equals(
                argument, HardwareTemperatureAgentOptions.AgentArgument,
                StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = HardwareTemperatureAgent.Run();
            return;
        }

        ApplicationConfiguration.Initialize();

        if (args.Contains("--test-studio-flow")) {
            using var main = new MainForm(connectOnShown: false);
            main.VerifyStudioFlow();
            return;
        }

        if (args.Contains("--test-lune-panel"))
        {
            LunePanelSelfTest.Run();
            return;
        }

        if (args.Contains("--render-lune-panel-preview"))
        {
            using var scene = new LuneForm();
            scene.UpdateTelemetry(new LuneTelemetrySnapshot(29.5f, 42, 40, 45, true, false, false,
                Color.FromArgb(40, 170, 255), Color.FromArgb(255, 70, 60), 37, 52));
            using var landscape = scene.RenderLunePanel(true);
            using var portrait = scene.RenderLunePanel(false);
            using var cyberLandscape = scene.RenderLunePanelCyber(true);
            using var cyberPortrait = scene.RenderLunePanelCyber(false);
            landscape.Save(Path.Combine(AppContext.BaseDirectory, "lune-panel-landscape.png"));
            portrait.Save(Path.Combine(AppContext.BaseDirectory, "lune-panel-portrait.png"));
            cyberLandscape.Save(Path.Combine(AppContext.BaseDirectory, "lune-panel-cyber-landscape.png"));
            cyberPortrait.Save(Path.Combine(AppContext.BaseDirectory, "lune-panel-cyber-portrait.png"));
            using var customRenderer = new LuneThemeRenderer(LuneThemeDocument.CreateDefault());
            using var custom = customRenderer.Render(new LuneTelemetrySnapshot(
                29.5f, 46, 42, 55, true, false, false, Color.Cyan, Color.DeepPink,
                37, 52, 45, 1380, 60, 2200));
            custom.Save(Path.Combine(AppContext.BaseDirectory, "lune-panel-custom-preview.png"));
            return;
        }

        if (args.Contains("--smoke-test-theme-editor"))
        {
            using var editor = new LuneThemeEditorForm();
            using var closeTimer = new System.Windows.Forms.Timer { Interval = 400 };
            editor.Shown += (_, _) => closeTimer.Start();
            closeTimer.Tick += (_, _) => { closeTimer.Stop(); editor.Close(); };
            Application.Run(editor);
            return;
        }

        if (args.Contains("--render-theme-editor-preview"))
        {
            using var editor = new LuneThemeEditorForm(
                english: args.Contains("--english"));
            editor.Show();
            Application.DoEvents();
            using var preview = new Bitmap(editor.Width, editor.Height);
            editor.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
            preview.Save(Path.Combine(AppContext.BaseDirectory, "lune-theme-editor-preview.png"));
            editor.Close();
            return;
        }

        var themePreviewIndex = Array.FindIndex(args, value => value == "--render-theme-file");
        if (themePreviewIndex >= 0 && themePreviewIndex + 1 < args.Length)
        {
            var theme = LuneThemeStore.Load(args[themePreviewIndex + 1]);
            using var renderer = new LuneThemeRenderer(theme);
            var values = new LuneTelemetrySnapshot(29.5f, 46, 42, 55, true, false, false,
                Color.Cyan, Color.DeepPink, 37, 52, 45, 1380, 60, 2200,
                "AMD Ryzen 7 5800X", "AMD Radeon RX 6900 XT", 34);
            using var first = renderer.Render(values, 0);
            using var second = renderer.Render(values, 1);
            first.Save(Path.Combine(AppContext.BaseDirectory, "lune-theme-frame-0.png"));
            second.Save(Path.Combine(AppContext.BaseDirectory, "lune-theme-frame-1.png"));
            return;
        }

        if (args.Any(argument => string.Equals(
                argument, "--smoke-test-disconnected-lune", StringComparison.OrdinalIgnoreCase)))
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Exception? uiThreadException = null;
            Application.ThreadException += (_, eventArgs) =>
                uiThreadException ??= eventArgs.Exception;
            using var main = new MainForm(connectOnShown: false);
            using var closeTimer = new System.Windows.Forms.Timer { Interval = 250 };
            main.Shown += (_, _) => {
                main.ShowDisconnectedLuneForSmokeTest();
                closeTimer.Start();
            };
            closeTimer.Tick += (_, _) => {
                closeTimer.Stop();
                main.CloseFromLuneViewForSmokeTest();
            };
            Application.Run(main);
            if (uiThreadException is not null)
                throw new InvalidOperationException(
                    "Disconnected LUNE smoke test failed.", uiThreadException);
            return;
        }

        if (args.Any(argument => string.Equals(
                argument, "--render-lune-preview", StringComparison.OrdinalIgnoreCase)))
        {
            using var lune = new LuneForm();
            lune.SavePreview(
                Path.Combine(AppContext.BaseDirectory, "WCD01-LUNE-cool-preview.png"),
                new LuneTelemetrySnapshot(
                    25.5f, 42f, 40f, 45f, true, false, false,
                    Color.FromArgb(40, 170, 255), Color.FromArgb(255, 70, 60)));
            lune.SavePreview(
                Path.Combine(AppContext.BaseDirectory, "WCD01-LUNE-warm-preview.png"),
                new LuneTelemetrySnapshot(
                    33.0f, 72f, 68f, 45f, true, false, false,
                    Color.FromArgb(40, 170, 255), Color.FromArgb(255, 70, 60)));
            lune.SavePreview(
                Path.Combine(AppContext.BaseDirectory, "WCD01-LUNE-critical-preview.png"),
                new LuneTelemetrySnapshot(
                    34.0f, 88f, 82f, 45f, true, false, false,
                    Color.FromArgb(40, 170, 255), Color.FromArgb(255, 70, 60)));
            lune.SavePreview(
                Path.Combine(AppContext.BaseDirectory, "WCD01-LUNE-water-only-preview.png"),
                new LuneTelemetrySnapshot(
                    37.2f, null, null, 45f, true, false, false,
                    Color.FromArgb(40, 170, 255), Color.FromArgb(255, 70, 60)));
            lune.SavePreview(
                Path.Combine(AppContext.BaseDirectory, "WCD01-LUNE-blink-preview.png"),
                new LuneTelemetrySnapshot(
                    33.0f, 58f, 54f, 45f, true, false, false,
                    Color.FromArgb(40, 170, 255), Color.FromArgb(255, 70, 60)),
                showBlink: true);
            lune.SavePreview(
                Path.Combine(AppContext.BaseDirectory, "WCD01-LUNE-warm-blink-preview.png"),
                new LuneTelemetrySnapshot(
                    33.0f, 72f, 68f, 45f, true, false, false,
                    Color.FromArgb(40, 170, 255), Color.FromArgb(255, 70, 60)),
                showBlink: true);
            lune.SavePreview(
                Path.Combine(AppContext.BaseDirectory, "WCD01-LUNE-critical-blink-preview.png"),
                new LuneTelemetrySnapshot(
                    34.0f, 88f, 82f, 45f, true, false, false,
                    Color.FromArgb(40, 170, 255), Color.FromArgb(255, 70, 60)),
                showBlink: true);
            return;
        }

        var form = new MainForm();
        if (args.Contains("--lune-panel")) form.EnableLunePanelForSession();
        Application.Run(form);
    }
}
