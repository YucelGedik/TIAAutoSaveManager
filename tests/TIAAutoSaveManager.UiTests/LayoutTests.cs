using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NUnit.Framework;
using Serilog;
using TIAAutoSaveManager.App;
using TIAAutoSaveManager.Contracts;
using TIAAutoSaveManager.Core;

namespace TIAAutoSaveManager.UiTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, SingleThreaded]
public class LayoutTests
{
    private sealed class NoApi : IOpennessClient, IBackupService, ISettingsService
    {
        public int Calls;
        public Task<HostResponse> SendAsync(HostRequest request, CancellationToken cancellationToken = default) { Calls++; throw new AssertionException("UI layout must not call TIA."); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<BackupResult> CreateAsync(PortalSnapshot target, ManagerSettings settings, CancellationToken cancellationToken = default) { Calls++; throw new AssertionException("UI layout must not back up projects."); }
        public Task<SettingsLoadResult> LoadAsync() => Task.FromResult(new SettingsLoadResult(new()));
        public Task SaveAsync(ManagerSettings settings) => throw new AssertionException("UI layout must not write settings.");
    }
    private sealed class CancelDialogs : IUserDialogService
    {
        public int Confirmations;
        public bool Confirm(string message, string title) { Confirmations++; return false; }
        public void Notice(string message, string title, bool error = false) => throw new AssertionException("Unexpected notice.");
    }
    [OneTimeSetUp] public void SetupResources()
    {
        if (Application.Current == null)
        {
            var app = new TIAAutoSaveManager.App.App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
        }
    }

    [TestCase(1240,825)][TestCase(1920,1040)][TestCase(800,600)][TestCase(744,480)][TestCase(640,400)][TestCase(880,420)]
    public void ActionsStayInsideTheirCardAndSmallWindowsCanScroll(int width, int height)
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var noApi = new NoApi();
        var model = new MainViewModel(new(noApi), noApi, noApi, logger, ".");
        model.ReportError("Long error\n" + string.Join("\n", Enumerable.Repeat(new string('x',180),8)));
        var window = new MainWindow(model, noApi, startServices: false);
        try
        {
            window.LayoutPreview(width,height);
            AssertActionsContained(window);
            var sidebar = (Border)window.FindName("Sidebar");
            Assert.That(Grid.GetRow(sidebar), Is.EqualTo(width < 1080 ? 2 : 0));
            var scroll = (ScrollViewer)window.FindName("PageScroll");
            if (width < 1080) Assert.That(scroll.ExtentHeight, Is.GreaterThan(scroll.ViewportHeight));
            Assert.That(((Border)window.FindName("StatusCard")).ActualHeight, Is.LessThan(125));
        }
        finally { window.ReleasePreviewResources(); }
    }

    [Test] public void ShrinkThenGrowReflowsTheSameVisualTree()
    {
        using var logger = new LoggerConfiguration().CreateLogger(); var noApi = new NoApi();
        var window = new MainWindow(new(new(noApi), noApi, noApi, logger,"."), noApi, false);
        try
        {
            foreach (var size in new[] { new Size(1600,950),new Size(744,480),new Size(1000,650),new Size(1600,950) })
            {
                window.LayoutPreview(size.Width,size.Height); AssertActionsContained(window);
                Assert.That(Grid.GetRow((Border)window.FindName("Sidebar")), Is.EqualTo(size.Width < 1080 ? 2 : 0));
            }
        }
        finally { window.ReleasePreviewResources(); }
    }
    private static void AssertActionsContained(MainWindow window)
    {
        var card = (Border)window.FindName("InstancesCard"); var bar = (WrapPanel)window.FindName("ActionBar");
        var bounds = new Rect(0,0,card.ActualWidth,card.ActualHeight); bounds.Inflate(0.5,0.5);
        Assert.That(bar.Children.Count, Is.EqualTo(5));
        foreach (Button button in bar.Children)
        {
            var rectangle = button.TransformToAncestor(card).TransformBounds(new Rect(0,0,button.ActualWidth,button.ActualHeight));
            Assert.That(button.ActualHeight, Is.GreaterThanOrEqualTo(35));
            Assert.That(bounds.Contains(rectangle), Is.True, $"{button.Content} clipped: {rectangle} outside {bounds}");
        }
    }
    [TestCase(1366,728,1)][TestCase(1366,728,1.25)][TestCase(1366,728,1.5)]
    [TestCase(1920,1040,2)][TestCase(1024,728,1.5)][TestCase(800,560,1)]
    public void StartupFitsScaledWorkAreaAndHelpNavigationStaysVisible(int pixelWidth, int pixelHeight, double scale)
    {
        var area = new Rect(-1920, 60, pixelWidth, pixelHeight);
        var plan = ScreenWindowLayout.Calculate(area, new DpiScale(scale, scale), new Size(1240,860));
        var physicalWindow = new Rect(plan.PositionPixels, new Size(plan.Size.Width * scale, plan.Size.Height * scale));
        Assert.That(area.Contains(physicalWindow), Is.True, "The whole window, including the title bar, must fit.");
        Assert.That(plan.MinimumSize.Width, Is.LessThanOrEqualTo(plan.Size.Width));
        Assert.That(plan.MinimumSize.Height, Is.LessThanOrEqualTo(plan.Size.Height));
        using var logger = new LoggerConfiguration().CreateLogger(); var noApi = new NoApi();
        var window = new MainWindow(new(new(noApi),noApi,noApi,logger,"."),noApi,false);
        try
        {
            double width = plan.Size.Width - 16, height = plan.Size.Height - 39;
            var surface = window.LayoutPreview(width,height);
            AssertActionsContained(window);
            var tabs = (TabControl)window.FindName("MainSections");
            var helpTab = (TabItem)tabs.Items[1];
            var bounds = helpTab.TransformToAncestor(surface).TransformBounds(new Rect(helpTab.RenderSize));
            var viewport = new Rect(0,0,width,height); viewport.Inflate(0.5,0.5); // WPF layout rounds fractional DIP edges to pixels.
            Assert.That(viewport.Contains(bounds), Is.True, $"Help must stay inside the top bar: {bounds} within {viewport}.");
            Assert.That(helpTab.IsEnabled, Is.True);
            window.LayoutPreview(width,height,helpTab:true);
            Assert.That(tabs.SelectedIndex, Is.EqualTo(1));
            Assert.That(noApi.Calls, Is.Zero);
        }
        finally { window.ReleasePreviewResources(); }
    }
    [Test] public void ScreenChangeBringsAnOffscreenWindowBackWithoutGrowingIt()
    {
        var area = new Rect(0,40,1366,688);
        var plan = ScreenWindowLayout.Calculate(area,new DpiScale(1.5,1.5),new Size(1240,860),new Point(-1800,-500));
        Assert.That(area.Contains(new Rect(plan.PositionPixels,new Size(plan.Size.Width*1.5,plan.Size.Height*1.5))), Is.True);
        var next = ScreenWindowLayout.Calculate(new Rect(1366,0,1920,1040),new DpiScale(1,1),plan.Size,plan.PositionPixels);
        Assert.That(next.Size, Is.EqualTo(plan.Size), "Moving to a larger display should not override the user's window size.");
    }
    [TestCase(true)][TestCase(false)] public void DialogUsesThemeAndNeverDefaultsToAccept(bool confirmation)
    {
        var dialog = new ThemedDialog(new string('x',500),"Otomatik işlem onayı",confirmation);
        var cancel = (Button)dialog.FindName("CancelAction"); var primary = (Button)dialog.FindName("PrimaryAction");
        Assert.That(cancel.IsCancel, Is.True); Assert.That(cancel.IsDefault, Is.True);
        Assert.That(primary.IsDefault, Is.False);
        Assert.That(primary.Visibility, Is.EqualTo(confirmation ? Visibility.Visible : Visibility.Collapsed));
        Assert.That(((SolidColorBrush)dialog.Background).Color, Is.EqualTo(((SolidColorBrush)Application.Current.FindResource("BackgroundBrush")).Color));
        dialog.FitToArea(744,480);
        Assert.That(dialog.MaxHeight, Is.LessThanOrEqualTo(432));
        Assert.That(((TextBox)dialog.FindName("Message")).MaxHeight, Is.LessThanOrEqualTo(202));
    }
    [TestCase("Save")][TestCase("Backup")][TestCase("Auto")]
    public void CancelledConfirmationNeverWritesOrEnablesAutomation(string operation)
    {
        using var logger = new LoggerConfiguration().CreateLogger(); var noApi = new NoApi(); var dialogs = new CancelDialogs();
        var model = new MainViewModel(new(noApi), noApi, noApi, logger,".",dialogs);
        var row = new PortalRow(new() { ProcessId = 1, StartedUtcTicks = 100, SessionId = "test", Status = "Connected", ProjectName = "Test", ProjectPath = @"D:\Test\Test.ap21" });
        model.Portals.Add(row); model.Selected = row;
        (operation == "Save" ? model.SaveCommand : operation == "Backup" ? model.BackupCommand : model.ToggleAutoSaveCommand).Execute(null);
        Assert.That(dialogs.Confirmations, Is.EqualTo(1)); Assert.That(noApi.Calls, Is.Zero);
        Assert.That(model.AutomationSummary, Is.EqualTo("Kapalı"));
    }
    [Test] public void HelpScreenshotsAndLogoAreBundledAndDecodeOffline()
    {
        Assert.That(HelpContent.SetupSteps.Count, Is.EqualTo(4));
        foreach (var uri in HelpContent.SetupSteps.Concat(HelpContent.UserNameSteps).Select(s => s.ImageUri).Append(BrandAssets.LogoUri).Append(BrandAssets.IconUri))
        {
            var image = new System.Windows.Media.Imaging.BitmapImage(new Uri(uri));
            Assert.That(image.PixelWidth, Is.GreaterThan(0)); Assert.That(image.PixelHeight, Is.GreaterThan(0));
        }
        using var trayIcon = BrandAssets.CreateTrayIcon(); Assert.That(trayIcon.Width, Is.EqualTo(32));
    }
    [Test] public void HelpTabIsFullWidthAndProgressBarIsThicker()
    {
        using var logger = new LoggerConfiguration().CreateLogger(); var noApi = new NoApi();
        var window = new MainWindow(new(new(noApi),noApi,noApi,logger,"."),noApi,false);
        try
        {
            window.LayoutPreview(1000,700,helpTab:true);
            var sections = (TabControl)window.FindName("MainSections");
            Assert.That(sections.Items.Count, Is.EqualTo(2)); Assert.That(sections.SelectedIndex, Is.EqualTo(1));
            var help = (HelpView)window.FindName("HelpPage");
            Assert.That(help.ActualWidth, Is.GreaterThan(900));
            Assert.That(((ItemsControl)help.FindName("SetupSteps")).Items.Count, Is.EqualTo(4));
            Assert.That(((ProgressBar)window.FindName("ActivityProgress")).Height, Is.EqualTo(12));
            Assert.That(noApi.Calls, Is.Zero);
        }
        finally { window.ReleasePreviewResources(); }
    }
    [Test] public void HelpStepsReflowBetweenCompactColumnsAndNarrowRows()
    {
        var help = new HelpView();
        foreach (var width in new[] { 1100, 600, 1100 })
        {
            for (var pass = 0; pass < 4; pass++)
            {
                help.Measure(new Size(width, 800));
                help.Arrange(new Rect(0, 0, width, 800));
                help.UpdateLayout();
            }
            var steps = Descendants(help).OfType<Grid>().Where(g => g.Name == "StepLayout").ToArray();
            Assert.That(steps.Length, Is.EqualTo(4));
            foreach (var step in steps)
            {
                var text = (FrameworkElement)step.Children[0];
                var imagePanel = (FrameworkElement)step.Children[1];
                var textBounds = text.TransformToAncestor(step).TransformBounds(new Rect(text.RenderSize));
                var imageBounds = imagePanel.TransformToAncestor(step).TransformBounds(new Rect(imagePanel.RenderSize));
                if (width == 1100)
                {
                    Assert.That(imageBounds.Left, Is.GreaterThanOrEqualTo(textBounds.Right + 23));
                    Assert.That(step.ActualHeight, Is.LessThan(330), "Wide help cards should stay compact.");
                }
                else Assert.That(imageBounds.Top, Is.GreaterThanOrEqualTo(textBounds.Bottom + 15));
                Assert.That(imageBounds.Right, Is.LessThanOrEqualTo(step.ActualWidth + 0.5));
                Assert.That(text.ActualWidth, Is.GreaterThan(300));
            }
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    [Test] public void ScreenshotViewerOpensBundledImageWithoutNetworkOrTia()
    {
        var viewer = new ScreenshotWindow(HelpContent.SetupSteps[0]);
        var image = (Image)viewer.FindName("Screenshot");
        Assert.That(image.Source.Width, Is.GreaterThan(0));
        var surface = (FrameworkElement)viewer.Content;
        surface.Measure(new Size(800,600)); surface.Arrange(new Rect(0,0,800,600)); surface.UpdateLayout();
        var zoom = (Slider)viewer.FindName("Zoom");
        var bounds = zoom.TransformToAncestor(surface).TransformBounds(new Rect(0,0,zoom.ActualWidth,zoom.ActualHeight));
        Assert.That(new Rect(0,0,800,600).Contains(bounds), Is.True);
    }

    [Test] public void SecondLaunchSignalsExistingInstanceInsteadOfBecomingPrimary()
    {
        string key = @"Local\TIAAutoSaveManager-Test-" + Guid.NewGuid().ToString("N");
        int activations = 0;
        using var primary = new SingleInstanceActivation(key, () => Interlocked.Increment(ref activations));
        Assert.That(primary.IsPrimary, Is.True);
        Task.Run(() =>
        {
            using var secondary = new SingleInstanceActivation(key, () => throw new AssertionException("Secondary must never handle activation."));
            Assert.That(secondary.IsPrimary, Is.False);
            Assert.That(SingleInstanceActivation.TryActivate(key, TimeSpan.FromSeconds(2)), Is.True);
        }).GetAwaiter().GetResult();
        Assert.That(activations, Is.EqualTo(1));
    }
    [Test] public void MissingActivationListenerDoesNotClaimTheWindowWasShown()
    {
        string key = @"Local\TIAAutoSaveManager-Test-" + Guid.NewGuid().ToString("N");
        using var oldInstance = new Mutex(true,key);
        try
        {
            Task.Run(() =>
            {
                using var secondary = new SingleInstanceActivation(key, () => { });
                Assert.That(secondary.IsPrimary, Is.False);
                Assert.That(SingleInstanceActivation.TryActivate(key, TimeSpan.FromMilliseconds(20)), Is.False);
            }).GetAwaiter().GetResult();
        }
        finally { oldInstance.ReleaseMutex(); }
    }
    [Test] public void AbandonedInstanceMutexCanBeRecovered()
    {
        string key = @"Local\TIAAutoSaveManager-Test-" + Guid.NewGuid().ToString("N");
        using var keepAlive = new Mutex(false,key);
        var deadOwner = new Thread(() => { using var handle = Mutex.OpenExisting(key); handle.WaitOne(); });
        deadOwner.Start(); deadOwner.Join();
        using var replacement = new SingleInstanceActivation(key, () => { });
        Assert.That(replacement.IsPrimary, Is.True);
    }
    [Test] public void InstanceLockIsReleasedForTheNextNormalLaunch()
    {
        string key = @"Local\TIAAutoSaveManager-Test-" + Guid.NewGuid().ToString("N");
        using (var first = new SingleInstanceActivation(key, () => { })) Assert.That(first.IsPrimary, Is.True);
        using var next = new SingleInstanceActivation(key, () => { });
        Assert.That(next.IsPrimary, Is.True);
    }
}
