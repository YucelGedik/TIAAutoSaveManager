using TIAAutoSaveManager.Contracts;
using System.Globalization;

namespace TIAAutoSaveManager.Infrastructure;

public sealed record BackupPaths(string Directory, string FinalName, string StagingDirectory, string StagingName);

public static class BackupPathPlanner
{
    public static BackupPaths Create(string root, string safeProjectName, DateTimeOffset localTime, int collisionIndex = 1, string version = "V21")
    {
        if (collisionIndex < 1) throw new ArgumentOutOfRangeException(nameof(collisionIndex));
        root = Path.GetFullPath(root);
        string suffix = "_" + localTime.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) +
            (collisionIndex == 1 ? "" : "_" + collisionIndex.ToString("D2", CultureInfo.InvariantCulture));
        // Folder and file share the same human-readable stem. Identity stays in receipts only.
        int prefixBudget = (ArchivePathRules.MaximumPathLength - root.TrimEnd(Path.DirectorySeparatorChar).Length - 2 - 6) / 2 - suffix.Length;
        if (prefixBudget < 1)
            throw new InvalidDataException("Backup kök klasörü TIA'nın 143 karakterlik arşiv sınırı için çok uzun. " +
                "Daha kısa bir klasör seçin (ör. C:\\TIA_Backups).");
        string folder = safeProjectName[..Math.Min(safeProjectName.Length, prefixBudget)] + suffix;
        string directory = Path.Combine(root, folder);
        string extension = PortalVersions.ArchiveExtension(version);
        string name = folder + extension;
        // Native Archive sees only a short name in a staging folder directly under the chosen root.
        // The descriptive final name is assigned after native completion and verification.
        string staging = Path.Combine(root, ".pending-" + Guid.NewGuid().ToString("N")[..16]);
        string stagingName = "a" + extension;
        ArchivePathRules.Validate(staging, stagingName, version);
        ArchivePathRules.Validate(directory, name, version);
        return new(directory, name, staging, stagingName);
    }
}
