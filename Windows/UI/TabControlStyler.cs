using System.Drawing;
using System.Windows.Forms;
using PocketOSManager.UI;

namespace PocketOSManager.UI;

public static class TabControlStyler
{
    public static void Apply(TabControl tabs)
    {
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.ItemSize = new Size(135, 38);
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.Font = new Font("Consolas", 10, FontStyle.Bold);

        tabs.DrawItem += (_, e) =>
        {
            bool selected = e.Index == tabs.SelectedIndex;

            using var background = new SolidBrush(
                selected
                    ? Color.FromArgb(32, 65, 42)
                    : Color.FromArgb(18, 21, 23));

            using var foreground = new SolidBrush(
                selected
                    ? Color.FromArgb(218, 248, 225)
                    : Color.LightGray);

            e.Graphics.FillRectangle(
                background,
                e.Bounds);

            e.Graphics.DrawString(
                tabs.TabPages[e.Index].Text,
                tabs.Font,
                foreground,
                e.Bounds.X + 10,
                e.Bounds.Y + 10);
        };
    }
}
