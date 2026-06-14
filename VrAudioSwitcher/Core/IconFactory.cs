using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace VrAudioSwitcher.Core;

/// <summary>Generates a simple "VR" badge icon/bitmap so the app needs no asset file.</summary>
public static class IconFactory
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <summary>Free an icon created via <see cref="CreateIcon"/> (GetHicon leaks otherwise).</summary>
    public static void DestroyHandle(Icon? icon)
    {
        if (icon == null) return;
        DestroyIcon(icon.Handle);
        icon.Dispose();
    }

    public static Bitmap CreateBitmap(int size, bool vrActive)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var bg = vrActive ? Color.FromArgb(46, 160, 67) : Color.FromArgb(56, 96, 160);
        using var brush = new SolidBrush(bg);
        g.FillEllipse(brush, 1, 1, size - 2, size - 2);

        using var font = new Font("Segoe UI", size * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var fg = new SolidBrush(Color.White);
        var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("VR", font, fg, new RectangleF(0, 0, size, size), fmt);
        return bmp;
    }

    public static Icon CreateIcon(bool vrActive)
    {
        using var bmp = CreateBitmap(32, vrActive);
        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>
    /// Write a PNG-compressed .ico (256×256) to disk — used once at design time to
    /// produce the embedded application icon (Resources/app.ico).
    /// </summary>
    public static void SaveAppIcon(string path)
    {
        using var bmp = CreateBitmap(256, vrActive: false);
        using var pngMs = new MemoryStream();
        bmp.Save(pngMs, ImageFormat.Png);
        var png = pngMs.ToArray();

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        // ICONDIR
        bw.Write((short)0);   // reserved
        bw.Write((short)1);   // type: icon
        bw.Write((short)1);   // image count
        // ICONDIRENTRY
        bw.Write((byte)0);    // width 0 => 256
        bw.Write((byte)0);    // height 0 => 256
        bw.Write((byte)0);    // palette
        bw.Write((byte)0);    // reserved
        bw.Write((short)1);   // color planes
        bw.Write((short)32);  // bits per pixel
        bw.Write(png.Length); // size of PNG data
        bw.Write(22);         // offset (6 + 16)
        bw.Write(png);
    }
}
