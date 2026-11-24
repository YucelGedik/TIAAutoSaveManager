using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using TIAAutoSaveManager.Core;
using Forms = System.Windows.Forms;

namespace TIAAutoSaveManager.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel model;
    private readonly IOpennessClient client;
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ToolStripMenuItem saveMenu;
    private readonly DispatcherTimer refresh;
    private bool canClose;
    private bool closing;
    private readonly ThemedNotification notifications = new();
    private Border? previewSurface;
    private readonly System.Drawing.Icon trayIcon;
    private HwndSource? windowSource;
    private bool screenFitQueued;
    private readonly bool trayEnabled;
    private static readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");

    public MainWindow(MainViewModel model, IOpennessClient client, bool startServices = true)
    {
        InitializeComponent();
        trayEnabled = startServices;
        this.model = model; this.client = client; DataContext = model;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Manager", null, (_, _) => Dispatcher.Invoke(Restore));
        saveMenu = new Forms.ToolStripMenuItem("Save Now", null, (_, _) => Dispatcher.Invoke(() =>
        {
            Restore(); if (model.SaveCommand.CanExecute(null)) model.SaveCommand.Execute(null);
        }));
        menu.Items.Add(saveMenu);
        var backupMenu = new Forms.ToolStripMenuItem("Backup Now", null, (_, _) => Dispatcher.Invoke(() => { Restore(); model.BackupCommand.Execute(null); }));
        menu.Items.Add(backupMenu);
        var pauseMenu = new Forms.ToolStripMenuItem("Pause / Resume", null, (_, _) => Dispatcher.Invoke(() => model.PauseCommand.Execute(null)));
        menu.Items.Add(pauseMenu);
        menu.Items.Add("View Logs", null, (_, _) => Dispatcher.Invoke(() => model.OpenLogsCommand.Execute(null)));
        menu.Items.Add("Yardım", null, (_, _) => Dispatcher.Invoke(() => { Restore(); MainSections.SelectedIndex = 1; }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(Close));
        trayIcon = BrandAssets.CreateTrayIcon();
        tray = new Forms.NotifyIcon { Text = "TIA Portal Auto Save Manager", Icon = trayIcon, ContextMenuStrip = menu, Visible = startServices };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(Restore);
        model.SaveCommand.CanExecuteChanged += (_, _) => saveMenu.Enabled = model.SaveCommand.CanExecute(null);
        saveMenu.Enabled = false;
        backupMenu.Enabled = false; pauseMenu.Enabled = false;
        model.BackupCommand.CanExecuteChanged += (_, _) => backupMenu.Enabled = model.BackupCommand.CanExecute(null);
        model.PauseCommand.CanExecuteChanged += (_, _) => { pauseMenu.Enabled = model.PauseCommand.CanExecute(null); pauseMenu.Text = model.PauseLabel; };
        model.NotificationRequested += (message, error) => notifications.Show(message, error, Restore);
        refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        refresh.Tick += async (_, _) => await model.OnTimerTickAsync();
        PageScroll.SizeChanged += (_, _) => UpdateResponsiveLayout();
        PageScroll.ScrollChanged += (_, e) => { if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) UpdateResponsiveLayout(); };
        StatusCard.SizeChanged += (_, _) => UpdateResponsiveLayout();
        HeaderGrid.SizeChanged += (_, _) => UpdateResponsiveLayout();
        SummaryGrid.SizeChanged += (_, _) => UpdateResponsiveLayout();
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.F1) { MainSections.SelectedIndex = 1; e.Handled = true; } };
        UpdateResponsiveLayout(Width - 16, Height - 39);
        if (startServices)
        {
            SourceInitialized += (_, _) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                windowSource = HwndSource.FromHwnd(handle);
                windowSource?.AddHook(ScreenMessages);
                WindowStartupLocation = WindowStartupLocation.Manual;
                FitToScreen(center: true);
            };
            Closed += (_, _) => windowSource?.RemoveHook(ScreenMessages);
            Loaded += async (_, _) => { UpdateResponsiveLayout(); await model.InitializeAsync(); refresh.Start(); };
            StateChanged += OnStateChanged;
            Closing += OnClosing;
        }
    }

    // Renders only our own visual tree for layout QA. No desktop capture, input, or TIA calls.
    public FrameworkElement LayoutPreview(double width, double height, bool settingsTab = false, bool helpTab = false)
    {
        MainSections.SelectedIndex = helpTab ? 1 : 0;
        AutomationTabs.SelectedIndex = settingsTab ? 1 : 0;
        if (previewSurface == null)
        {
            var root = (FrameworkElement)Content;
            root.SetValue(System.Windows.Documents.TextElement.FontSizeProperty, FontSize);
            root.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, FontFamily);
            root.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, Foreground);
            root.DataContext = model; Content = null;
            previewSurface = new Border { Background = Background, Child = root };
        }
        previewSurface.Width = width; previewSurface.Height = height;
        for (int pass = 0; pass < 4; pass++)
        {
            previewSurface.Measure(new Size(width, height));
            previewSurface.Arrange(new Rect(0, 0, width, height));
            previewSurface.UpdateLayout();
            if (!helpTab) UpdateResponsiveLayout();
        }
        return previewSurface;
    }

    public void RenderPreview(string path, bool settingsTab = false, int width = 1240, int height = 825, bool helpTab = false)
    {
        var surface = LayoutPreview(width, height, settingsTab, helpTab);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var output = System.IO.File.Create(path)) encoder.Save(output);
        ReleasePreviewResources();
    }

    public void ReleasePreviewResources() { notifications.Dispose(); tray.Dispose(); trayIcon.Dispose(); }

    private void UpdateResponsiveLayout(double width = 0, double height = 0)
    {
        if (width <= 0) width = PageScroll.ViewportWidth > 0 ? PageScroll.ViewportWidth : PageScroll.ActualWidth;
        if (height <= 0) height = PageScroll.ViewportHeight > 0 ? PageScroll.ViewportHeight : PageScroll.ActualHeight;
        if (width <= 0 || height <= 0) return;
        bool narrow = width < 1080;
        Grid.SetColumnSpan(LeftPanels, narrow ? 3 : 1);
        Grid.SetRow(Sidebar, narrow ? 2 : 0); Grid.SetColumn(Sidebar, narrow ? 0 : 2); Grid.SetColumnSpan(Sidebar, narrow ? 3 : 1);
        BodyGrid.RowDefinitions[1].Height = new GridLength(narrow ? 18 : 0);
        BodyGrid.ColumnDefinitions[1].Width = new GridLength(narrow ? 0 : 20);
        BodyGrid.ColumnDefinitions[2].Width = new GridLength(narrow ? 0 : 300);
        bool stackedHeader = width < 1000;
        Grid.SetRow(VersionBadge, stackedHeader ? 1 : 0); Grid.SetColumn(VersionBadge, stackedHeader ? 0 : 1);
        Grid.SetColumnSpan(VersionBadge, stackedHeader ? 2 : 1);
        VersionBadge.HorizontalAlignment = stackedHeader ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        VersionBadge.Margin = stackedHeader ? new Thickness(0,12,0,0) : new Thickness(16,9,0,0);
        double chrome = PageContent.Margin.Top + PageContent.Margin.Bottom + PageContent.RowDefinitions[0].ActualHeight + PageContent.RowDefinitions[1].ActualHeight + PageContent.RowDefinitions[3].ActualHeight;
        double bodyHeight = Math.Max(440, height - chrome);
        if (Math.Abs(LeftPanels.Height - bodyHeight) > 0.5 || double.IsNaN(LeftPanels.Height)) LeftPanels.Height = bodyHeight;
        double sideHeight = narrow ? 460 : bodyHeight;
        if (Math.Abs(Sidebar.Height - sideHeight) > 0.5 || double.IsNaN(Sidebar.Height)) Sidebar.Height = sideHeight;
    }

    public void OpenFromShortcut() => Restore();
    private void Restore()
    {
        if (canClose) return;
        if (trayEnabled && !closing) { tray.Visible = false; tray.Visible = true; }
        Show(); WindowState = WindowState.Normal; QueueScreenFit(); Activate();
    }

    private IntPtr ScreenMessages(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (taskbarCreated != 0 && (uint)message == taskbarCreated && trayEnabled && !closing)
            Dispatcher.BeginInvoke(new Action(() => { if (!closing) { tray.Visible = false; tray.Visible = true; } }));
        // WPF first applies its own DPI change; fit afterwards, without intercepting it.
        if (message is 0x02E0 or 0x007E or 0x001A or 0x0232) QueueScreenFit();
        return IntPtr.Zero;
    }
    private void QueueScreenFit()
    {
        if (screenFitQueued || closing) return;
        screenFitQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            screenFitQueued = false;
            if (!closing) FitToScreen(center: false);
        }));
    }
    private void FitToScreen(bool center)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || WindowState == WindowState.Minimized) return;
        var work = Forms.Screen.FromHandle(handle).WorkingArea;
        uint dpi = GetDpiForWindow(handle);
        double scale = dpi == 0 ? 1 : dpi / 96.0;
        var preferred = center ? new Size(1240, 860) : new Size(Width, Height);
        Point? position = !center && GetWindowRect(handle, out var rectangle) ? new Point(rectangle.Left, rectangle.Top) : null;
        var plan = ScreenWindowLayout.Calculate(new Rect(work.Left, work.Top, work.Width, work.Height), new DpiScale(scale, scale), preferred, position);
        MinWidth = plan.MinimumSize.Width; MinHeight = plan.MinimumSize.Height;
        if (WindowState != WindowState.Normal) return;
        Width = plan.Size.Width; Height = plan.Size.Height;
        SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(plan.PositionPixels.X), (int)Math.Round(plan.PositionPixels.Y), 0, 0, 0x0015);
        UpdateResponsiveLayout();
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rectangle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal) QueueScreenFit();
        if (WindowState == WindowState.Minimized && model.MinimizeToTray)
        {
            Hide();
            if (model.ShowNotifications) notifications.Show("Uygulama sistem tepsisinde. Otomatik işlemler: " + model.AutomationSummary, false, Restore);
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (canClose) return;
        e.Cancel = true;
        if (closing) return;
        if (model.IsBusy) { Restore(); model.ReportError("TIA işlemi sürüyor. Tamamlandıktan sonra çıkabilirsiniz; işlem zorla sonlandırılmadı."); return; }
        closing = true; refresh.Stop(); model.BeginShutdown();
        try
        {
            await client.DisposeAsync();
            notifications.Dispose(); tray.Visible = false; tray.Dispose(); trayIcon.Dispose();
            canClose = true; Close();
        }
        catch (Exception error) { model.ReportError("Güvenli ayrılma bekleniyor: " + error.Message); closing = false; }
    }
}
