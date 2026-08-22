namespace WaterCoolingDevice;

internal sealed class MainForm : Form
{
    private readonly HidDeviceClient client = new();
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer animationTimer = new() { Interval = 100 };
    private readonly Label deviceSelectorLabel = new() {
        Text = "接続デバイス:", AutoSize = true, Padding = new Padding(0, 7, 6, 0)
    };
    private readonly ComboBox deviceComboBox = new() {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = 560
    };
    private readonly FlowLayoutPanel deviceSelectorPanel = new() {
        Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(18, 6, 18, 4),
        Visible = false
    };
    private readonly RowStyle deviceSelectorRowStyle = new(SizeType.Absolute, 0);
    private readonly Font temperatureNormalFont = new("Segoe UI Semibold", 22f);
    private readonly Font temperatureFaultFont = new("Segoe UI Semibold", 14f);
    private readonly Label connectionLabel = MakeValueLabel("未接続", 12);
    private readonly Label temperatureLabel = MakeValueLabel("--.-- °C", 22);
    private readonly Label duty1Label = MakeValueLabel("--- %", 22);
    private readonly Label rpm1Label = MakeValueLabel("----- rpm", 22);
    private readonly Label duty2Label = MakeValueLabel("--- %", 22);
    private readonly Label rpm2Label = MakeValueLabel("----- rpm", 22);
    private readonly Label fan2DutyTitle = new() { Text = "ファン2 出力", AutoSize = true, ForeColor = Color.Silver };
    private readonly Label fan2RpmTitle = new() { Text = "RPM 2", AutoSize = true, ForeColor = Color.Silver };
    private readonly Label failSafeStatusLabel = new() {
        Text = "センサーエラー - フェイルセーフ作動中（ファン出力 100%）",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI Semibold", 11f),
        ForeColor = Color.White,
        BackColor = Color.FromArgb(165, 42, 42),
        Visible = false,
        Margin = new Padding(5, 2, 5, 4)
    };
    private readonly RowStyle failSafeRowStyle = new(SizeType.Absolute, 0);
    private readonly Label pumpErrorStatusLabel = new() {
        Text = "ポンプエラー - フェイルセーフ作動中（ポンプ出力 100%）",
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI Semibold", 11f), ForeColor = Color.White,
        BackColor = Color.DarkOrange, Visible = false, Margin = new Padding(5, 2, 5, 4)
    };
    private readonly RowStyle pumpErrorRowStyle = new(SizeType.Absolute, 0);
    private readonly DutyGraphControl graph = new() { Dock = DockStyle.Fill };
    private readonly DutyGraphControl editGraph = new() { Dock = DockStyle.Fill, Editable = true };
    private readonly NumericUpDown[] duty1Editors = new NumericUpDown[5];
    private readonly NumericUpDown[] duty2Editors = new NumericUpDown[5];
    private readonly Button saveButton = new() { Text = "本体へ保存", AutoSize = true };
    private readonly Button reloadButton = new() { Text = "再読込", AutoSize = true };
    private readonly Button resetButton = new() { Text = "初期値に戻す", AutoSize = true };
    private readonly Button copyFan1ToFan2Button = new() {
        Text = "ファン1 → ファン2へコピー", AutoSize = true
    };
    private readonly CheckBox englishCheckBox = new() {
        Text = "English", AutoSize = true, Anchor = AnchorStyles.Left
    };
    private readonly CheckBox startupCheckBox = new() {
        Text = "Windows起動時に起動", AutoSize = true, Anchor = AnchorStyles.Left
    };
    private readonly CheckBox warningBeepCheckBox = new() {
        Text = "警告温度でBEEPを鳴らす", AutoSize = true, Anchor = AnchorStyles.Left
    };
    private readonly CheckBox alwaysOnTopCheckBox = new() {
        Text = "常に手前に表示", AutoSize = true, Anchor = AnchorStyles.Left
    };
    private readonly CheckBox pumpModeCheckBox = new() {
        Text = "FAN2をPUMPとして使用（実験的機能）", AutoSize = true
    };
    private readonly NumericUpDown pumpDutyEditor = new() {
        Minimum = 35, Maximum = 100, Value = 60, Width = 70,
        TextAlign = HorizontalAlignment.Center
    };
    private readonly Button warningColorButton = new() {
        Text = "警告温度の色を選択", AutoSize = true
    };
    private readonly Panel warningColorPreview = new() {
        Width = 44, Height = 24, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button lowColorButton = new() {
        Text = "20℃以下の色を選択", AutoSize = true
    };
    private readonly Panel lowColorPreview = new() {
        Width = 44, Height = 24, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly NotifyIcon trayIcon = new() { Text = "Water Cooling Device" };
    private readonly AppSettings appSettings = AppSettings.Load();
    private readonly NumericUpDown warningEditor = new() {
        Minimum = 0, Maximum = 60, Value = 55, Width = 70,
        TextAlign = HorizontalAlignment.Center
    };
    private bool busy;
    private int[] duty1Table = { 10, 20, 25, 40, 70 };
    private int[] duty2Table = { 10, 20, 25, 40, 70 };
    private bool updatingEditors;
    private bool isEnglish;
    private Color warningColor;
    private Color lowColor;
    private float currentTemperature;
    private Icon? generatedTrayIcon;
    private bool warningAlarmActive;
    private bool sensorFault;
    private bool reconnecting;
    private bool updatingDeviceSelector;
    private string? preferredDeviceIdentity;
    private string? pendingDeviceIdentity;
    private string lastDeviceListSignature = string.Empty;
    private string connectionStatusJapanese = "未接続";
    private string connectionStatusEnglish = "Disconnected";
    private string connectionDetailJapanese = string.Empty;
    private string connectionDetailEnglish = string.Empty;
    private Color connectionStatusColor = Color.White;
    private DateTime nextReconnectAttemptUtc = DateTime.MinValue;
    private DateTime nextDeviceScanUtc = DateTime.MinValue;
    private int lastSettingsVersion = -1;

    private static readonly Dictionary<string, string> JapaneseToEnglish = new() {
        ["状態表示"] = "Status",
        ["未接続"] = "Disconnected",
        ["接続中..."] = "Connecting...",
        ["● 水冷ファンコントローラ 接続済み"] = "● Water Cooling Device connected",
        ["接続デバイス:"] = "Device:",
        ["Duty Table編集"] = "Fan Curve Settings",
        ["設定"] = "Settings",
        ["再接続"] = "Reconnect",
        ["水温"] = "Coolant Temperature",
        ["センサー故障"] = "Sensor Fault",
        ["再接続待機中"] = "Waiting to reconnect",
        ["センサーエラー - フェイルセーフ作動中（ファン出力 100%）"] =
            "SENSOR ERROR - FAIL-SAFE ACTIVE (FAN OUTPUT 100%)",
        ["ポンプエラー - フェイルセーフ作動中（ポンプ出力 100%）"] =
            "PUMP ERROR - FAIL-SAFE ACTIVE (PUMP OUTPUT 100%)",
        ["ファン1 出力"] = "Fan 1 Output",
        ["RPM 1"] = "Fan 1 Speed",
        ["ファン2 出力"] = "Fan 2 Output",
        ["RPM 2"] = "Fan 2 Speed",
        ["ファンカーブ  —  X: 水温 20～60°C / Y: ファン出力 0～100%"] =
            "Fan Curve  —  X: Coolant 20–60°C / Y: Fan Output 0–100%",
        ["水温ポイント"] = "Temperature Point",
        ["本体へ保存"] = "Save to Device",
        ["再読込"] = "Reload",
        ["初期値に戻す"] = "Restore Defaults",
        ["警告温度:"] = "Warning Temperature:",
        ["設定は本体受信後500msで保存されます。"] =
            "Settings are saved to the Water Cooling Device 500 ms after reception.",
        ["マウス編集: 左ドラッグ＝FAN1（青・実線・丸） / 右ドラッグ＝FAN2（赤・破線・四角）"] =
            "Mouse editing: Left drag = Fan 1 (blue/solid/circle), Right drag = Fan 2 (red/dashed/square)",
        ["Windows起動時に起動"] = "Start with Windows",
        ["警告温度の色を選択"] = "Choose Warning Color",
        ["表示言語"] = "Display Language",
        ["自動起動"] = "Startup",
        ["温度表示"] = "Temperature Display",
        ["警告音"] = "Warning Sound",
        ["20℃以下の色を選択"] = "Choose Low Temperature Color",
        ["警告温度でBEEPを鳴らす"] = "Beep at Warning Temperature",
        ["ウィンドウ表示"] = "Window",
        ["常に手前に表示"] = "Always on Top",
        ["ファン1 → ファン2へコピー"] = "Copy Fan 1 to Fan 2",
        ["PUMP設定"] = "PUMP Settings",
        ["FAN2をPUMPとして使用（実験的機能）"] = "Use FAN2 as PUMP (experimental)",
        ["Pump Duty:"] = "Pump Duty:",
        ["実験的機能／対応ポンプでのみ使用／設定によってはポンプ停止の可能性"] =
            "Experimental / Use only with compatible pumps / Incorrect settings may stop the pump",
        ["PUMP 出力"] = "PUMP Output",
        ["PUMP RPM"] = "PUMP Speed",
        ["最小化すると通知領域に現在水温と温度色を表示します。"] =
            "When minimized, the notification area shows coolant temperature and its color."
    };

    public MainForm()
    {
        Text = "Water Cooling Device with Dynamic Lighting";
        ClientSize = new Size(980, 680);
        MinimumSize = new Size(820, 580);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(30, 33, 39);
        ForeColor = Color.Gainsboro;

        var tabs = new StyledTabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildMonitorTab());
        tabs.TabPages.Add(BuildEditorTab());
        tabs.TabPages.Add(BuildSettingsTab());
        deviceSelectorPanel.Controls.Add(deviceSelectorLabel);
        deviceSelectorPanel.Controls.Add(deviceComboBox);
        var shell = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2
        };
        shell.RowStyles.Add(deviceSelectorRowStyle);
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(deviceSelectorPanel, 0, 0);
        shell.Controls.Add(tabs, 0, 1);
        Controls.Add(shell);

        refreshTimer.Tick += async (_, _) => await MonitorConnectionAsync();
        animationTimer.Tick += (_, _) => graph.Invalidate();
        animationTimer.Start();
        Shown += async (_, _) => await ConnectAsync(true);
        deviceComboBox.SelectedIndexChanged +=
            async (_, _) => await DeviceSelectionChangedAsync();
        FormClosed += (_, _) => {
            animationTimer.Stop();
            animationTimer.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            generatedTrayIcon?.Dispose();
            temperatureNormalFont.Dispose();
            temperatureFaultFont.Dispose();
            client.Dispose();
        };
        englishCheckBox.CheckedChanged += (_, _) => {
            isEnglish = englishCheckBox.Checked;
            ApplyLanguage(this);
            RefreshConnectionStatusDisplay();
            appSettings.English = isEnglish;
            appSettings.Save();
        };
        startupCheckBox.CheckedChanged += (_, _) => StartupManager.SetEnabled(startupCheckBox.Checked);
        warningColorButton.Click += (_, _) => ChooseWarningColor();
        lowColorButton.Click += (_, _) => ChooseLowColor();
        warningBeepCheckBox.CheckedChanged += (_, _) => {
            appSettings.BeepOnWarning = warningBeepCheckBox.Checked;
            appSettings.Save();
        };
        alwaysOnTopCheckBox.CheckedChanged += (_, _) => {
            TopMost = alwaysOnTopCheckBox.Checked;
            appSettings.AlwaysOnTop = alwaysOnTopCheckBox.Checked;
            appSettings.Save();
        };
        pumpModeCheckBox.CheckedChanged += async (_, _) => await PumpSettingChangedAsync();
        pumpDutyEditor.ValueChanged += async (_, _) => await PumpSettingChangedAsync();
        Resize += (_, _) => MinimizeToTrayIfNeeded();

        warningColor = Color.FromArgb(appSettings.WarningColorArgb);
        lowColor = Color.FromArgb(appSettings.LowColorArgb);
        warningColorPreview.BackColor = warningColor;
        lowColorPreview.BackColor = lowColor;
        warningBeepCheckBox.Checked = appSettings.BeepOnWarning;
        alwaysOnTopCheckBox.Checked = appSettings.AlwaysOnTop;
        TopMost = appSettings.AlwaysOnTop;
        startupCheckBox.Checked = StartupManager.IsEnabled();
        englishCheckBox.Checked = appSettings.English;
        pumpDutyEditor.Value = Math.Clamp(appSettings.PumpDuty, 35, 100);
        pumpModeCheckBox.Checked = appSettings.Fan2PumpMode;
        isEnglish = englishCheckBox.Checked;
        ApplyLanguage(this);
        RefreshConnectionStatusDisplay();
        ConfigureTrayIcon();
        editGraph.DutyPointChanged += (_, e) => {
            var editors = e.Fan == 1 ? duty1Editors : duty2Editors;
            editors[e.Point].Value = e.Duty;
        };
    }

    private TabPage BuildMonitorTab()
    {
        var page = NewTab("状態表示");
        var root = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(failSafeRowStyle);
        root.RowStyles.Add(pumpErrorRowStyle);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var reconnect = new Button { Text = "再接続", AutoSize = true };
        reconnect.Click += async (_, _) => await ConnectAsync(true);
        header.Controls.Add(connectionLabel);
        header.Controls.Add(reconnect);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
        for (var i = 0; i < 5; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        cards.Controls.Add(MakeCard("水温", temperatureLabel), 0, 0);
        cards.Controls.Add(MakeCard("ファン1 出力", duty1Label), 1, 0);
        cards.Controls.Add(MakeCard("RPM 1", rpm1Label), 2, 0);
        cards.Controls.Add(MakeCard(fan2DutyTitle, duty2Label), 3, 0);
        cards.Controls.Add(MakeCard(fan2RpmTitle, rpm2Label), 4, 0);

        var graphGroup = new GroupBox {
            Text = "ファンカーブ  —  X: 水温 20～60°C / Y: ファン出力 0～100%",
            Dock = DockStyle.Fill, ForeColor = ForeColor, Padding = new Padding(10)
        };
        graphGroup.Controls.Add(graph);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(failSafeStatusLabel, 0, 1);
        root.Controls.Add(pumpErrorStatusLabel, 0, 2);
        root.Controls.Add(cards, 0, 3);
        root.Controls.Add(graphGroup, 0, 4);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildSettingsTab()
    {
        var page = NewTab("設定");
        var panel = new TableLayoutPanel {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2,
            Padding = new Padding(30), CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));

        panel.Controls.Add(MakeHeader("表示言語"), 0, 0);
        panel.Controls.Add(englishCheckBox, 1, 0);
        panel.Controls.Add(MakeHeader("自動起動"), 0, 1);
        panel.Controls.Add(startupCheckBox, 1, 1);
        panel.Controls.Add(MakeHeader("温度表示"), 0, 2);

        var colorRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        colorRow.Controls.Add(lowColorButton);
        colorRow.Controls.Add(lowColorPreview);
        colorRow.Controls.Add(warningColorButton);
        colorRow.Controls.Add(warningColorPreview);
        colorRow.Controls.Add(new Label {
            Text = "最小化すると通知領域に現在水温と温度色を表示します。",
            AutoSize = true, Padding = new Padding(10, 6, 0, 0)
        });
        panel.Controls.Add(colorRow, 1, 2);
        panel.Controls.Add(MakeHeader("警告音"), 0, 3);
        panel.Controls.Add(warningBeepCheckBox, 1, 3);
        panel.Controls.Add(MakeHeader("ウィンドウ表示"), 0, 4);
        panel.Controls.Add(alwaysOnTopCheckBox, 1, 4);
        panel.Controls.Add(MakeHeader("PUMP設定"), 0, 5);
        var pumpRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        pumpRow.Controls.Add(pumpModeCheckBox);
        pumpRow.Controls.Add(new Label { Text = "Pump Duty:", AutoSize = true, Padding = new Padding(16, 6, 0, 0) });
        pumpRow.Controls.Add(pumpDutyEditor);
        pumpRow.Controls.Add(new Label { Text = "%（最小35%）", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        pumpRow.Controls.Add(new Label {
            Text = "実験的機能／対応ポンプでのみ使用／設定によってはポンプ停止の可能性",
            AutoSize = true, ForeColor = Color.Orange, Padding = new Padding(0, 8, 0, 0)
        });
        panel.Controls.Add(pumpRow, 1, 5);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildEditorTab()
    {
        var page = NewTab("Duty Table編集");
        var outer = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(18)
        };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        var table = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = 6,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        for (var i = 0; i < 5; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.4f));

        var temperatures = new[] { 20, 30, 40, 50, 60 };
        table.Controls.Add(MakeHeader("水温ポイント"), 0, 0);
        for (var i = 0; i < 5; i++) table.Controls.Add(MakeHeader($"{temperatures[i]} °C"), i + 1, 0);
        table.Controls.Add(MakeHeader("ファン1 出力"), 0, 1);
        table.Controls.Add(MakeHeader("ファン2 出力"), 0, 2);

        for (var i = 0; i < 5; i++)
        {
            duty1Editors[i] = MakeEditor();
            duty2Editors[i] = MakeEditor();
            var point = i;
            duty1Editors[i].ValueChanged += (_, _) => EditorValueChanged(1, point);
            duty2Editors[i].ValueChanged += (_, _) => EditorValueChanged(2, point);
            table.Controls.Add(duty1Editors[i], i + 1, 1);
            table.Controls.Add(duty2Editors[i], i + 1, 2);
        }

        var buttons = new FlowLayoutPanel {
            Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(4, 8, 4, 4)
        };
        saveButton.Click += async (_, _) => await SaveTablesAsync();
        reloadButton.Click += async (_, _) => await LoadTablesAsync();
        resetButton.Click += (_, _) => ResetEditorsToDefaults();
        copyFan1ToFan2Button.Click += (_, _) => CopyFan1ToFan2();
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(reloadButton);
        buttons.Controls.Add(resetButton);
        buttons.Controls.Add(copyFan1ToFan2Button);
        buttons.Controls.Add(new Label {
            Text = "警告温度:", AutoSize = true, Padding = new Padding(16, 7, 0, 0)
        });
        buttons.Controls.Add(warningEditor);
        buttons.Controls.Add(new Label {
            Text = "°C", AutoSize = true, Padding = new Padding(0, 7, 8, 0)
        });
        buttons.Controls.Add(new Label {
            Text = "設定は本体受信後500msで保存されます。",
            AutoSize = true, Padding = new Padding(12, 7, 0, 0), ForeColor = Color.Silver
        });

        var graphGroup = new GroupBox {
            Text = "マウス編集: 左ドラッグ＝FAN1（青・実線・丸） / 右ドラッグ＝FAN2（赤・破線・四角）",
            Dock = DockStyle.Fill, ForeColor = ForeColor, Padding = new Padding(10)
        };
        graphGroup.Controls.Add(editGraph);

        outer.Controls.Add(table, 0, 0);
        outer.Controls.Add(graphGroup, 0, 1);
        outer.Controls.Add(buttons, 0, 2);
        page.Controls.Add(outer);
        return page;
    }

    private async Task MonitorConnectionAsync()
    {
        if (reconnecting || busy) return;

        if (client.IsConnected) {
            if (DateTime.UtcNow >= nextDeviceScanUtc)
                await RefreshDeviceSelectorAsync();
            await RefreshStatusAsync();
            return;
        }

        if (DateTime.UtcNow >= nextReconnectAttemptUtc)
            await ConnectAsync(false);
    }

    private async Task ConnectAsync(bool manual, string? requestedIdentity = null)
    {
        if (reconnecting) return;
        if (!manual && DateTime.UtcNow < nextReconnectAttemptUtc) return;

        reconnecting = true;
        refreshTimer.Stop();
        deviceComboBox.Enabled = false;
        try
        {
            SetConnectionStatus("接続中...", "Connecting...", Color.Gainsboro);
            var devices = await Task.Run(HidDeviceClient.GetConnectedDevices);
            var desiredIdentity = requestedIdentity ?? preferredDeviceIdentity;
            UpdateDeviceSelector(devices, desiredIdentity);

            var target = desiredIdentity is null
                ? null
                : devices.FirstOrDefault(item =>
                    string.Equals(item.Identity, desiredIdentity, StringComparison.OrdinalIgnoreCase));

            if (target is null && desiredIdentity is not null && !manual)
            {
                var serial = SerialFromIdentity(desiredIdentity);
                throw new IOException(T(
                    $"選択中のコントローラ (Serial: {serial}) が見つかりません。",
                    $"The selected controller (Serial: {serial}) was not found."));
            }

            target ??= deviceComboBox.SelectedItem as HidDeviceDescriptor;
            target ??= devices.FirstOrDefault();
            if (target is null)
                throw new IOException("Vendor HIDが見つかりません。");

            preferredDeviceIdentity = target.Identity;
            UpdateDeviceSelector(devices, target.Identity);
            await Task.Run(() => client.Connect(target));

            var connectedSuffix = target.HasSerialNumber
                ? $" — Serial: {target.SerialNumber}"
                : string.Empty;
            SetConnectionStatus(
                "● 水冷ファンコントローラ 接続済み",
                "● Water Cooling Device connected",
                Color.LightGreen,
                connectedSuffix,
                connectedSuffix);
            Text = "Water Cooling Device with Dynamic Lighting" +
                (target.HasSerialNumber ? $" — {ShortSerial(target.SerialNumber)}" : string.Empty);
            nextReconnectAttemptUtc = DateTime.MinValue;
            nextDeviceScanUtc = DateTime.UtcNow.AddSeconds(3);
            await LoadTablesAsync();
            lastSettingsVersion = await client.QueryAsync(HidDeviceClient.GetSettingsVersion, 0);
            await ApplyPumpSettingsAsync();
            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            client.Disconnect();
            nextReconnectAttemptUtc = DateTime.UtcNow.AddSeconds(3);
            SetConnectionStatus(
                "● 未接続",
                "● Disconnected",
                Color.Salmon,
                $": {ErrorText(ex, false)}",
                $": {ErrorText(ex, true)}");
        }
        finally
        {
            reconnecting = false;
            deviceComboBox.Enabled = deviceSelectorPanel.Visible;
            refreshTimer.Start();
        }
    }

    private async Task DeviceSelectionChangedAsync()
    {
        if (updatingDeviceSelector || reconnecting ||
            deviceComboBox.SelectedItem is not HidDeviceDescriptor selected)
            return;
        if (string.Equals(selected.Identity, client.ConnectedIdentity,
                StringComparison.OrdinalIgnoreCase))
            return;

        preferredDeviceIdentity = selected.Identity;
        if (busy)
        {
            pendingDeviceIdentity = selected.Identity;
            return;
        }
        await ConnectAsync(true, selected.Identity);
    }

    private async Task RefreshDeviceSelectorAsync()
    {
        nextDeviceScanUtc = DateTime.UtcNow.AddSeconds(3);
        try
        {
            var devices = await Task.Run(HidDeviceClient.GetConnectedDevices);
            UpdateDeviceSelector(devices, preferredDeviceIdentity ?? client.ConnectedIdentity);
        }
        catch
        {
            // Device enumeration is retried on the next scan; active HID communication continues.
        }
    }

    private void UpdateDeviceSelector(
        IReadOnlyList<HidDeviceDescriptor> devices, string? selectedIdentity)
    {
        updatingDeviceSelector = true;
        try
        {
            var signature = string.Join("\u001F", devices.Select(item =>
                $"{item.Identity}\u001E{item.ProductName}\u001E{item.SerialNumber}"));
            if (!string.Equals(signature, lastDeviceListSignature, StringComparison.Ordinal))
            {
                deviceComboBox.BeginUpdate();
                try
                {
                    deviceComboBox.Items.Clear();
                    foreach (var item in devices) deviceComboBox.Items.Add(item);
                    lastDeviceListSignature = signature;
                }
                finally
                {
                    deviceComboBox.EndUpdate();
                }
            }

            var selected = deviceComboBox.Items.Cast<HidDeviceDescriptor>()
                .FirstOrDefault(item => string.Equals(item.Identity, selectedIdentity,
                    StringComparison.OrdinalIgnoreCase));
            var current = deviceComboBox.SelectedItem as HidDeviceDescriptor;
            if (selected is not null)
            {
                if (current is null || !string.Equals(current.Identity, selected.Identity,
                        StringComparison.OrdinalIgnoreCase))
                    deviceComboBox.SelectedItem = selected;
            }
            else if (deviceComboBox.Items.Count > 0) deviceComboBox.SelectedIndex = 0;

            var showSelector = devices.Count >= 2;
            if (deviceSelectorPanel.Visible != showSelector)
                deviceSelectorPanel.Visible = showSelector;
            var selectorHeight = showSelector ? 46 : 0;
            if (Math.Abs(deviceSelectorRowStyle.Height - selectorHeight) > 0.1f)
                deviceSelectorRowStyle.Height = selectorHeight;
            var shouldEnable = showSelector && !reconnecting;
            if (deviceComboBox.Enabled != shouldEnable)
                deviceComboBox.Enabled = shouldEnable;
        }
        finally
        {
            updatingDeviceSelector = false;
        }
    }

    private static string SerialFromIdentity(string identity) =>
        identity.StartsWith("serial:", StringComparison.OrdinalIgnoreCase)
            ? identity[7..]
            : "(unavailable)";

    private static string ShortSerial(string serialNumber) =>
        serialNumber.Length <= 8 ? serialNumber : serialNumber[^8..];

    private async Task RefreshStatusAsync()
    {
        if (busy || !client.IsConnected) return;
        busy = true;
        try
        {
            var tempX100 = await client.QueryAsync(HidDeviceClient.GetWaterTemperature, 0);
            var settingsVersion = await client.QueryAsync(HidDeviceClient.GetSettingsVersion, 0);
            if (lastSettingsVersion >= 0 && settingsVersion != lastSettingsVersion)
                await LoadTablesAsync();
            lastSettingsVersion = settingsVersion;
            var duty1 = await client.QueryAsync(HidDeviceClient.GetDuty, 0);
            var rpm1 = await client.QueryAsync(HidDeviceClient.GetFanRpm, 0);
            var duty2 = await client.QueryAsync(HidDeviceClient.GetDuty, 1);
            var rpm2 = await client.QueryAsync(HidDeviceClient.GetFanRpm, 1);
            var pumpStatus = pumpModeCheckBox.Checked
                ? await client.QueryAsync(HidDeviceClient.GetPumpStatus, 0) : 0;
            var sensorStatus = await client.QueryAsync(HidDeviceClient.GetSensorStatus, 0);
            var temperature = tempX100 / 100f;
            currentTemperature = temperature;

            sensorFault = sensorStatus != 0 || temperature < -20f || temperature > 60f;
            if (sensorFault) {
                temperatureLabel.Font = temperatureFaultFont;
                temperatureLabel.AutoSize = false;
                temperatureLabel.Dock = DockStyle.Fill;
                temperatureLabel.TextAlign = ContentAlignment.MiddleCenter;
                temperatureLabel.Text = T("センサー\n故障", "Sensor\nFault");
                temperatureLabel.ForeColor = Color.Salmon;
                warningAlarmActive = false;
                failSafeRowStyle.Height = 46;
                failSafeStatusLabel.Visible = true;
            } else {
                failSafeStatusLabel.Visible = false;
                failSafeRowStyle.Height = 0;
                temperatureLabel.Font = temperatureNormalFont;
                temperatureLabel.AutoSize = false;
                temperatureLabel.Dock = DockStyle.Fill;
                temperatureLabel.TextAlign = ContentAlignment.MiddleCenter;
                temperatureLabel.Text = $"{temperature:F2} °C";
                temperatureLabel.ForeColor = TemperatureColor(
                    temperature, (float)warningEditor.Value, lowColor, warningColor);
                UpdateWarningAlarm(temperature);
            }
            UpdateTrayTemperature();
            duty1Label.Text = $"{duty1} %";
            rpm1Label.Text = $"{rpm1} rpm";
            duty2Label.Text = $"{duty2} %";
            rpm2Label.Text = $"{rpm2} rpm";
            var pumpFault = pumpModeCheckBox.Checked && (pumpStatus & 2) != 0;
            pumpErrorStatusLabel.Visible = pumpFault;
            pumpErrorRowStyle.Height = pumpFault ? 46 : 0;
            graph.SetCurrent(sensorFault ? float.NaN : temperature, duty1, duty2);
        }
        catch (Exception ex)
        {
            client.Disconnect();
            nextReconnectAttemptUtc = DateTime.UtcNow.AddSeconds(3);
            SetConnectionStatus(
                "● 通信エラー",
                "● Communication error",
                Color.Salmon,
                $": {ErrorText(ex, false)}",
                $": {ErrorText(ex, true)}");
            temperatureLabel.Text = T("再接続待機中", "Waiting to reconnect");
            temperatureLabel.ForeColor = Color.Salmon;
            failSafeStatusLabel.Visible = false;
            failSafeRowStyle.Height = 0;
            pumpErrorStatusLabel.Visible = false;
            pumpErrorRowStyle.Height = 0;
        }
        finally {
            busy = false;
            if (pendingDeviceIdentity is not null && !reconnecting)
            {
                var requestedIdentity = pendingDeviceIdentity;
                pendingDeviceIdentity = null;
                BeginInvoke(new Action(async () =>
                    await ConnectAsync(true, requestedIdentity)));
            }
        }
    }

    private async Task LoadTablesAsync()
    {
        if (!client.IsConnected) return;
        var lockSelectorForManualReload = !busy && !reconnecting;
        saveButton.Enabled = reloadButton.Enabled = false;
        if (lockSelectorForManualReload) deviceComboBox.Enabled = false;
        try
        {
            duty1Table = await client.ReadTableAsync(true);
            duty2Table = await client.ReadTableAsync(false);
            var warningTemperature = await client.QueryAsync(
                HidDeviceClient.GetWarningTemperature, 0);
            updatingEditors = true;
            for (var i = 0; i < 5; i++) {
                duty1Editors[i].Value = duty1Table[i];
                duty2Editors[i].Value = duty2Table[i];
            }
            updatingEditors = false;
            warningEditor.Value = Math.Clamp(warningTemperature,
                (int)warningEditor.Minimum, (int)warningEditor.Maximum);
            graph.SetTables(duty1Table, duty2Table);
            editGraph.SetTables(duty1Table, duty2Table);
        }
        finally {
            saveButton.Enabled = reloadButton.Enabled = true;
            if (lockSelectorForManualReload)
                deviceComboBox.Enabled = deviceSelectorPanel.Visible && !reconnecting;
        }
    }

    private async Task SaveTablesAsync()
    {
        if (!client.IsConnected) return;
        refreshTimer.Stop();
        saveButton.Enabled = reloadButton.Enabled = false;
        deviceComboBox.Enabled = false;
        try
        {
            duty1Table = duty1Editors.Select(item => (int)item.Value).ToArray();
            duty2Table = duty2Editors.Select(item => (int)item.Value).ToArray();
            await client.WriteTableAsync(true, duty1Table);
            await client.WriteTableAsync(false, duty2Table);
            var warning = (int)warningEditor.Value;
            var acknowledgedWarning = await client.QueryAsync(
                HidDeviceClient.SetWarningTemperature, 0, warning);
            if (acknowledgedWarning != warning)
                throw new IOException("警告温度の確認値が一致しません。");
            graph.SetTables(duty1Table, duty2Table);
            editGraph.SetTables(duty1Table, duty2Table);
            await Task.Delay(600);
            MessageBox.Show(this,
                T("ファンカーブと警告温度を保存しました。",
                  "Fan curves and warning temperature have been saved."),
                T("保存完了", "Saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ErrorText(ex), T("保存エラー", "Save error"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally {
            saveButton.Enabled = reloadButton.Enabled = true;
            deviceComboBox.Enabled = deviceSelectorPanel.Visible && !reconnecting && !busy;
            refreshTimer.Start();
        }
    }

    private void EditorValueChanged(int fan, int point)
    {
        if (updatingEditors) return;
        if (fan == 1) duty1Table[point] = (int)duty1Editors[point].Value;
        else duty2Table[point] = (int)duty2Editors[point].Value;
        editGraph.SetTables(duty1Table, duty2Table);
    }

    private void ResetEditorsToDefaults()
    {
        var defaults = new[] { 10, 20, 25, 40, 70 };
        updatingEditors = true;
        for (var i = 0; i < 5; i++) {
            duty1Editors[i].Value = defaults[i];
            duty2Editors[i].Value = defaults[i];
        }
        updatingEditors = false;
        duty1Table = (int[])defaults.Clone();
        duty2Table = (int[])defaults.Clone();
        warningEditor.Value = 55;
        editGraph.SetTables(duty1Table, duty2Table);
    }

    private void CopyFan1ToFan2()
    {
        updatingEditors = true;
        for (var i = 0; i < duty1Editors.Length; i++)
            duty2Editors[i].Value = duty1Editors[i].Value;
        updatingEditors = false;

        duty1Table = duty1Editors.Select(editor => (int)editor.Value).ToArray();
        duty2Table = (int[])duty1Table.Clone();
        editGraph.SetTables(duty1Table, duty2Table);
    }

    private async Task PumpSettingChangedAsync()
    {
        appSettings.Fan2PumpMode = pumpModeCheckBox.Checked;
        appSettings.PumpDuty = (int)pumpDutyEditor.Value;
        appSettings.Save();
        UpdatePumpUi();
        if (!client.IsConnected) return;
        deviceComboBox.Enabled = false;
        try { await ApplyPumpSettingsAsync(); }
        catch (Exception ex) {
            SetConnectionStatus(
                "● PUMP設定エラー",
                "● PUMP setting error",
                Color.Salmon,
                $": {ErrorText(ex, false)}",
                $": {ErrorText(ex, true)}");
        }
        finally {
            deviceComboBox.Enabled = deviceSelectorPanel.Visible && !reconnecting && !busy;
        }
    }

    private async Task ApplyPumpSettingsAsync() =>
        await client.ConfigurePumpAsync(pumpModeCheckBox.Checked, (int)pumpDutyEditor.Value);

    private void UpdatePumpUi()
    {
        var pump = pumpModeCheckBox.Checked;
        fan2DutyTitle.Text = pump ? T("PUMP 出力", "PUMP Output") : T("ファン2 出力", "Fan 2 Output");
        fan2RpmTitle.Text = pump ? T("PUMP RPM", "PUMP Speed") : T("RPM 2", "Fan 2 Speed");
        foreach (var editor in duty2Editors) if (editor is not null) editor.Enabled = !pump;
        copyFan1ToFan2Button.Enabled = !pump;
        graph.SetPumpMode(pump, (int)pumpDutyEditor.Value);
        if (!pump) {
            pumpErrorStatusLabel.Visible = false;
            pumpErrorRowStyle.Height = 0;
        }
    }

    private static TabPage NewTab(string text) => new(text) {
        BackColor = Color.FromArgb(30, 33, 39), ForeColor = Color.Gainsboro
    };
    private static Label MakeValueLabel(string text, float size) => new() {
        Text = text, AutoSize = true, Font = new Font("Segoe UI Semibold", size),
        ForeColor = Color.White, Padding = new Padding(6)
    };
    private static Label MakeHeader(string text) => new() {
        Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        AutoSize = true, Padding = new Padding(8), ForeColor = Color.Gainsboro
    };
    private static NumericUpDown MakeEditor() => new() {
        Minimum = 0, Maximum = 100, Increment = 1, Dock = DockStyle.Fill,
        TextAlign = HorizontalAlignment.Center, Font = new Font("Segoe UI", 12),
        Margin = new Padding(10)
    };
    private static Control MakeCard(string title, Label value) =>
        MakeCard(new Label { Text = title, AutoSize = true, ForeColor = Color.Silver }, value);
    private static Control MakeCard(Label title, Label value)
    {
        var panel = new TableLayoutPanel {
            Dock = DockStyle.Fill, RowCount = 2, Margin = new Padding(5),
            BackColor = Color.FromArgb(42, 46, 54), Padding = new Padding(8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(value, 0, 1);
        return panel;
    }

    private string T(string japanese, string english) => isEnglish ? english : japanese;

    private void SetConnectionStatus(
        string japanese, string english, Color color,
        string japaneseDetail = "", string englishDetail = "")
    {
        connectionStatusJapanese = japanese;
        connectionStatusEnglish = english;
        connectionDetailJapanese = japaneseDetail;
        connectionDetailEnglish = englishDetail;
        connectionStatusColor = color;
        RefreshConnectionStatusDisplay();
    }

    private void RefreshConnectionStatusDisplay()
    {
        connectionLabel.Text = isEnglish
            ? connectionStatusEnglish + connectionDetailEnglish
            : connectionStatusJapanese + connectionDetailJapanese;
        connectionLabel.ForeColor = connectionStatusColor;
    }

    private static Color TemperatureColor(float temperature, float warningTemperature,
                                          Color lowEndpoint, Color highEndpoint)
    {
        var upper = Math.Max(20.1f, warningTemperature);
        var ratio = Math.Clamp((temperature - 20f) / (upper - 20f), 0f, 1f);

        // Cool blue at/below 20°C, transitioning continuously to warning red.
        var red = (int)Math.Round(lowEndpoint.R + (highEndpoint.R - lowEndpoint.R) * ratio);
        var green = (int)Math.Round(lowEndpoint.G + (highEndpoint.G - lowEndpoint.G) * ratio);
        var blue = (int)Math.Round(lowEndpoint.B + (highEndpoint.B - lowEndpoint.B) * ratio);
        return Color.FromArgb(red, green, blue);
    }

    private void ChooseWarningColor()
    {
        using var dialog = new ColorDialog { Color = warningColor, FullOpen = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        warningColor = dialog.Color;
        warningColorPreview.BackColor = warningColor;
        appSettings.WarningColorArgb = warningColor.ToArgb();
        appSettings.Save();
        temperatureLabel.ForeColor = sensorFault ? Color.Salmon : TemperatureColor(
            currentTemperature, (float)warningEditor.Value, lowColor, warningColor);
        UpdateTrayTemperature();
    }

    private void ChooseLowColor()
    {
        using var dialog = new ColorDialog { Color = lowColor, FullOpen = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        lowColor = dialog.Color;
        lowColorPreview.BackColor = lowColor;
        appSettings.LowColorArgb = lowColor.ToArgb();
        appSettings.Save();
        temperatureLabel.ForeColor = sensorFault ? Color.Salmon : TemperatureColor(
            currentTemperature, (float)warningEditor.Value, lowColor, warningColor);
        UpdateTrayTemperature();
    }

    private void UpdateWarningAlarm(float temperature)
    {
        var threshold = (float)warningEditor.Value;
        if (temperature >= threshold) {
            if (warningBeepCheckBox.Checked && !warningAlarmActive)
                System.Media.SystemSounds.Exclamation.Play();
            warningAlarmActive = true;
        } else if (temperature <= threshold - 1f) {
            warningAlarmActive = false;
        }
    }

    private void ConfigureTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Exit", null, (_, _) => Close());
        trayIcon.ContextMenuStrip = menu;
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        trayIcon.Icon = SystemIcons.Application;
    }

    private void MinimizeToTrayIfNeeded()
    {
        if (WindowState != FormWindowState.Minimized) return;
        UpdateTrayTemperature();
        trayIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
        trayIcon.Visible = false;
    }

    private void UpdateTrayTemperature()
    {
        var color = sensorFault ? Color.Firebrick : TemperatureColor(currentTemperature,
            (float)warningEditor.Value, lowColor, warningColor);
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap)) {
            graphics.Clear(color);
            var text = sensorFault ? "!" : Math.Round(currentTemperature).ToString("0");
            var fontSize = text.Length <= 2 ? 23f : 18f;
            using var font = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var textColor = color.GetBrightness() > 0.55f ? Color.Black : Color.White;
            TextRenderer.DrawText(graphics, text, font, new Rectangle(0, 0, 32, 32), textColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding);
        }
        var handle = bitmap.GetHicon();
        var nextIcon = (Icon)Icon.FromHandle(handle).Clone();
        NativeMethods.DestroyIcon(handle);
        var previous = generatedTrayIcon;
        generatedTrayIcon = nextIcon;
        trayIcon.Icon = nextIcon;
        trayIcon.Text = sensorFault
            ? T("水温センサー故障", "Coolant sensor fault")
            : $"Coolant: {currentTemperature:F2} °C";
        previous?.Dispose();
    }

    private void ApplyLanguage(Control root)
    {
        foreach (Control control in root.Controls) {
            var pair = JapaneseToEnglish.FirstOrDefault(item =>
                control.Text == item.Key || control.Text == item.Value);
            if (!string.IsNullOrEmpty(pair.Key)) control.Text = isEnglish ? pair.Value : pair.Key;
            if (control.HasChildren) ApplyLanguage(control);
        }
        UpdatePumpUi();
    }

    private string ErrorText(Exception ex) => ErrorText(ex, isEnglish);

    private string ErrorText(Exception ex, bool english)
    {
        if (!english) return ex.Message;

        if (ex is DeviceInUseException)
            return "The selected controller is in use by another application or cannot be accessed.";

        if (ex is TimeoutException)
            return "The water cooling controller did not respond.";

        if (ex.Message.StartsWith("The selected controller", StringComparison.Ordinal))
            return ex.Message;

        if (ex.Message.Contains("Vendor HIDが見つかりません", StringComparison.Ordinal))
            return "The vendor HID interface was not found.";

        if (ex.Message.Contains("接続されていません", StringComparison.Ordinal))
            return "The water cooling controller is not connected.";

        if (ex.Message.Contains("警告温度の確認値", StringComparison.Ordinal))
            return "The warning-temperature verification value did not match.";

        if (ex.Message.StartsWith("Point ", StringComparison.Ordinal) &&
            ex.Message.Contains("確認値が一致しません", StringComparison.Ordinal))
            return ex.Message.Replace("の確認値が一致しません。",
                " verification value did not match.", StringComparison.Ordinal);

        if (ex is IOException)
            return $"USB HID communication failed. (0x{ex.HResult:X8})";

        return $"An unexpected error occurred. (0x{ex.HResult:X8})";
    }
}
