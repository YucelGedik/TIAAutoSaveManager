using System;
using System.Drawing;
using System.Windows;

namespace TIAAutoSaveManager.App;

public static class BrandAssets
{
    public const string LogoUri = "pack://application:,,,/TIAAutoSaveManager.App;component/Assets/Logo.png";
    public const string IconUri = "pack://application:,,,/TIAAutoSaveManager.App;component/Assets/App.ico";
    public static Icon CreateTrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri(IconUri))!.Stream;
        using var original = new Icon(stream,32,32);
        return (Icon)original.Clone();
    }
}
