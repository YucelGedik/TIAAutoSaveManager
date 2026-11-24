using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TIAAutoSaveManager.App;

// Application-owned notification; uses our theme without changing Windows notification settings.
public sealed class ThemedNotification : IDisposable
{
    private Window? current;
    private DispatcherTimer? timer;
    public void Show(string message, bool error, Action openManager)
    {
        Dispose();
        var window = new Window { Width = 370, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, ShowActivated = false, ShowInTaskbar = false, Topmost = true,
            Style = (Style)Application.Current.FindResource(typeof(Window)), Title = "TIA Auto Save Manager" };
        var border = new Border { BorderBrush = (Brush)Application.Current.FindResource("AccentBrush"), BorderThickness = new Thickness(1), Padding = new Thickness(18), Background = window.Background };
        var content = new StackPanel();
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,9) };
        heading.Children.Add(new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(BrandAssets.LogoUri)), Width = 28, Height = 28, Margin = new Thickness(0,0,10,0) });
        heading.Children.Add(new TextBlock { Text = error ? "İşlem tamamlanamadı" : "TIA Auto Save Manager", Foreground = (Brush)Application.Current.FindResource("AccentBrush"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        content.Children.Add(heading);
        content.Children.Add(new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 100, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), FontSize = 12 });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,12,0,0) };
        var open = new Button { Content = "Manager’ı aç", Padding = new Thickness(12,7,12,7), Margin = new Thickness(0,0,8,0) };
        var close = new Button { Content = "Kapat", Padding = new Thickness(12,7,12,7) };
        open.Click += (_, _) => { openManager(); Dispose(); }; close.Click += (_, _) => Dispose();
        actions.Children.Add(open); actions.Children.Add(close); content.Children.Add(actions); border.Child = content; window.Content = border;
        window.ContentRendered += (_, _) => { var area = SystemParameters.WorkArea; window.Left = area.Right - window.ActualWidth - 16; window.Top = area.Bottom - window.ActualHeight - 16; };
        current = window;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(error ? 10 : 6) };
        timer.Tick += (_, _) => Dispose();
        window.Show(); timer.Start();
    }
    public void Dispose() { timer?.Stop(); timer = null; var window = current; current = null; window?.Close(); }
}
