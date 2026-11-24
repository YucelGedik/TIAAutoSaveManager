using Microsoft.Win32;
using Serilog;
using TIAAutoSaveManager.Contracts;
using TIAAutoSaveManager.Core;

namespace TIAAutoSaveManager.Infrastructure;

public sealed class VersionedOpennessClient : IOpennessClient
{
    private readonly Func<IReadOnlyList<string>> installedVersions;
    private readonly Func<string, IOpennessClient> factory;
    private readonly Dictionary<string, IOpennessClient> clients = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool disposed;

    public VersionedOpennessClient(string hostPath, ILogger log)
        : this(FindInstalledVersions, version => new OpennessHostClient(hostPath, log, version)) { }
    public VersionedOpennessClient(Func<IReadOnlyList<string>> installedVersions, Func<string, IOpennessClient> factory)
    { this.installedVersions = installedVersions; this.factory = factory; }

    public async Task<HostResponse> SendAsync(HostRequest request, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (request.Operation == "Disconnect")
            {
                foreach (var client in clients.Values)
                {
                    var response = await client.SendAsync(new() { Operation = "Disconnect" }, cancellationToken).ConfigureAwait(false);
                    if (!response.Success) return new() { Id = request.Id, Outcome = response.Outcome, Message = response.Message };
                }
                return new() { Id = request.Id, Success = true, Outcome = "Success", Message = "V20/V21 bağlantıları bırakıldı; kullanıcı projeleri kapatılmadı." };
            }
            var versions = installedVersions().Where(PortalVersions.IsSupported).Distinct().ToArray();
            if (request.Operation != "Discover")
            {
                var version = request.Target?.Version;
                if (version == null || !versions.Contains(version))
                    return new() { Id = request.Id, Outcome = "Stale", Message = "Seçili TIA sürümünün Openness kurulumu bulunamadı. Yenileyip doğru projeye bağlanın." };
                return await Client(version).SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            var portals = new List<PortalSnapshot>(); var messages = new List<string>(); int successes = 0;
            foreach (var version in versions)
            {
                try
                {
                    var response = await Client(version).SendAsync(new() { Operation = "Discover" }, cancellationToken).ConfigureAwait(false);
                    messages.Add(version + ": " + response.Message);
                    if (!response.Success) continue;
                    if (response.Portals.Any(p => p.Version != version)) throw new InvalidDataException("Host sürümü ile keşif sonucu eşleşmiyor.");
                    portals.AddRange(response.Portals); successes++;
                }
                catch (Exception error) when (error is IOException or TimeoutException or InvalidOperationException or UnauthorizedAccessException)
                { messages.Add(version + ": " + error.Message); }
            }
            return new() { Id = request.Id, Success = successes > 0, Outcome = successes > 0 ? "Success" : "Failed",
                Portals = portals.ToArray(), Message = versions.Length == 0
                    ? "V20/V21 Openness kurulumu bulunamadı. Siemens kurulumundan kullandığınız sürümün Openness bileşenini ekleyin."
                    : string.Join(" | ", messages) };
        }
        finally { gate.Release(); }
    }
    private IOpennessClient Client(string version)
    {
        if (!clients.TryGetValue(version, out var client)) clients.Add(version, client = factory(version));
        return client;
    }
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try { if (disposed) return; disposed = true; foreach (var client in clients.Values) await client.DisposeAsync().ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    private static IReadOnlyList<string> FindInstalledVersions()
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        var versions = new List<string>();
        using var v20 = root.OpenSubKey(@"SOFTWARE\Siemens\Automation\Openness\20.0\PublicAPI\20.0.0.0");
        if (v20 != null) versions.Add("V20");
        using var v21 = root.OpenSubKey(@"SOFTWARE\Siemens\Automation\Openness\21.0\PublicAPI\21.0.0.0\net48");
        if (v21 != null) versions.Add("V21");
        return versions;
    }
}
