using TIAAutoSaveManager.Contracts;

namespace TIAAutoSaveManager.Core;

public interface IOpennessClient : IAsyncDisposable
{
    Task<HostResponse> SendAsync(HostRequest request, CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    Task<SettingsLoadResult> LoadAsync();
    Task SaveAsync(ManagerSettings settings);
}

public sealed record SettingsLoadResult(ManagerSettings Settings, string? Warning = null);

public sealed record BackupResult(bool Success, HostResponse Response, string Message, string ArchivePath = "");
public interface IBackupService
{
    Task<BackupResult> CreateAsync(PortalSnapshot target, ManagerSettings settings, CancellationToken cancellationToken = default);
}

public sealed record ManagerSettings
{
    public int SchemaVersion { get; init; } = 1;
    public int SaveIntervalMinutes { get; init; } = 10;
    public bool MinimizeToTray { get; init; } = true;
    public bool ShowNotifications { get; init; } = true;
    public int BackupIntervalMinutes { get; init; } = 60;
    public int BackupRetention { get; init; } = 10;
    public string BackupDirectory { get; init; } = "";

    public void Validate()
    {
        if (SchemaVersion != 1 || SaveIntervalMinutes is < 1 or > 1440 || BackupIntervalMinutes is < 1 or > 1440)
            throw new InvalidDataException("Ayar sürümü veya kayıt/yedekleme aralığı geçersiz (1–1440 dakika).");
        if (BackupRetention is not (0 or 5 or 10 or 20))
            throw new InvalidDataException("Yedek sayısı 5, 10, 20 veya sınırsız için 0 olmalı.");
        if (!string.IsNullOrWhiteSpace(BackupDirectory) && !Path.IsPathFullyQualified(BackupDirectory))
            throw new InvalidDataException("Yedek klasörü tam yol olmalı.");
    }
}

// A single gate covers discovery, connection, manual Save, and future scheduler/Archive.
// Cancellation applies before dispatch only: it cannot abort a synchronous Siemens call.
public sealed class ProjectOperationCoordinator(IOpennessClient client)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<HostResponse> ExecuteAsync(HostRequest request, CancellationToken cancellationToken = default)
    {
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return new HostResponse { Id = request.Id, Outcome = "Busy", Message = "Başka bir TIA işlemi devam ediyor." };
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
}
