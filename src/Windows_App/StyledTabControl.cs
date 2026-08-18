using System.Drawing.Drawing2D;

namespace WaterCoolingDevice;

internal sealed class StyledTabControl : TabControl
{
    public StyledTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(180, 38);
        Padding = new Point(18, 6);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var selected = e.Index == SelectedIndex;
        var bounds = GetTabRect(e.Index);
        bounds.Inflate(-2, selected ? 0 : -2);

        var top = selected ? Color.FromArgb(82, 89, 103) : Color.FromArgb(48, 52, 61);
        var bottom = selected ? Color.FromArgb(48, 53, 63) : Color.FromArgb(34, 37, 44);
        using var path = RoundedTop(bounds, 7);
        using var brush = new LinearGradientBrush(bounds, top, bottom, LinearGradientMode.Vertical);
        using var border = new Pen(selected ? Color.FromArgb(150, 160, 178) : Color.FromArgb(70, 76, 88));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(border, path);

        TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, bounds,
            selected ? Color.White : Color.Silver,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath RoundedTop(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddLine(r.Right, r.Bottom, r.Left, r.Bottom);
        path.CloseFigure();
        return path;
    }
}
