using System;
using System.IO;

namespace TIAAutoSaveManager.Contracts
{
    public static class PortalVersions
    {
        public static bool IsSupported(string version) => version == "V20" || version == "V21";
        public static string ArchiveExtension(string version)
        {
            if (!IsSupported(version)) throw new InvalidDataException("Desteklenmeyen TIA sürümü: " + version);
            return version == "V20" ? ".zap20" : ".zap21";
        }
        public static bool IsArchive(string name) => !string.IsNullOrEmpty(name) &&
            (name.EndsWith(".zap20", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".zap21", StringComparison.OrdinalIgnoreCase));
    }
}
