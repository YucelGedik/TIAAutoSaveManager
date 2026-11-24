using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace TIAAutoSaveManager.App;

public partial class ScreenshotWindow : Window
{
    public ScreenshotWindow(HelpStep step)
    {
        InitializeComponent(); Title = step.Title; Heading.Text = step.Title;
        Screenshot.Source = new BitmapImage(new Uri(step.ImageUri));
        var area = SystemParameters.WorkArea; Width = Math.Min(1100,area.Width-48); Height = Math.Min(850,area.Height-48);
        MinWidth = Math.Min(600,Width); MinHeight = Math.Min(400,Height);
        Loaded += (_,_) => Fit(); PreviewKeyDown += (_,e) => { if(e.Key == Key.Escape) Close(); };
    }
    private void Fit() => Zoom.Value = Math.Clamp((ImageViewport.ActualWidth-24)/Screenshot.Source.Width,0.25,1);
    private void FitImage(object sender,RoutedEventArgs e) => Fit();
    private void ActualSize(object sender,RoutedEventArgs e) => Zoom.Value=1;
    private void CloseImage(object sender,RoutedEventArgs e) => Close();
    private void DragHeader(object sender,MouseButtonEventArgs e) { if(e.ChangedButton==MouseButton.Left && e.LeftButton==MouseButtonState.Pressed) DragMove(); }
}
