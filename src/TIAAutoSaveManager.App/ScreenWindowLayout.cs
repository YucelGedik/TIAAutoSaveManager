using System;
using System.Windows;

namespace TIAAutoSaveManager.App;

public sealed record ScreenWindowPlacement(Size Size, Size MinimumSize, Point PositionPixels);

public static class ScreenWindowLayout
{
    // Monitor work areas are physical pixels; WPF window sizes are device-independent units.
    public static ScreenWindowPlacement Calculate(Rect workAreaPixels, DpiScale dpi, Size preferredSize, Point? currentPositionPixels = null)
    {
        if (workAreaPixels.IsEmpty || workAreaPixels.Width <= 0 || workAreaPixels.Height <= 0 ||
            dpi.DpiScaleX <= 0 || dpi.DpiScaleY <= 0) throw new ArgumentException("Invalid screen work area or DPI.");
        double marginX = Math.Min(16, workAreaPixels.Width / dpi.DpiScaleX / 4);
        double marginY = Math.Min(16, workAreaPixels.Height / dpi.DpiScaleY / 4);
        double width = Math.Min(preferredSize.Width, workAreaPixels.Width / dpi.DpiScaleX - marginX * 2);
        double height = Math.Min(preferredSize.Height, workAreaPixels.Height / dpi.DpiScaleY - marginY * 2);
        double left = workAreaPixels.Left + marginX * dpi.DpiScaleX;
        double top = workAreaPixels.Top + marginY * dpi.DpiScaleY;
        double right = workAreaPixels.Right - (width + marginX) * dpi.DpiScaleX;
        double bottom = workAreaPixels.Bottom - (height + marginY) * dpi.DpiScaleY;
        var position = currentPositionPixels == null
            ? new Point((left + right) / 2, (top + bottom) / 2)
            : new Point(Math.Clamp(currentPositionPixels.Value.X, left, Math.Max(left, right)),
                Math.Clamp(currentPositionPixels.Value.Y, top, Math.Max(top, bottom)));
        return new(new Size(width, height), new Size(Math.Min(760, width), Math.Min(520, height)), position);
    }
}
