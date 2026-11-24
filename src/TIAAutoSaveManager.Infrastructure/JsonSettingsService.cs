using System.Text.Json;
using TIAAutoSaveManager.Core;

namespace TIAAutoSaveManager.Infrastructure;

public sealed class JsonSettingsService(string directory) : ISettingsService
{
    private string FilePath => Path.Combine(directory, "settings.json");
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<SettingsLoadResult> LoadAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (!File.Exists(FilePath)) return new(new());
            try
            {
                using var input = File.OpenRead(FilePath);
                var settings = await JsonSerializer.DeserializeAsync<ManagerSettings>(input)
                    ?? throw new InvalidDataException("Ayar dosyası boş.");
                settings.Validate();
                return new(settings);
            }
            catch (Exception e) when (e is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                string detail = e.Message;
                try { File.Copy(FilePath, FilePath + ".invalid-" + Guid.NewGuid().ToString("N")); }
                catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException)
                { detail += " Yedek oluşturulamadı: " + copyError.Message; }
                return new(new(), "Ayarlar yüklenemedi; varsayılanlar kullanılıyor. " + detail);
            }
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(ManagerSettings settings)
    {
        settings.Validate();
        await gate.WaitAsync();
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(output, settings, new JsonSerializerOptions { WriteIndented = true });
                await output.FlushAsync();
                output.Flush(flushToDisk: true);
            }
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
            else File.Move(temporary, FilePath);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            finally { gate.Release(); }
        }
    }
}
