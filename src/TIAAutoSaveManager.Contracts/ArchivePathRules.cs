using System;
using System.IO;

namespace TIAAutoSaveManager.Contracts
{
    public static class ArchivePathRules
    {
        // V21 Archive reported a 143-character limit in the user's failing call.
        // Count the extension as well, conservatively, rather than rely on Win32's longer limit.
        public const int MaximumPathLength = 143;

        public static void Validate(string directory, string name, string version = "V21")
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory) || string.IsNullOrWhiteSpace(name) ||
                Path.GetFileName(name) != name || !name.EndsWith(PortalVersions.ArchiveExtension(version), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Arşiv hedefi geçersiz.");
            int length = Path.GetFullPath(Path.Combine(directory, name)).Length;
            if (length > MaximumPathLength)
                throw new InvalidDataException("TIA arşiv yolu " + length + " karakter; güvenli sınır " + MaximumPathLength +
                    ". Daha kısa bir backup klasörü seçin (ör. C:\\TIA_Backups). Kayıt/arşiv çağrısı yapılmadı.");
        }
    }
}
