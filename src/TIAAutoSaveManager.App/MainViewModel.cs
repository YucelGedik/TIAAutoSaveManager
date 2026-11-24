using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using TIAAutoSaveManager.Contracts;
using TIAAutoSaveManager.Core;

namespace TIAAutoSaveManager.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ProjectOperationCoordinator coordinator;
    private readonly IBackupService backups;
    private readonly ISettingsService settingsService;
    private readonly ILogger log;
    private readonly string dataDirectory;
    private readonly IUserDialogService dialogs;
    private readonly AutoSaveSchedule schedule = new();
    private readonly Dictionary<string, DateTimeOffset> lastBackups = new();
    private ManagerSettings settings = new();
    private PortalRow? selected;
    private bool busy, uncertain, closing, dialogOpen;
    private long lastDiscovery = Stopwatch.GetTimestamp();
    private string discoverySignature = "";
    private string status = "Hazır. Otomatik işlemler başlangıçta kapalıdır.";
    private string intervalText = "10", backupIntervalText = "60", retentionText = "10";
    private string backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TIAAutoSaveManager", "Backups");
    private bool minimizeToTray = true, showNotifications = true;

    public MainViewModel(ProjectOperationCoordinator coordinator, IBackupService backups, ISettingsService settingsService, ILogger log, string directory, IUserDialogService? dialogs = null)
    {
        this.coordinator = coordinator; this.backups = backups; this.settingsService = settingsService; this.log = log; dataDirectory = directory;
        this.dialogs = dialogs ?? ThemedDialogService.Shared;
        settings = settings with { BackupDirectory = backupDirectory };
        RefreshCommand = new(() => RunAsync("Discover"), CanOperate);
        ConnectCommand = new(() => RunAsync("Attach", Selected?.Snapshot), () => CanOperate() && Selected is { Snapshot.StartedUtcTicks: > 0 });
        SaveCommand = new(() => ManualAsync("Save"), CanWrite);
        BackupCommand = new(() => ManualAsync("Backup"), CanWrite);
        DisconnectCommand = new(() => RunAsync("Disconnect"), CanOperate);
        ToggleAutoSaveCommand = new(() => ToggleAutomationAsync(false), CanWrite);
        ToggleBackupCommand = new(() => ToggleAutomationAsync(true), CanWrite);
        PauseCommand = new(TogglePauseAsync, () => !closing && !uncertain && schedule.EnabledCount > 0);
        SaveSettingsCommand = new(SaveSettingsAsync, CanOperate);
        OpenLogsCommand = new(() => OpenFolderAsync(Path.Combine(dataDirectory, "Logs")));
        OpenBackupFolderCommand = new(() => OpenFolderAsync(settings.BackupDirectory));
        IntervalPresetCommand = new(value => IntervalText = value?.ToString() ?? "10");
        BrowseBackupFolderCommand = new(BrowseBackupFolderAsync, CanOperate);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string, bool>? NotificationRequested;
    public ObservableCollection<PortalRow> Portals { get; } = new();
    public ObservableCollection<string> Events { get; } = new();
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand ConnectCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand BackupCommand { get; }
    public AsyncCommand DisconnectCommand { get; }
    public AsyncCommand ToggleAutoSaveCommand { get; }
    public AsyncCommand ToggleBackupCommand { get; }
    public AsyncCommand PauseCommand { get; }
    public AsyncCommand SaveSettingsCommand { get; }
    public AsyncCommand OpenLogsCommand { get; }
    public AsyncCommand OpenBackupFolderCommand { get; }
    public AsyncCommand BrowseBackupFolderCommand { get; }
    public ParameterCommand IntervalPresetCommand { get; }
    public PortalRow? Selected { get => selected; set { selected = value; Changed(); RefreshLabels(); } }
    public bool IsBusy { get => busy; private set { busy = value; Changed(); RefreshLabels(); } }
    public string ActivityText => IsBusy ? "TIA / dosya işlemi sürüyor · zorla iptal edilmez" : "Seçili projeler için Save ve sürümlü Backup";
    public string Status { get => status; private set { status = value; Changed(); } }
    public string InstanceCount => Portals.Count.ToString(CultureInfo.InvariantCulture);
    public string ConnectedCount => Portals.Count(p => p.Snapshot.Status == "Connected").ToString(CultureInfo.InvariantCulture);
    public string SelectedPath => Selected?.Snapshot.ProjectPath is { Length: > 0 } path ? path : "Bir instance seçin. Proje yolu burada gösterilir.";
    public string AutomationSummary => schedule.EnabledCount == 0 ? "Kapalı" : schedule.IsPaused ? "Duraklatıldı" : schedule.EnabledCount + " proje etkin";
    public string AutomationDetail => "Save: " + settings.SaveIntervalMinutes + " dk · Backup: " + settings.BackupIntervalMinutes + " dk";
    public string AppliedBackupDirectory => settings.BackupDirectory;
    public string AutoSaveLabel => schedule.Enabled(Selected?.Snapshot).Save ? "Auto Save’i kapat" : "Auto Save’i etkinleştir";
    public string AutoBackupLabel => schedule.Enabled(Selected?.Snapshot).Backup ? "Otomatik Backup’ı kapat" : "Otomatik Backup’ı etkinleştir";
    public string PauseLabel => schedule.IsPaused ? "▶  Resume" : "Ⅱ  Pause";
    public string SelectedSchedule => Selected == null ? "Otomatik işlem için bağlı projeyi seçin." : "Sonraki Save: " + Countdown(Selected.Snapshot, ScheduledOperationKind.Save) + "   ·   Backup: " + Countdown(Selected.Snapshot, ScheduledOperationKind.Backup);
    public string LastBackupText => Selected != null && lastBackups.TryGetValue(Selected.Snapshot.SessionId, out var value) ? value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "—";
    public string IntervalText { get => intervalText; set { intervalText = value; Changed(); } }
    public string BackupIntervalText { get => backupIntervalText; set { backupIntervalText = value; Changed(); } }
    public string RetentionText { get => retentionText; set { retentionText = value; Changed(); } }
    public string BackupDirectory { get => backupDirectory; set { backupDirectory = value; Changed(); } }
    public bool MinimizeToTray { get => minimizeToTray; set { minimizeToTray = value; Changed(); } }
    public bool ShowNotifications { get => showNotifications; set { showNotifications = value; Changed(); } }
    private bool CanOperate() => !IsBusy && !closing && !uncertain && !dialogOpen;
    private bool CanWrite() => CanOperate() && Selected is { CanSave: true };

    public async Task InitializeAsync()
    {
        try
        {
            var loaded = await settingsService.LoadAsync(); settings = loaded.Settings;
            if (string.IsNullOrWhiteSpace(settings.BackupDirectory)) settings = settings with { BackupDirectory = backupDirectory };
            IntervalText = settings.SaveIntervalMinutes.ToString(CultureInfo.InvariantCulture);
            BackupIntervalText = settings.BackupIntervalMinutes.ToString(CultureInfo.InvariantCulture);
            RetentionText = settings.BackupRetention.ToString(CultureInfo.InvariantCulture); BackupDirectory = settings.BackupDirectory;
            MinimizeToTray = settings.MinimizeToTray; ShowNotifications = settings.ShowNotifications;
            schedule.Configure(TimeSpan.FromMinutes(settings.SaveIntervalMinutes), TimeSpan.FromMinutes(settings.BackupIntervalMinutes));
            if (loaded.Warning != null) AddEvent(loaded.Warning, true);
        }
        catch (Exception e) { AddEvent("Ayarlar okunamadı: " + e.Message, true); }
        await RunAsync("Discover");
    }

    private bool Confirm(string message, string title)
    {
        dialogOpen = true; UpdateCommands();
        try { return dialogs.Confirm(message, title); }
        finally { dialogOpen = false; UpdateCommands(); }
    }
    private static string ProjectConfirmation(PortalSnapshot target) => "Proje: " + target.ProjectName + "\nYol: " + target.ProjectPath + "\nPID: " + target.ProcessId + "\n\nSave, TIA Portal’ın Undo/Redo geçmişini temizler. İlk doğrulamayı ayrı test projesinde yapın.";
    private async Task ManualAsync(string operation)
    {
        var target = Selected?.Snapshot;
        if (target == null) return;
        string extra = operation == "Backup" ? "\n\nÖnce Save, sonra tarihli " + PortalVersions.ArchiveExtension(target.Version) + " oluşturulur.\nHedef: " + settings.BackupDirectory : "";
        if (Confirm(ProjectConfirmation(target) + extra + "\n\nDevam edilsin mi?", operation + " · Proje onayı")) await RunAsync(operation, target);
    }
    private Task ToggleAutomationAsync(bool backup)
    {
        var target = Selected?.Snapshot;
        if (target == null) return Task.CompletedTask;
        var enabled = schedule.Enabled(target);
        bool turningOn = backup ? !enabled.Backup : !enabled.Save;
        if (turningOn && !Confirm(ProjectConfirmation(target) + (backup ? "\n\nOtomatik Backup her " + settings.BackupIntervalMinutes + " dakikada Save + Archive yapar.\nHedef: " + settings.BackupDirectory : "\n\nAuto Save her " + settings.SaveIntervalMinutes + " dakikada mevcut projeyi kaydeder.") + "\n\nBu proje için etkinleştirilsin mi?", "Otomatik işlem onayı")) return Task.CompletedTask;
        schedule.Enable(target, backup ? enabled.Save : turningOn, backup ? turningOn : enabled.Backup);
        AddEvent(target.ProjectName + " · " + (backup ? "Otomatik Backup" : "Auto Save") + (turningOn ? " açıldı." : " kapatıldı."));
        RefreshLabels(); return Task.CompletedTask;
    }
    private Task TogglePauseAsync()
    {
        schedule.SetPaused(!schedule.IsPaused);
        Status = schedule.IsPaused ? "Otomatik işlemler duraklatıldı. Aktif işlem zorla kesilmez." : "Otomatik işlemler devam ediyor; süreler yeniden başladı.";
        AddEvent(Status); RefreshLabels(); return Task.CompletedTask;
    }
    public async Task OnTimerTickAsync()
    {
        RefreshCountdowns();
        if (!CanOperate()) return;
        var due = schedule.TakeDue();
        if (due != null) { await RunAsync(due.Kind == ScheduledOperationKind.Backup ? "Backup" : "Save", due.Target, true, scheduled: due); return; }
        if (Stopwatch.GetElapsedTime(lastDiscovery) >= TimeSpan.FromSeconds(15)) await RunAsync("Discover", background: true);
    }

    private async Task RunAsync(string operation, PortalSnapshot? target = null, bool automatic = false, bool background = false, ScheduledOperation? scheduled = null)
    {
        if (!CanOperate()) return;
        IsBusy = true;
        if (!background) Status = operation == "Attach" ? "Bağlanılıyor. TIA Portal erişim istemini kontrol edin." : operation + " devam ediyor…";
        HostResponse? response = null;
        try
        {
            if (operation == "Backup")
            {
                var result = await Task.Run(() => backups.CreateAsync(target!, settings));
                response = result.Response; response.Message = result.Message;
                if (result.Success) lastBackups[target!.SessionId] = DateTimeOffset.UtcNow;
            }
            else response = await coordinator.ExecuteAsync(new() { Operation = operation, Target = target! });
            if (operation == "Discover")
            {
                lastDiscovery = Stopwatch.GetTimestamp();
                if (response.Success)
                {
                    var previousSelection = Selected?.Snapshot; int previousCount = schedule.EnabledCount;
                    schedule.Synchronize(response.Portals); Portals.Clear();
                    foreach (var item in response.Portals) Portals.Add(new(item));
                    Selected = previousSelection == null ? Portals.FirstOrDefault() : Portals.FirstOrDefault(p => previousSelection.SameProcess(p.Snapshot));
                    if (schedule.EnabledCount < previousCount) AddEvent("Bağlantı/proje değişti; ilgili otomatik işlemler kapatıldı.", true);
                }
                else schedule.DisableAll();
            }
            else if (operation == "Disconnect" && response.Success) { schedule.DisableAll(); Portals.Clear(); Selected = null; }
            else
            {
                foreach (var item in response.Portals) ReplaceRow(item);
                if (!response.Success && response.Outcome is "Stale" or "Disconnected" or "AccessDenied") Invalidate(target);
            }
            if (response.Outcome == "Disconnected") { schedule.DisableAll(); foreach (var row in Portals.ToArray()) Invalidate(row.Snapshot); }
            if (response.Outcome == "Uncertain")
            {
                uncertain = true; schedule.DisableAll();
                Status = "İşlem sonucu bilinmiyor. TIA projesini kontrol edin; otomatik tekrar engellendi. " + response.Message;
            }
            bool changedDiscovery = operation != "Discover" || discoverySignature != Signature(response);
            if (operation == "Discover") discoverySignature = Signature(response);
            if ((!background || changedDiscovery || !response.Success) && response.Outcome != "Uncertain") Status = response.Message;
            if (operation != "Discover" || changedDiscovery || !response.Success) AddEvent((automatic ? "Auto " : "") + operation + " · " + response.Message, !response.Success);
            if (automatic && ShowNotifications) NotificationRequested?.Invoke(response.Message, !response.Success);
            if (!automatic && target != null && operation is "Save" or "Backup") schedule.Complete(new(target, operation == "Backup" ? ScheduledOperationKind.Backup : ScheduledOperationKind.Save), response.SaveCompleted, response.Outcome is not ("Uncertain" or "Stale" or "Disconnected"));
        }
        catch (Exception e)
        {
            if (operation is "Save" or "Backup") { uncertain = true; schedule.DisableAll(); }
            else if (operation == "Discover") schedule.DisableAll();
            Status = "İşlem tamamlanamadı: " + e.Message; log.Error(e, "UI request failed {Operation}", operation); AddEvent(Status, true);
            if (ShowNotifications) NotificationRequested?.Invoke(Status, true);
        }
        finally
        {
            if (scheduled != null) schedule.Complete(scheduled, response?.SaveCompleted == true, !uncertain && response?.Outcome is not ("Stale" or "Disconnected" or "AccessDenied"));
            IsBusy = false; RefreshLabels();
        }
    }
    private static string Signature(HostResponse response) => response.Success + ":" + response.Message + string.Join("|", response.Portals.Select(p => $"{p.ProcessId}:{p.StartedUtcTicks}:{p.ProjectPath}:{p.SessionId}:{p.Status}"));
    private void ReplaceRow(PortalSnapshot item)
    {
        var previous = Portals.FirstOrDefault(p => p.Snapshot.SameProcess(item)); bool wasSelected = previous != null && ReferenceEquals(previous, Selected);
        var row = new PortalRow(item);
        if (previous == null) Portals.Add(row); else Portals[Portals.IndexOf(previous)] = row;
        if (wasSelected || Selected == null) Selected = row;
        schedule.Synchronize(Portals.Select(p => p.Snapshot));
    }
    private void Invalidate(PortalSnapshot? target)
    {
        if (target == null) return;
        var row = Portals.FirstOrDefault(p => p.Snapshot.SameProcess(target)); if (row == null) return;
        row.Snapshot.Status = "Disconnected"; row.Snapshot.SessionId = ""; ReplaceRow(row.Snapshot);
    }
    private async Task SaveSettingsAsync()
    {
        try
        {
            if (!int.TryParse(IntervalText, out int minutes) || !int.TryParse(BackupIntervalText, out int backupMinutes) || !int.TryParse(RetentionText, out int keep)) throw new InvalidDataException("Aralık ve yedek sayısı alanlarına tam sayı yazın.");
            var updated = new ManagerSettings { SaveIntervalMinutes = minutes, BackupIntervalMinutes = backupMinutes, BackupRetention = keep, BackupDirectory = BackupDirectory.Trim(), MinimizeToTray = MinimizeToTray, ShowNotifications = ShowNotifications };
            updated.Validate(); if (string.IsNullOrWhiteSpace(updated.BackupDirectory)) throw new InvalidDataException("Yedek hedefi boş olamaz.");
            await settingsService.SaveAsync(updated); settings = updated;
            schedule.Configure(TimeSpan.FromMinutes(minutes), TimeSpan.FromMinutes(backupMinutes));
            Status = "Ayarlar kaydedildi. Değişen aralıkların sayaçları yeniden başlatıldı."; AddEvent(Status); RefreshLabels();
        }
        catch (Exception e) { Status = "Ayarlar kaydedilemedi: " + e.Message; AddEvent(Status, true); }
    }
    private Task OpenFolderAsync(string path)
    {
        try { if (string.IsNullOrWhiteSpace(path)) throw new IOException("Klasör ayarlanmamış."); Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception e) { AddEvent("Klasör açılamadı: " + e.Message, true); }
        return Task.CompletedTask;
    }
    private Task BrowseBackupFolderAsync()
    {
        dialogOpen = true; UpdateCommands();
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "TIA Auto Save · Backup klasörü", Multiselect = false };
            if (dialog.ShowDialog() == true) BackupDirectory = dialog.FolderName;
        }
        finally { dialogOpen = false; UpdateCommands(); }
        return Task.CompletedTask;
    }
    public void BeginShutdown() { closing = true; schedule.DisableAll(); Status = "Bağlantılar güvenle bırakılıyor…"; RefreshLabels(); }
    public void ReportError(string message) { Status = message; AddEvent(message, true); }
    private string Countdown(PortalSnapshot target, ScheduledOperationKind kind)
    {
        var remaining = schedule.Remaining(target, kind); if (!remaining.HasValue) return "Kapalı";
        if (schedule.IsPaused) return "Pause";
        long seconds = (long)Math.Ceiling(remaining.Value.TotalSeconds);
        return seconds >= 3600 ? $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}" : $"{seconds / 60:00}:{seconds % 60:00}";
    }
    private void RefreshCountdowns()
    {
        foreach (var row in Portals) { var e = schedule.Enabled(row.Snapshot); row.SetSchedule((e.Save ? "S" : "—") + "/" + (e.Backup ? "B" : "—"), Countdown(row.Snapshot, ScheduledOperationKind.Save)); }
        Changed(nameof(SelectedSchedule)); Changed(nameof(LastBackupText));
    }
    private void AddEvent(string text, bool error = false)
    {
        Events.Insert(0, DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  " + text);
        while (Events.Count > 100) Events.RemoveAt(Events.Count - 1);
        if (error) log.Warning("{Message}", text); else log.Information("{Message}", text);
    }
    private void RefreshLabels()
    {
        foreach (string name in new[] { nameof(ActivityText), nameof(InstanceCount), nameof(ConnectedCount), nameof(SelectedPath), nameof(AutomationSummary), nameof(AutomationDetail), nameof(AppliedBackupDirectory), nameof(AutoSaveLabel), nameof(AutoBackupLabel), nameof(PauseLabel), nameof(SelectedSchedule), nameof(LastBackupText) }) Changed(name);
        RefreshCountdowns(); UpdateCommands();
    }
    private void UpdateCommands()
    {
        foreach (var command in new[] { RefreshCommand, ConnectCommand, SaveCommand, BackupCommand, DisconnectCommand, SaveSettingsCommand, ToggleAutoSaveCommand, ToggleBackupCommand, PauseCommand, BrowseBackupFolderCommand }) command?.RaiseCanExecuteChanged();
    }
    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}

public sealed class PortalRow(PortalSnapshot snapshot) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public PortalSnapshot Snapshot { get; } = snapshot;
    public string ProjectName => Snapshot.ProjectName;
    public string Pid => Snapshot.ProcessId.ToString(CultureInfo.InvariantCulture);
    public string Version => Snapshot.Version;
    public string State => Snapshot.Status switch { "Connected" => "Connected", "Detected" => "Detected", "ProjectChanged" => "Proje değişti", "Error" => "Hata", _ => "Disconnected" };
    public string Message => Snapshot.Message;
    public string LastSaved => DateTimeOffset.TryParse(Snapshot.LastSavedUtc, out var time) ? time.ToLocalTime().ToString("HH:mm:ss") : "—";
    public string NextSave { get; private set; } = "Kapalı";
    public string AutoMode { get; private set; } = "—/—";
    public bool CanSave => Snapshot.Status == "Connected" && !string.IsNullOrEmpty(Snapshot.ProjectPath) && !string.IsNullOrEmpty(Snapshot.SessionId);
    public void SetSchedule(string mode, string next) { AutoMode = mode; NextSave = next; PropertyChanged?.Invoke(this, new(nameof(AutoMode))); PropertyChanged?.Invoke(this, new(nameof(NextSave))); }
}
