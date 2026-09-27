namespace WaterCoolingDevice;

internal sealed partial class MainForm
{
    private CoolantArgbOptions coolantArgbOptions = CoolantArgbOptions.Load();
    private CoolantArgbOutput coolantArgbOutput = new();
    private readonly CheckBox coolantArgbEnabled = new() { AutoSize = true };
    private readonly NumericUpDown[] coolantArgbTemperatures = Enumerable.Range(0, 3)
        .Select(_ => new NumericUpDown { Minimum = -20, Maximum = 60, Width = 80 }).ToArray();
    private readonly Button[] coolantArgbColors = Enumerable.Range(0, 3)
        .Select(_ => new Button { Width = 145, Height = 34 }).ToArray();
    private readonly Label[] coolantArgbLevels = Enumerable.Range(0, 3)
        .Select(_ => new Label { AutoSize = true, Width = 100 }).ToArray();
    private readonly NumericUpDown coolantArgbBrightness = new() { Minimum = 0, Maximum = 100, Width = 80 };
    private readonly Label coolantArgbBrightnessLabel = new() { AutoSize = true };
    private readonly Label coolantArgbHelp = new() { AutoSize = true, MaximumSize = new Size(720, 0) };
    private readonly Label coolantArgbStatus = new() { AutoSize = true, MaximumSize = new Size(720, 0) };
    private readonly Panel coolantArgbSwatch = new() { Width = 160, Height = 30, BackColor = Color.Black };
    private readonly Button coolantArgbSave = new() { AutoSize = true };
    private readonly Button coolantArgbWindows = new() { AutoSize = true };
    private TabPage? coolantArgbPage;

    internal void ShowCoolantArgbSettings()
    {
        RestoreFromTray();
        mainTabs.SelectedTab = coolantArgbPage;
    }

    private TabPage BuildCoolantArgbTab()
    {
        coolantArgbPage = NewTab("水温連動ARGB");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(28) };
        panel.Controls.Add(coolantArgbHelp);
        panel.Controls.Add(coolantArgbEnabled);
        var temperatures = new[] { coolantArgbOptions.LowTemperature, coolantArgbOptions.MidTemperature, coolantArgbOptions.HighTemperature };
        var colors = new[] { coolantArgbOptions.LowColor, coolantArgbOptions.MidColor, coolantArgbOptions.HighColor };
        for (var i = 0; i < 3; i++) {
            var index = i;
            coolantArgbTemperatures[i].Value = temperatures[i];
            coolantArgbColors[i].BackColor = Color.FromArgb(colors[i]);
            coolantArgbColors[i].Click += (_, _) => {
                using var dialog = new ColorDialog { Color = coolantArgbColors[index].BackColor, FullOpen = true };
                if (dialog.ShowDialog(this) == DialogResult.OK) {
                    coolantArgbColors[index].BackColor = dialog.Color;
                    coolantArgbColors[index].ForeColor = dialog.Color.GetBrightness() > .5 ? Color.Black : Color.White;
                }
            };
            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            row.Controls.AddRange([coolantArgbLevels[i], coolantArgbTemperatures[i], new Label { Text = "°C", AutoSize = true }, coolantArgbColors[i]]);
            panel.Controls.Add(row);
        }
        coolantArgbEnabled.Checked = coolantArgbOptions.Enabled;
        coolantArgbBrightness.Value = coolantArgbOptions.Brightness;
        var brightness = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 12, 0, 12) };
        brightness.Controls.AddRange([coolantArgbBrightnessLabel, coolantArgbBrightness, new Label { Text = "%", AutoSize = true }]);
        panel.Controls.Add(brightness);
        panel.Controls.Add(coolantArgbSave);
        panel.Controls.Add(coolantArgbWindows);
        panel.Controls.Add(coolantArgbSwatch);
        panel.Controls.Add(coolantArgbStatus);
        coolantArgbSave.Click += (_, _) => SaveCoolantArgbSettings();
        coolantArgbWindows.Click += (_, _) => {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:personalization-lighting") { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); }
        };
        coolantArgbPage.Controls.Add(panel);
        UpdateCoolantArgbLanguage();
        return coolantArgbPage;
    }
    private void UpdateCoolantArgbLanguage()
    {
        if (coolantArgbPage is null) return;
        coolantArgbPage.Text = T("水温連動ARGB", "Coolant ARGB");
        coolantArgbEnabled.Text = T("水温連動を有効にする（ARGB 1・2共通）", "Enable coolant lighting (ARGB 1 and 2)");
        coolantArgbHelp.Text = T(
            "選択中の本体の水温に合わせて色を滑らかに変えます。Windowsの動的ライティングと他のRGBアプリによる制御を停止してください。アプリ動作中のみ有効です。無効化・終了時は消灯し、センサー異常時は赤色になります。変更後は「保存・適用」を押してください。",
            "Colors blend with the selected controller's coolant temperature. Disable Windows Dynamic Lighting and other RGB control for this device. Runs while this app is open. Disabling or exiting turns LEDs off; sensor faults show red. Click Save and apply after editing.");
        var names = new[] { T("低温", "Low"), T("中温", "Medium"), T("高温", "High") };
        for (var i = 0; i < 3; i++) { coolantArgbLevels[i].Text = names[i]; coolantArgbColors[i].Text = T("色を選択", "Choose color"); coolantArgbColors[i].ForeColor = coolantArgbColors[i].BackColor.GetBrightness() > .5 ? Color.Black : Color.White; }
        coolantArgbBrightnessLabel.Text = T("明るさ", "Brightness");
        coolantArgbSave.Text = T("保存・適用", "Save and apply");
        coolantArgbWindows.Text = T("Windowsの動的ライティング設定を開く", "Open Windows Dynamic Lighting settings");
        coolantArgbStatus.Text = T("有効化して本体を接続すると更新します。", "Enable and connect a controller to start updating.");
    }
    private void SaveCoolantArgbSettings()
    {
        var options = new CoolantArgbOptions {
            Enabled = coolantArgbEnabled.Checked,
            LowTemperature = (int)coolantArgbTemperatures[0].Value,
            MidTemperature = (int)coolantArgbTemperatures[1].Value,
            HighTemperature = (int)coolantArgbTemperatures[2].Value,
            LowColor = coolantArgbColors[0].BackColor.ToArgb(), MidColor = coolantArgbColors[1].BackColor.ToArgb(), HighColor = coolantArgbColors[2].BackColor.ToArgb(),
            Brightness = (int)coolantArgbBrightness.Value
        };
        if (!options.Valid) { MessageBox.Show(this, T("温度は低温 < 中温 < 高温の順にしてください。", "Temperatures must be ordered: Low < Medium < High.")); return; }
        try {
            if (!studioTestMode) options.Save();
            coolantArgbOutput.Release();
            coolantArgbOptions = options;
            if (!options.Enabled) { coolantArgbOutput.Release(); coolantArgbSwatch.BackColor = Color.Black; }
            coolantArgbStatus.Text = T("保存しました。次の水温取得時に反映します。", "Saved. Applies on the next coolant reading.");
        } catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private async Task UpdateCoolantArgbAsync()
    {
        if (studioTestMode || !coolantArgbOptions.Enabled || applicationClosing) return;
        var color = sensorFault ? Color.FromArgb((int)Math.Round(255 * coolantArgbOptions.Brightness / 100f), 0, 0)
            : coolantArgbOptions.ColorAt(currentTemperature);
        var serial = client.ConnectedSerialNumber;
        var output = coolantArgbOutput;
        var generation = output.Generation;
        try {
            if (!await Task.Run(() => output.Update(serial, color, generation)).WaitAsync(TimeSpan.FromSeconds(2))) return;
            if (applicationClosing) return;
            coolantArgbSwatch.BackColor = color;
            coolantArgbStatus.Text = sensorFault ? T("センサー異常：赤色表示", "Sensor fault: showing red")
                : T($"水温 {currentTemperature:F2} °C・ARGB 1 / 2 更新中", $"Coolant {currentTemperature:F2} °C · updating ARGB 1 / 2");
        } catch (Exception) {
            if (ReferenceEquals(output, coolantArgbOutput)) {
                output.Dispose(false);
                coolantArgbOutput = new CoolantArgbOutput();
            }
            if (!applicationClosing) coolantArgbStatus.Text = T("ARGBへ送信できません。本体接続と他の照明アプリを確認してください。", "Cannot update ARGB. Check the controller connection and other lighting apps.");
        }
    }

    internal void VerifyCoolantArgb()
    {
        if (!studioTestMode) throw new InvalidOperationException("Test mode required");
        void Check(bool value) { if (!value) throw new InvalidOperationException("Coolant ARGB check failed"); }
        var options = new CoolantArgbOptions { Brightness = 100 };
        Check(!options.Enabled && options.Valid);
        Check(options.ColorAt(-20).ToArgb() == options.LowColor);
        Check(options.ColorAt(35).ToArgb() == options.MidColor);
        Check(options.ColorAt(60).ToArgb() == options.HighColor);
        var mid = options.ColorAt(30);
        Check(mid.R == 128 && mid.G == 203 && mid.B == 128);
        Check((options with { Brightness = 0 }).ColorAt(30).ToArgb() == Color.Black.ToArgb());
        Check(!(options with { MidTemperature = 25 }).Valid);
        var packet = CoolantArgbOutput.RangeReport(51, 64, Color.FromArgb(12, 34, 56));
        Check(packet[0] == 5 && packet[1] == 1 && packet[2] == 0 && packet[3] == 0 && packet[4] == 63 && packet[5] == 0);
        Check(packet[6] == 12 && packet[7] == 34 && packet[8] == 56 && packet[9] == 255);
        var original = coolantArgbOptions;
        coolantArgbEnabled.Checked = false;
        SaveCoolantArgbSettings();
        Check(!coolantArgbOptions.Enabled);
        foreach (var english in new[] { false, true }) {
            isEnglish = english;
            ApplyLanguage(this);
            Check(coolantArgbPage!.Text == (english ? "Coolant ARGB" : "水温連動ARGB"));
            mainTabs.SelectedTab = coolantArgbPage;
            Show(); Application.DoEvents();
            using var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, english ? "coolant-argb-en.png" : "coolant-argb-ja.png"));
        }
        coolantArgbOptions = original;
        Close();
    }
}
