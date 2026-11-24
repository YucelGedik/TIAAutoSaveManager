using System.Diagnostics;
using NUnit.Framework;
using TIAAutoSaveManager.Contracts;
using TIAAutoSaveManager.Core;
using TIAAutoSaveManager.Infrastructure;
using TIAAutoSaveManager.OpennessHost;

namespace TIAAutoSaveManager.Tests;

public class VersionRoutingTests
{
    private sealed class Client(string version) : IOpennessClient
    {
        public readonly List<HostRequest> Requests = new();
        public bool Fail, Disposed;
        public Task<HostResponse> SendAsync(HostRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new HostResponse { Id = request.Id, Success = !Fail, Outcome = Fail ? "Failed" : "Success",
                Message = version, Portals = request.Operation == "Discover" && !Fail ? new[] { new PortalSnapshot { Version = version, ProcessId = 1 } } : Array.Empty<PortalSnapshot>() });
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
    [Test] public async Task BothVersionsDiscoverAndWritesReachOnlyTheirOwnHost()
    {
        var clients = new Dictionary<string, Client> { ["V20"] = new("V20"), ["V21"] = new("V21") };
        await using var router = new VersionedOpennessClient(() => new[] { "V20", "V21" }, v => clients[v]);
        var discovery = await router.SendAsync(new());
        Assert.That(discovery.Portals.Select(p => p.Version), Is.EqualTo(new[] { "V20", "V21" }));
        await router.SendAsync(new() { Operation = "Save", Target = new() { Version = "V20" } });
        Assert.That(clients["V20"].Requests.Last().Operation, Is.EqualTo("Save"));
        Assert.That(clients["V21"].Requests.Count, Is.EqualTo(1));
        await router.SendAsync(new() { Operation = "Disconnect" });
        Assert.That(clients.Values.All(c => c.Requests.Last().Operation == "Disconnect"), Is.True);
        await router.DisposeAsync();
        Assert.That(clients.Values.All(c => c.Disposed), Is.True);
    }
    [Test] public async Task V20OnlyDoesNotCreateOrRequireAV21Host()
    {
        var client = new Client("V20");
        await using var router = new VersionedOpennessClient(() => new[] { "V20" }, v => { Assert.That(v, Is.EqualTo("V20")); return client; });
        Assert.That((await router.SendAsync(new())).Success, Is.True);
        Assert.That((await router.SendAsync(new() { Operation = "Save", Target = new() { Version = "V21" } })).Success, Is.False);
        Assert.That(client.Requests.Count, Is.EqualTo(1));
    }
    [Test] public async Task OneFailedSdkDoesNotHideTheOtherVersion()
    {
        var clients = new Dictionary<string, Client> { ["V20"] = new("V20"), ["V21"] = new("V21") { Fail = true } };
        await using var router = new VersionedOpennessClient(() => new[] { "V20", "V21" }, v => clients[v]);
        var result = await router.SendAsync(new());
        Assert.That(result.Success, Is.True); Assert.That(result.Portals.Single().Version, Is.EqualTo("V20"));
        Assert.That(result.Message, Does.Contain("V21"));
    }
    [Test] public async Task NoSdkExplainsInstallationAndCannotWrite()
    {
        await using var router = new VersionedOpennessClient(() => Array.Empty<string>(), _ => throw new AssertionException("No host should start."));
        Assert.That((await router.SendAsync(new())).Message, Does.Contain("Openness"));
        Assert.That((await router.SendAsync(new() { Operation = "Save", Target = new() { Version = "V20" } })).Success, Is.False);
    }
    [Test] public void VersionIsPartOfProcessIdentity()
    {
        var first = new PortalSnapshot { ProcessId = 1, StartedUtcTicks = 1, Version = "V20", SessionId = "same", ProjectPath = @"D:\Test.ap20" };
        var other = new PortalSnapshot { ProcessId = 1, StartedUtcTicks = 1, Version = "V21", SessionId = "same", ProjectPath = first.ProjectPath };
        Assert.That(first.SameProcess(other), Is.False); Assert.That(first.SameSelection(other), Is.False);
    }
    [TestCase("V20", ".zap20")][TestCase("V21", ".zap21")]
    public void ArchiveNamesAndValidationUseProjectVersion(string version, string extension)
    {
        var paths = BackupPathPlanner.Create(@"D:\Backups", "Machine", DateTimeOffset.Now, version: version);
        Assert.That(paths.FinalName, Does.EndWith(extension)); Assert.That(paths.StagingName, Is.EqualTo("a" + extension));
        Assert.DoesNotThrow(() => ArchivePathRules.Validate(paths.Directory, paths.FinalName, version));
        Assert.Throws<InvalidDataException>(() => ArchivePathRules.Validate(paths.Directory, "wrong" + (version == "V20" ? ".zap21" : ".zap20"), version));
    }
}

public class VersionedHostEngineTests
{
    // Our own interface mock, not a substitute Siemens assembly. No TIA calls.
    private sealed class Runtime(string version) : IPortalRuntime, IPortalProcess, IPortalConnection, IPortalProject
    {
        public string Version => version;
        public int Id => Environment.ProcessId;
        public string ProjectPath { get; set; } = @"D:\Test\Project.ap" + version[1..];
        public bool IsLost { get; set; }
        public int Saves, Archives, Releases;
        public string Name => "Project";
        public string Path => ProjectPath;
        public IEnumerable<IPortalProcess> GetProcesses() => new[] { this };
        public IPortalConnection Attach() => this;
        public IPortalProject FirstProject() => this;
        public void ReleaseOwnSessions(int ownProcessId) { Assert.That(ownProcessId, Is.EqualTo(Environment.ProcessId)); Releases++; }
        public void Save() => Saves++;
        public void Archive(string directory, string name) { Assert.That(name, Does.EndWith(PortalVersions.ArchiveExtension(version))); Archives++; }
    }
    [TestCase("V20")][TestCase("V21")]
    public void DiscoverAttachSaveBackupAndDisconnectPreserveVersionSafety(string version)
    {
        var runtime = new Runtime(version); var engine = new PortalAdapter(runtime, () => true);
        var detected = engine.Handle(new()).Portals.Single();
        Assert.That(detected.Version, Is.EqualTo(version)); Assert.That(detected.SessionId, Is.Empty);
        var connected = engine.Handle(new() { Operation = "Attach", Target = detected }).Portals.Single();
        Assert.That(connected.Version, Is.EqualTo(version));
        Assert.That(engine.Handle(new() { Operation = "Save", Target = connected }).SaveCompleted, Is.True);
        string directory = System.IO.Path.GetTempPath();
        Assert.That(engine.Handle(new() { Operation = "Backup", Target = connected, ArchiveDirectory = directory,
            ArchiveName = "a" + PortalVersions.ArchiveExtension(version) }).Success, Is.True);
        Assert.That(runtime.Saves, Is.EqualTo(2)); Assert.That(runtime.Archives, Is.EqualTo(1));
        Assert.That(engine.Handle(new() { Operation = "Backup", Target = connected, ArchiveDirectory = directory,
            ArchiveName = "wrong" + (version == "V20" ? ".zap21" : ".zap20") }).Success, Is.False);
        Assert.That(runtime.Saves, Is.EqualTo(2), "Wrong archive extension must fail before Save.");
        runtime.IsLost = true;
        Assert.That(engine.Handle(new() { Operation = "Save", Target = connected }).Success, Is.False);
        runtime.IsLost = false; runtime.ProjectPath = @"D:\Other.ap20";
        Assert.That(engine.Handle(new() { Operation = "Save", Target = connected }).Success, Is.False);
        Assert.That(runtime.Releases, Is.EqualTo(1)); Assert.That(runtime.Saves, Is.EqualTo(2));
        engine.DisconnectAll(); Assert.That(runtime.Releases, Is.EqualTo(1));
    }
    [Test] public void WrongVersionNeverAttachesOrSaves()
    {
        var runtime = new Runtime("V20"); var engine = new PortalAdapter(runtime, () => true);
        Assert.That(engine.Handle(new() { Operation = "Attach", Target = new() { Version = "V21" } }).Outcome, Is.EqualTo("Stale"));
        Assert.That(runtime.Saves, Is.Zero);
    }
}
