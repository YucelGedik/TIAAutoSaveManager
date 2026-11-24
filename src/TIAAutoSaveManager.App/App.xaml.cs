using System;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Serilog;
using TIAAutoSaveManager.Infrastructure;
using TIAAutoSaveManager.Core;

namespace TIAAutoSaveManager.App;

public partial class App : Application
{
    private SingleInstanceActivation? singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            bool preview = (e.Args.Length == 2 || e.Args.Length == 4) && (e.Args[0] == "--render-preview" || e.Args[0] == "--render-settings-preview" || e.Args[0] == "--render-error-preview" || e.Args[0] == "--render-dialog-preview" || e.Args[0] == "--render-help-preview");
            if (!preview)
            {
                using var identity = WindowsIdentity.GetCurrent();
                string key = @"Local\TIAAutoSaveManager-" + identity.User!.Value;
                singleInstance = new SingleInstanceActivation(key, () => Dispatcher.Invoke(() =>
                {
                    if (MainWindow is MainWindow existing) existing.OpenFromShortcut();
                }));
                if (!singleInstance.IsPrimary)
                {
                    GrantForegroundToExistingInstance();
                    if (!SingleInstanceActivation.TryActivate(key, TimeSpan.FromSeconds(3)))
                        ThemedDialogService.Shared.Notice("Çalışan Manager pencereyi açma isteğine yanıt vermedi. Açılış veya kapanış sürüyor olabilir. Eski sürüm çalışıyorsa güncelleme kurucusundaki kapatma onayını kullanın; TIA işlemini zorla kesmeyin.", "Çalışan Manager’a ulaşılamadı");
                    Shutdown();
                    return;
                }
            }
            string directory = Environment.GetEnvironmentVariable("TIAAUTOSAVE_DATA_DIR")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TIAAutoSaveManager");
            Directory.CreateDirectory(Path.Combine(directory, "Logs"));
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().Enrich.WithProperty("Component", "Desktop")
                .WriteTo.File(Path.Combine(directory, "Logs", "manager-.log"), rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14, fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true).CreateLogger();
            if (preview && e.Args[0] == "--render-dialog-preview")
            {
                new ThemedDialog("Proje: Örnek Makine\nYol: D:\\TestProjects\\Makine\\Makine.ap21\nPID: 7124\n\nSave, TIA Portal’ın Undo/Redo geçmişini temizler.\n\nOtomatik Backup her 10 dakikada Save + Archive yapar.\nHedef: D:\\TIA_Backups\n\nBu proje için etkinleştirilsin mi?", "Otomatik Backup’ı etkinleştir")
                    .RenderPreview(Path.GetFullPath(e.Args[1]));
                Shutdown(); return;
            }
            var client = new VersionedOpennessClient(Path.Combine(AppContext.BaseDirectory, "host", "TIAAutoSaveManager.OpennessHost.exe"), Log.Logger);
            var coordinator = new ProjectOperationCoordinator(client);
            var model = new MainViewModel(coordinator, new ProjectBackupService(coordinator, Log.Logger), new JsonSettingsService(directory), Log.Logger, directory);
            var window = new MainWindow(model, client, startServices: !preview);
            if (preview)
            {
                var sample = new TIAAutoSaveManager.Contracts.PortalSnapshot { ProcessId = 7124, StartedUtcTicks = 1, SessionId = "preview-only", Status = "Connected", ProjectName = "Örnek Makine", ProjectPath = @"D:\TestProjects\Makine\Makine.ap21" };
                model.Portals.Add(new PortalRow(sample)); model.Selected = model.Portals[0];
                model.ReportError(e.Args[0] == "--render-error-preview"
                    ? "Görsel hata yerleşim testi · gerçek Archive çalıştırılmadı.\n\nUnable to archive the project.\n\nArchiving failed.\nThe specified path: C:\\BackupTest\\" + new string('x', 190) + " is too long. A maximum of 143 characters is allowed."
                    : "Arayüz önizlemesi · örnek veri · TIA çağrısı yok.");
                int width = e.Args.Length == 4 ? int.Parse(e.Args[2], System.Globalization.CultureInfo.InvariantCulture) : 1240;
                int height = e.Args.Length == 4 ? int.Parse(e.Args[3], System.Globalization.CultureInfo.InvariantCulture) : 825;
                window.RenderPreview(Path.GetFullPath(e.Args[1]), e.Args[0] == "--render-settings-preview", width, height, e.Args[0] == "--render-help-preview");
                Shutdown();
                return;
            }
            MainWindow = window;
            window.Show();
            Log.Information("Manager started. Automatic Save and backup require per-project user activation.");
        }
        catch (Exception ex)
        {
            ThemedDialogService.Shared.Notice("Uygulama başlatılamadı: " + ex.Message, "Başlatma hatası", error: true);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        singleInstance?.Dispose();
        Log.CloseAndFlush(); base.OnExit(e);
    }
    private static void GrantForegroundToExistingInstance()
    {
        using var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName(current.ProcessName))
            using (process)
                try { if (process.Id != current.Id && process.SessionId == current.SessionId) AllowSetForegroundWindow((uint)process.Id); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
