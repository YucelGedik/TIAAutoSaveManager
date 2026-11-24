using NUnit.Framework;
using Serilog;
using TIAAutoSaveManager.Contracts;
using TIAAutoSaveManager.Core;
using TIAAutoSaveManager.Infrastructure;

namespace TIAAutoSaveManager.Tests;

public class CoordinatorTests
{
    private sealed class HeldClient : IOpennessClient
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<HostResponse> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public Task<HostResponse> SendAsync(HostRequest request, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Calls); Started.TrySetResult(); return Completion.Task; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Test]
    public async Task LongSavePreventsSecondSaveAndDoesNotQueueIt()
    {
        var client = new HeldClient(); var coordinator = new ProjectOperationCoordinator(client);
        var first = coordinator.ExecuteAsync(new() { Operation = "Save" });
        await client.Started.Task;
        var second = await coordinator.ExecuteAsync(new() { Operation = "Save" });
        Assert.That(second.Outcome, Is.EqualTo("Busy"));
        Assert.That(client.Calls, Is.EqualTo(1));
        client.Completion.SetResult(new() { Success = true }); await first;
        Assert.That(client.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task CancellationAfterDispatchDoesNotReleaseGateEarly()
    {
        var client = new HeldClient(); var coordinator = new ProjectOperationCoordinator(client);
        using var cancellation = new CancellationTokenSource();
        var first = coordinator.ExecuteAsync(new() { Operation = "Save" }, cancellation.Token);
        await client.Started.Task; cancellation.Cancel();
        Assert.That((await coordinator.ExecuteAsync(new() { Operation = "Attach" })).Outcome, Is.EqualTo("Busy"));
        client.Completion.SetResult(new() { Success = true });
        Assert.That((await first).Success, Is.True);
    }

    [Test]
    public void CancellationBeforeDispatchMakesNoApiCall()
    {
        var client = new HeldClient(); var coordinator = new ProjectOperationCoordinator(client);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.That(async () => await coordinator.ExecuteAsync(new(), cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(client.Calls, Is.Zero);
    }

    [Test]
    public void GateIsReleasedAfterConnectionFailure()
    {
        var client = new HeldClient(); var coordinator = new ProjectOperationCoordinator(client);
        var first = coordinator.ExecuteAsync(new());
        client.Completion.SetException(new IOException("Connection lost"));
        Assert.ThrowsAsync<IOException>(async () => await first);
        Assert.ThrowsAsync<IOException>(async () => await coordinator.ExecuteAsync(new()));
        Assert.That(client.Calls, Is.EqualTo(2));
    }
}

public class IdentityAndProtocolTests
{
    private static PortalSnapshot Target() => new() { ProcessId = 42, StartedUtcTicks = 100, SessionId = "one", ProjectPath = @"D:\Test\Test.ap21" };

    [TestCase("pid")][TestCase("start")][TestCase("session")][TestCase("project")]
    public void ChangedIdentityCannotInheritSaveSelection(string change)
    {
        var original = Target(); var changed = Target();
        if (change == "pid") changed.ProcessId++;
        if (change == "start") changed.StartedUtcTicks++;
        if (change == "session") changed.SessionId = "two";
        if (change == "project") changed.ProjectPath = @"D:\Another\Another.ap21";
        Assert.That(original.SameSelection(changed), Is.False);
    }

    [Test]
    public void EmptyProjectOrUnattachedSessionCannotBeSaved()
    {
        var first = Target(); first.ProjectPath = "";
        Assert.That(first.SameSelection(first), Is.False);
        first = Target(); first.SessionId = "";
        Assert.That(first.SameSelection(first), Is.False);
    }

    [Test]
    public void WireRoundtripPreservesUnicodeAndSelectedIdentity()
    {
        using var buffer = new MemoryStream();
        var request = new HostRequest { Operation = "Save", Target = Target() };
        request.Target.ProjectName = "Ölçüm İstasyonu";
        WireProtocol.Write(buffer, request); buffer.Position = 0;
        var result = WireProtocol.Read<HostRequest>(buffer);
        Assert.That(result.Target.SameSelection(request.Target), Is.True);
        Assert.That(result.Target.ProjectName, Is.EqualTo("Ölçüm İstasyonu"));
    }

    [Test]
    public void OversizedFrameIsRejectedBeforeAllocation()
    {
        using var buffer = new MemoryStream(BitConverter.GetBytes(WireProtocol.MaximumBytes + 1));
        Assert.Throws<InvalidDataException>(() => WireProtocol.Read<HostRequest>(buffer));
    }

    [Test]
    public void TruncatedFrameIsNotAcceptedAsSuccessfulResponse()
    {
        using var buffer = new MemoryStream(new byte[] { 20, 0, 0, 0, 1 });
        Assert.Throws<EndOfStreamException>(() => WireProtocol.Read<HostResponse>(buffer));
    }
}

public class SettingsTests
{
    private string directory = "";
    [SetUp] public void Setup() { directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "settings-test-data", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
    [TearDown] public void Cleanup() { Directory.Delete(directory, true); }

    [Test]
    public async Task SettingsSurviveRestartAndAtomicReplacement()
    {
        var settings = new JsonSettingsService(directory);
        await settings.SaveAsync(new() { SaveIntervalMinutes = 15 });
        await settings.SaveAsync(new() { SaveIntervalMinutes = 30, MinimizeToTray = false });
        var result = await new JsonSettingsService(directory).LoadAsync();
        Assert.That(result.Settings.SaveIntervalMinutes, Is.EqualTo(30));
        Assert.That(result.Settings.MinimizeToTray, Is.False);
        Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
    }

    [TestCase("{broken")][TestCase("null")][TestCase("{\"SaveIntervalMinutes\":0}")][TestCase("{\"SchemaVersion\":99}")]
    public async Task InvalidSettingsRecoverToDefaultsAndPreserveOriginal(string json)
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), json);
        var result = await new JsonSettingsService(directory).LoadAsync();
        Assert.That(result.Settings.SaveIntervalMinutes, Is.EqualTo(10));
        Assert.That(result.Warning, Is.Not.Null);
        Assert.That(Directory.GetFiles(directory, "*.invalid-*"), Has.Length.EqualTo(1));
        Assert.That(await File.ReadAllTextAsync(Path.Combine(directory, "settings.json")), Is.EqualTo(json));
    }

    [Test]
    public void InvalidIntervalDoesNotOverwriteValidFile()
    {
        var service = new JsonSettingsService(directory);
        Assert.ThrowsAsync<InvalidDataException>(async () => await service.SaveAsync(new() { SaveIntervalMinutes = -1 }));
        Assert.That(File.Exists(Path.Combine(directory, "settings.json")), Is.False);
    }
}

public class RealV21ReadOnlyTests
{
    [Test, Category("ReadOnlyV21")]
    public async Task VersionRouterDiscoversRealV21AndV20WithoutSdkFailsClearly()
    {
        string? host = Environment.GetEnvironmentVariable("TIAAUTOSAVE_TEST_HOST");
        if (string.IsNullOrEmpty(host)) { Assert.Ignore("Set TIAAUTOSAVE_TEST_HOST for read-only checks."); return; }
        using var logger = new LoggerConfiguration().CreateLogger();
        await using var router = new VersionedOpennessClient(host, logger);
        var discovery = await router.SendAsync(new());
        Assert.That(discovery.Success, Is.True, discovery.Message);
        Assert.That(discovery.Portals.All(p => PortalVersions.IsSupported(p.Version) && p.SessionId == ""), Is.True);
        using var registry = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
        using var v20 = registry.OpenSubKey(@"SOFTWARE\Siemens\Automation\Openness\20.0\PublicAPI\20.0.0.0");
        if (v20 == null)
        {
            await using var client = new OpennessHostClient(host, logger, "V20");
            var missing = await client.SendAsync(new());
            Assert.That(missing.Success, Is.False);
            Assert.That(missing.Message, Does.Contain("V20").And.Not.Contain("V21"));
            TestContext.Out.WriteLine("V20 absent: " + missing.Message);
        }
    }
    [Test, Category("ReadOnlyV21")]
    public async Task InstalledV21CanDiscoverWithoutAttachingOrSaving()
    {
        string? host = Environment.GetEnvironmentVariable("TIAAUTOSAVE_TEST_HOST");
        if (string.IsNullOrEmpty(host)) { Assert.Ignore("Set TIAAUTOSAVE_TEST_HOST to opt into real V21 read-only discovery."); return; }
        using var logger = new LoggerConfiguration().CreateLogger();
        await using var client = new OpennessHostClient(host, logger);
        var result = await client.SendAsync(new() { Operation = "Discover" });
        TestContext.Out.WriteLine(result.Message);
        TestContext.Out.WriteLine("Portals: " + result.Portals.Length);
        foreach (var portal in result.Portals)
            TestContext.Out.WriteLine($"PID {portal.ProcessId}; {portal.Status}; {portal.ProjectPath}; {portal.Message}");
        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Portals.All(p => p.Status != "Error"), Is.True, "Instance metadata must be readable.");
        Assert.That(result.Portals.All(p => p.SessionId == ""), Is.True, "Discovery must never attach.");
    }
}
