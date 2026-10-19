using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MultiRoblox;

/// <summary>Draws the tray/window icon at runtime (two stacked windows on a rounded tile), so the repo needs no .ico file.</summary>
internal static class AppIcons
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create(Color tile)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using (var path = RoundedRect(new Rectangle(1, 1, 30, 30), 8))
            using (var brush = new SolidBrush(tile))
            {
                g.FillPath(brush, path);
            }

            using var pen = new Pen(Color.White, 2.5f);
            using var white = new SolidBrush(Color.White);
            g.DrawRectangle(pen, 7, 7, 12, 12);
            g.FillRectangle(white, 13, 13, 12, 12);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
