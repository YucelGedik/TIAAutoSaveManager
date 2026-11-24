using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;
using TIAAutoSaveManager.Contracts;
using TIAAutoSaveManager.Core;

namespace TIAAutoSaveManager.Infrastructure;

public sealed class ProjectBackupService(ProjectOperationCoordinator coordinator, ILogger log, TimeProvider? timeProvider = null) : IBackupService
{
    private sealed record Receipt(int Schema, string ProjectKey, string ArchiveFile, string Sha256, long Bytes, DateTimeOffset CreatedUtc);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<BackupResult> CreateAsync(PortalSnapshot target, ManagerSettings settings, CancellationToken cancellationToken = default)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
            return new(false, new() { Outcome = "Busy" }, "Başka bir yedekleme devam ediyor.");
        HostResponse response = new() { Outcome = "BackupFailed" };
        try
        {
            settings.Validate();
            if (!target.SameSelection(target) || target.Status != "Connected") throw new InvalidOperationException("Önce açık projeye bağlanın.");
            string root = settings.BackupDirectory;
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidDataException("Yedek klasörünü seçin.");
            root = Path.GetFullPath(root);
            string source = Path.GetDirectoryName(Path.GetFullPath(target.ProjectPath))!;
            if (IsWithin(root, source)) throw new InvalidDataException("Yedek klasörü açık projenin içinde olamaz.");
            string projectKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(target.ProjectPath).ToUpperInvariant())))[..16];
            string projectName = SafeName(target.ProjectName);
            var localTime = clock.GetLocalNow();
            var paths = BackupPathPlanner.Create(root, projectName, localTime, version: target.Version);
            EnsureSafeDirectory(root);
            var drive = new DriveInfo(Path.GetPathRoot(root)!);
            if (!drive.IsReady || drive.AvailableFreeSpace < 256L * 1024 * 1024)
                throw new IOException("Yedek hedefinde en az 256 MB boş alan gerekiyor. Büyük projeler daha fazla alan gerektirebilir.");
            string staging = paths.StagingDirectory;
            Directory.CreateDirectory(staging);
            cancellationToken.ThrowIfCancellationRequested();
            response = await coordinator.ExecuteAsync(new() { Operation = "Backup", Target = target, ArchiveDirectory = staging, ArchiveName = paths.StagingName }, cancellationToken);
            if (!response.Success) return new(false, response, "Yedek oluşturulamadı: " + response.Message);
            string stagedArchive = Path.Combine(staging, paths.StagingName);
            if (!string.Equals(Path.GetFullPath(response.ArchivePath), stagedArchive, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Host beklenen arşiv yolunu döndürmedi.");
            // No cancellation after a native call has completed: finish validation/commit or report failure.
            var receipt = await Task.Run(() => ValidateArchive(stagedArchive, projectKey, paths.FinalName));
            string final = await Task.Run(() => Commit(root, projectName, localTime, stagedArchive, receipt, target.Version));
            try { Directory.Delete(staging); } // Only our staging directory, never recursive.
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { log.Warning(e, "Archive committed; staging cleanup incomplete {Directory}", staging); }
            string warning = await Task.Run(() => Rotate(root, projectKey, final, settings.BackupRetention));
            log.Information("Backup committed {ProjectKey} {Archive} {Bytes} bytes {Sha256}", projectKey, final, receipt.Bytes, receipt.Sha256);
            return new(true, response, "Yedek doğrulandı: " + final + warning, final);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException or JsonException)
        {
            log.Error(e, "Backup failed for PID {Pid}; old backups retained", target.ProcessId);
            // Native Save can have succeeded even when local archive verification fails.
            response.Success = false;
            if (response.Outcome != "Uncertain") response.Outcome = "BackupFailed";
            return new(false, response, "Yedekleme başarısız; eski yedekler korunuyor. " + e.Message);
        }
        finally { gate.Release(); }
    }

    private static string Commit(string root, string projectName, DateTimeOffset localTime, string stagedArchive, Receipt receipt, string version)
    {
        EnsureSafeDirectory(root);
        // Only the short local commit is locked across processes; native calls are never repeated.
        using var commitLock = new FileStream(Path.Combine(root,".tia-autosave-commit.lock"), FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
        for (int index = 1; index <= 9999; index++)
        {
            var paths = BackupPathPlanner.Create(root, projectName, localTime, index, version);
            if (Directory.Exists(paths.Directory) || File.Exists(paths.Directory)) continue;
            EnsureSafeDirectory(paths.Directory);
            string final = Path.Combine(paths.Directory, paths.FinalName);
            string temporaryReceipt = Path.Combine(Path.GetDirectoryName(stagedArchive)!,"receipt.json");
            try
            {
                File.WriteAllText(temporaryReceipt, JsonSerializer.Serialize(receipt with { ArchiveFile = paths.FinalName }, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(stagedArchive, final); // Never overwrite another backup, regardless of minute/project name.
                File.Move(temporaryReceipt, final + ".manifest.json");
                return final;
            }
            catch
            {
                if (!Directory.EnumerateFileSystemEntries(paths.Directory).Any()) Directory.Delete(paths.Directory);
                throw;
            }
        }
        throw new IOException("Bu dakika için kullanılabilir yedek adı bulunamadı; mevcut dosyalar değiştirilmedi.");
    }

    private static Receipt ValidateArchive(string path, string key, string name)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length == 0 || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Arşiv yok, boş veya sembolik bağlantı.");
        using (var input = File.OpenRead(path))
        using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
        {
            if (zip.Entries.Count == 0) throw new InvalidDataException("Arşiv içeriği boş.");
            foreach (var entry in zip.Entries)
            {
                using var content = entry.Open();
                content.CopyTo(Stream.Null); // Exercise decompression; no extraction to disk.
            }
        }
        using var hashed = File.OpenRead(path);
        return new(1, key, name, Convert.ToHexString(SHA256.HashData(hashed)), info.Length, DateTimeOffset.UtcNow);
    }

    private string Rotate(string root, string key, string newestArchive, int keep)
    {
        if (keep == 0) return "";
        try
        {
            var managed = new List<(Receipt Receipt, string Manifest, string Directory)>();
            // One level only: dated backups and legacy project-hash folders. Never scan retrieved projects.
            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                if (Path.GetFileName(directory).StartsWith(".pending-", StringComparison.OrdinalIgnoreCase) ||
                    (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (var file in Directory.EnumerateFiles(directory, "*.manifest.json"))
                {
                    try
                    {
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                        var receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(file));
                        if (receipt == null || receipt.Schema != 1 || receipt.ProjectKey != key || string.IsNullOrWhiteSpace(receipt.ArchiveFile) ||
                            string.IsNullOrWhiteSpace(receipt.Sha256) || receipt.Sha256.Length != 64 || receipt.Bytes <= 0 ||
                            Path.GetFileName(receipt.ArchiveFile) != receipt.ArchiveFile || !PortalVersions.IsArchive(receipt.ArchiveFile) ||
                            !string.Equals(file, Path.Combine(directory, receipt.ArchiveFile) + ".manifest.json", StringComparison.OrdinalIgnoreCase) ||
                            !File.Exists(Path.Combine(directory, receipt.ArchiveFile))) continue;
                        managed.Add((receipt, file, directory));
                    }
                    catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { log.Warning(e, "Ignoring invalid backup receipt {Path}", file); }
                }
            }
            // Always protect this operation's newly committed archive even if the wall clock moved back.
            foreach (var old in managed.OrderByDescending(p => string.Equals(Path.Combine(p.Directory,p.Receipt.ArchiveFile),newestArchive,StringComparison.OrdinalIgnoreCase)).ThenByDescending(p => p.Receipt.CreatedUtc).Skip(keep))
            {
                EnsureSafeDirectory(old.Directory);
                string archive = Path.Combine(old.Directory, old.Receipt.ArchiveFile);
                if ((File.GetAttributes(archive) & FileAttributes.ReparsePoint) != 0) continue;
                using (var input = File.OpenRead(archive))
                    if (Convert.ToHexString(SHA256.HashData(input)) != old.Receipt.Sha256) continue; // Changed/foreign file: preserve.
                File.Delete(archive);
                File.Delete(old.Manifest);
                log.Information("Backup rotation removed {Archive}", archive);
                if (!Directory.EnumerateFileSystemEntries(old.Directory).Any()) Directory.Delete(old.Directory);
            }
            return "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            log.Warning(e, "Backup committed, retention incomplete");
            return " · Yedek başarılı; eski yedek temizliği tamamlanamadı: " + e.Message;
        }
    }

    private static bool IsWithin(string child, string parent)
    {
        string relative = Path.GetRelativePath(parent, child);
        return relative == "." || (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static void EnsureSafeDirectory(string path)
    {
        // Reject junction/symlink ancestors, including a user-changed backup target.
        for (var current = new DirectoryInfo(path); current != null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Yedek hedefinde sembolik bağlantı/junction kullanılamaz: " + current.FullName);
        Directory.CreateDirectory(path);
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        string cleaned = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (string.IsNullOrEmpty(cleaned)) cleaned = "Project";
        return cleaned[..Math.Min(cleaned.Length, 60)];
    }
}
