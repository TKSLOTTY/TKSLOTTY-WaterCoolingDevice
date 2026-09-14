namespace WaterCoolingDevice;

internal sealed partial class MainForm
{
    private bool studioTestMode;
    private readonly StyledTabControl mainTabs = new() { Dock = DockStyle.Fill };
    private TabPage? studioPage;
    private readonly CheckBox studioEnabled = new() { AutoSize = true };
    private readonly Label studioDescription = new() { AutoSize = true, MaximumSize = new Size(480, 0) };
    private readonly CheckBox desktopAutoShow = new() { AutoSize = true };
    private readonly FlowLayoutPanel themeGallery = new() { Width = 730, Height = 130, AutoScroll = true, WrapContents = false };

    private Control BuildStudioSetting()
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        panel.Controls.Add(studioEnabled);
        panel.Controls.Add(studioDescription);
        return panel;
    }

    private void InitializeStudio()
    {
        studioEnabled.Checked = lunePanelOptions.StudioEnabled;
        desktopAutoShow.Checked = lunePanelOptions.DesktopAutoShow;
        studioEnabled.CheckedChanged += (_, _) => {
            lunePanelOptions = lunePanelOptions with { StudioEnabled = studioEnabled.Checked };
            if (!studioTestMode) lunePanelOptions.Save();
            UpdateStudioVisibility();
            if (studioEnabled.Checked) ApplyLunePanelOptions();
        };
        desktopAutoShow.CheckedChanged += (_, _) => {
            lunePanelOptions = lunePanelOptions with { DesktopAutoShow = desktopAutoShow.Checked };
            if (!studioTestMode) lunePanelOptions.Save();
        };
        UpdateStudioVisibility();
        if (studioEnabled.Checked) ApplyLunePanelOptions();
    }

    private void UpdateStudioVisibility()
    {
        if (studioPage is null) return;
        if (studioEnabled.Checked) {
            if (!mainTabs.TabPages.Contains(studioPage)) mainTabs.TabPages.Add(studioPage);
            ApplyLanguage(studioPage);
            UpdateLunePanelLanguage();
        } else {
            mainTabs.TabPages.Remove(studioPage);
            lunePanelTimer.Stop();
            lunePanel?.Configure(lunePanelOptions with { Enabled = false });
            if (lunePanelWindow is { IsDisposed: false }) lunePanelWindow.Close();
            luneForm?.Hide();
        }
        luneViewButton.Visible = studioEnabled.Checked;
    }

    private void UpdateStudioLanguage()
    {
        studioEnabled.Text = T("LUNE Studioを有効にする", "Enable LUNE Studio");
        studioDescription.Text = T("テーマを使ったモニター画面を、デスクトップや対応USB液晶に表示できます。", "Display themed monitors on your desktop or a compatible USB display.");
        desktopAutoShow.Text = T("次回起動時にもデスクトップに表示", "Show on desktop when the app starts");
        lunePanelEnabled.Text = T("USB液晶にも表示", "Display on USB monitor");
        if (studioPage is not null) studioPage.Text = "LUNE Studio";
    }

    private void RebuildThemeGallery()
    {
        foreach (Control control in themeGallery.Controls) {
            if (control is Button button) button.Image?.Dispose();
        }
        while (themeGallery.Controls.Count > 0) themeGallery.Controls[0].Dispose();
        foreach (ThemeChoice choice in lunePanelTheme.Items) {
            try {
                using var scene = new LuneForm();
                using var frame = choice.Theme == LunePanelTheme.Custom
                    ? RenderCustomThumbnail(choice.File)
                    : choice.Theme == LunePanelTheme.Cyber ? scene.RenderLunePanelCyber(true) : scene.RenderLunePanel(true);
                var button = new Button { Width = 142, Height = 108, Text = choice.Name,
                    Image = new Bitmap(frame, new Size(120, 80)), TextImageRelation = TextImageRelation.ImageAboveText };
                button.Click += (_, _) => lunePanelTheme.SelectedItem = choice;
                themeGallery.Controls.Add(button);
            } catch { }
        }
    }

    private Bitmap RenderCustomThumbnail(string file)
    {
        using var renderer = new LuneThemeRenderer(LuneThemeStore.Load(file));
        return renderer.Render(CurrentLuneTelemetry());
    }

    internal void VerifyStudioFlow()
    {
        if (!studioTestMode) throw new InvalidOperationException("Test mode required");
        void Check(bool value) { if (!value) throw new InvalidOperationException("Studio check failed"); }
        Check(!studioEnabled.Checked && !mainTabs.TabPages.Contains(studioPage!));
        studioEnabled.Checked = true;
        Check(mainTabs.TabPages.Contains(studioPage!) && lunePanelWindow is null);
        desktopAutoShow.Checked = true;
        Check(lunePanelOptions.DesktopAutoShow && lunePanelWindow is null);
        foreach (var english in new[] { false, true }) {
            isEnglish = english;
            ApplyLanguage(this);
            UpdateLunePanelLanguage();
            Check(studioEnabled.Text == (english ? "Enable LUNE Studio" : "LUNE Studioを有効にする"));
            Check(desktopAutoShow.Text == (english ? "Show on desktop when the app starts" : "次回起動時にもデスクトップに表示"));
            mainTabs.SelectedTab = studioPage;
            Show(); Application.DoEvents();
            using var image = new Bitmap(Width, Height);
            DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(Path.Combine(AppContext.BaseDirectory, english ? "studio-en.png" : "studio-ja.png"));
            mainTabs.SelectedIndex = 3;
            Application.DoEvents();
            using var settingsImage = new Bitmap(Width, Height);
            DrawToBitmap(settingsImage, new Rectangle(Point.Empty, settingsImage.Size));
            settingsImage.Save(Path.Combine(AppContext.BaseDirectory, english ? "settings-en.png" : "settings-ja.png"));
        }
        ShowLunePanelWindow();
        Check(lunePanelWindow is { Visible: true });
        var selectedTheme = lunePanelOptions.ThemeFile;
        studioEnabled.Checked = false;
        Check(!mainTabs.TabPages.Contains(studioPage!) && lunePanelWindow is null);
        Check(lunePanelOptions.ThemeFile == selectedTheme && lunePanelOptions.DesktopAutoShow);
        Close();
    }
}
