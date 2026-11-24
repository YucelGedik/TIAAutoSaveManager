using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace TIAAutoSaveManager.App;

public interface IUserDialogService
{
    bool Confirm(string message, string title);
    void Notice(string message, string title, bool error = false);
}

public sealed class ThemedDialogService : IUserDialogService
{
    public static ThemedDialogService Shared { get; } = new();
    public bool Confirm(string message, string title) => Show(message, title, true, false);
    public void Notice(string message, string title, bool error = false) => Show(message, title, false, error);
    private static bool Show(string message, string title, bool confirmation, bool error)
    {
        var window = new ThemedDialog(message, title, confirmation, error);
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible);
        if (owner != null) { window.Owner = owner; window.FitToArea(owner.ActualWidth, owner.ActualHeight); }
        else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return window.ShowDialog() == true;
    }
}

public partial class ThemedDialog : Window
{
    public ThemedDialog(string message, string title, bool confirmation = true, bool error = false)
    {
        InitializeComponent();
        Title = title; Heading.Text = title; Message.Text = message;
        if (!confirmation) { PrimaryAction.Visibility = Visibility.Collapsed; CancelAction.Content = "Tamam"; Symbol.Text = error ? "!" : "i"; }
        var area = SystemParameters.WorkArea; FitToArea(area.Width, area.Height);
        ContentRendered += (_, _) => CancelAction.Focus();
    }
    public void FitToArea(double width, double height)
    {
        Width = Math.Min(600, Math.Max(320, width - 48)); MinWidth = Math.Min(360, Width);
        MaxHeight = Math.Max(240, height - 48);
        Message.MaxHeight = Math.Max(70, Math.Min(340, MaxHeight - 230));
    }
    public void RenderPreview(string path)
    {
        var root = (FrameworkElement)Content;
        root.SetValue(System.Windows.Documents.TextElement.FontSizeProperty, FontSize);
        root.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, FontFamily);
        root.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, Foreground);
        Content = null;
        var surface = new System.Windows.Controls.Border { Width = Width, Background = Background, Child = root };
        surface.Measure(new Size(Width, MaxHeight));
        int height = (int)Math.Ceiling(Math.Min(MaxHeight, surface.DesiredSize.Height));
        surface.Height = height;
        surface.Arrange(new Rect(0,0,Width,height)); surface.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var output = System.IO.File.Create(path); encoder.Save(output);
    }
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void CloseDialog(object sender, RoutedEventArgs e) => Close();
    private void AcceptDialog(object sender, RoutedEventArgs e) { DialogResult = true; }
}
