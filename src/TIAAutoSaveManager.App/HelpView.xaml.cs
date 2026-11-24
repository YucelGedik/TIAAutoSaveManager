using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace TIAAutoSaveManager.App;

public partial class HelpView : UserControl
{
    public HelpView() { InitializeComponent(); SetupSteps.ItemsSource = HelpContent.SetupSteps; UserNameScreenshots.ItemsSource = HelpContent.UserNameSteps; }
    private void StepLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var layout = (Grid)sender;
        var screenshot = (FrameworkElement)layout.Children[1];
        var sideBySide = e.NewSize.Width >= 720;
        layout.ColumnDefinitions[1].Width = sideBySide ? new GridLength(1.15, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(screenshot, sideBySide ? 1 : 0);
        Grid.SetRow(screenshot, sideBySide ? 0 : 1);
        screenshot.Margin = sideBySide ? new Thickness(24, 0, 0, 0) : new Thickness(0, 16, 0, 0);
    }
    private void GoSetup(object sender, RoutedEventArgs e) => SetupSection.BringIntoView();
    private void GoRestore(object sender, RoutedEventArgs e) => RestoreSection.BringIntoView();
    private void GoUsage(object sender, RoutedEventArgs e) => UsageSection.BringIntoView();
    private void GoTroubleshooting(object sender, RoutedEventArgs e) => TroubleshootingSection.BringIntoView();
    private void OpenImage(object sender, MouseButtonEventArgs e) => ShowScreenshot((FrameworkElement)sender);
    private void OpenImageButton(object sender, RoutedEventArgs e) => ShowScreenshot((FrameworkElement)sender);
    private void ShowScreenshot(FrameworkElement element)
    {
        if (element.DataContext is not HelpStep step) return;
        var window = new ScreenshotWindow(step) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }
    private void OpenDocumentation(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (System.Exception ex) { ThemedDialogService.Shared.Notice(ex.Message,"Doküman açılamadı",true); }
    }
}
