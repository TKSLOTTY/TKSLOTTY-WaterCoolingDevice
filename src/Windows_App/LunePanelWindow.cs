namespace WaterCoolingDevice;

internal sealed class LunePanelWindow : Form
{
    public event EventHandler? PositionSettled;

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        PositionSettled?.Invoke(this, EventArgs.Empty);
    }

    private const int WmNcHitTest = 0x0084;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int ResizeGrip = 7;

    private readonly LuneForm builtInScene = new();
    private LunePanelTheme mode;
    private LuneThemeDocument? customTheme;
    private LuneThemeRenderer? customRenderer;
    private LuneTelemetrySnapshot telemetry;
    private int animationFrame;
    private bool dragging;
    private Point dragCursorStart;
    private Point dragWindowStart;
    private readonly System.Windows.Forms.Timer timer = new();

    public LunePanelWindow(LunePanelTheme mode, bool landscape, LuneThemeDocument? customTheme)
    {
        this.mode = mode;
        telemetry = new(null, null, null, 55, false, false, false, Color.Cyan, Color.DeepPink);
        ApplyTheme(mode, landscape, customTheme);
        Text = "LUNE Panel";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Black;
        DoubleBuffered = true;
        MinimumSize = new Size(240, 180);
        KeyPreview = true;
        ShowInTaskbar = true;
        BuildContextMenu();
        timer.Tick += (_, _) => { animationFrame++; Invalidate(); };
        timer.Start();
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        FormClosed += (_, _) => { timer.Dispose(); customRenderer?.Dispose(); builtInScene.Dispose(); };
    }

    public void ApplyTheme(LunePanelTheme nextMode, bool landscape, LuneThemeDocument? nextCustom)
    {
        mode = nextMode; customTheme = nextCustom;
        customRenderer?.Dispose(); customRenderer = nextCustom is null ? null : new LuneThemeRenderer(nextCustom);
        var width = nextCustom?.Width ?? (landscape ? 480 : 320);
        var height = nextCustom?.Height ?? (landscape ? 320 : 480);
        ClientSize = new Size(width, height);
        timer.Interval = nextMode == LunePanelTheme.Cyber
            ? 700
            : nextCustom?.AnimationFrameIntervalMs() ?? 1000;
        animationFrame = 0; Invalidate();
    }

    public void UpdateTelemetry(LuneTelemetrySnapshot snapshot)
    {
        telemetry = snapshot; builtInScene.UpdateTelemetry(snapshot); Invalidate();
    }

    public void RefreshThemeLayout()
    {
        if (mode == LunePanelTheme.Custom && customTheme is not null)
            timer.Interval = customTheme.AnimationFrameIntervalMs();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var image = mode switch {
            LunePanelTheme.Cyber => builtInScene.RenderLunePanelCyber(
                ClientSize.Width >= ClientSize.Height, animationFrame),
            LunePanelTheme.Custom when customRenderer is not null => customRenderer.Render(
                telemetry, animationFrame, ClientSize),
            _ => builtInScene.RenderLunePanel(ClientSize.Width >= ClientSize.Height, false)
        };
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(image, new Rectangle(Point.Empty, ClientSize));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || IsOnResizeEdge(e.Location)) return;
        dragging = true;
        dragCursorStart = Cursor.Position;
        dragWindowStart = Location;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging || e.Button != MouseButtons.Left) return;
        var cursor = Cursor.Position;
        Location = new Point(dragWindowStart.X + cursor.X - dragCursorStart.X,
            dragWindowStart.Y + cursor.Y - dragCursorStart.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        dragging = false;
        Capture = false;
        PositionSettled?.Invoke(this, EventArgs.Empty);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmNcHitTest && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref message);
            if ((int)message.Result != 1) return;

            var cursor = PointToClient(Cursor.Position);
            var left = cursor.X < ResizeGrip;
            var right = cursor.X >= ClientSize.Width - ResizeGrip;
            var top = cursor.Y < ResizeGrip;
            var bottom = cursor.Y >= ClientSize.Height - ResizeGrip;
            message.Result = (nint)((left, right, top, bottom) switch {
                (true, _, true, _) => HtTopLeft,
                (_, true, true, _) => HtTopRight,
                (true, _, _, true) => HtBottomLeft,
                (_, true, _, true) => HtBottomRight,
                (true, _, _, _) => HtLeft,
                (_, true, _, _) => HtRight,
                (_, _, true, _) => HtTop,
                (_, _, _, true) => HtBottom,
                _ => 1
            });
            return;
        }
        base.WndProc(ref message);
    }

    private bool IsOnResizeEdge(Point point) =>
        point.X < ResizeGrip || point.X >= ClientSize.Width - ResizeGrip ||
        point.Y < ResizeGrip || point.Y >= ClientSize.Height - ResizeGrip;

    private void BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("実寸に戻す", null, (_, _) =>
            ClientSize = new Size(customTheme?.Width ?? (ClientSize.Width >= ClientSize.Height ? 480 : 320),
                                  customTheme?.Height ?? (ClientSize.Width >= ClientSize.Height ? 320 : 480)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("閉じる  (Esc)", null, (_, _) => Close());
        ContextMenuStrip = menu;
    }

}
