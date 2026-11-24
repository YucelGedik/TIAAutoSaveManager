using System.IO.Compression;
using NUnit.Framework;
using Serilog;
using TIAAutoSaveManager.Contracts;
using TIAAutoSaveManager.Core;
using TIAAutoSaveManager.Infrastructure;

namespace TIAAutoSaveManager.Tests;

public class ScheduleTests
{
    private sealed class Clock : TimeProvider
    {
        public long Ticks;
        public DateTimeOffset Wall = DateTimeOffset.UtcNow;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Ticks;
        public override DateTimeOffset GetUtcNow() => Wall;
        public void Advance(int seconds) => Ticks += TimeSpan.FromSeconds(seconds).Ticks;
    }
    private Clock clock = null!;
    private AutoSaveSchedule schedule = null!;
    private PortalSnapshot target = null!;
    private static PortalSnapshot Target(string id = "one") => new() { ProcessId = id == "one" ? 1 : 2, StartedUtcTicks = 100, SessionId = id, Status = "Connected", ProjectPath = @"D:\Test\" + id + ".ap21" };
    [SetUp] public void Setup() { clock = new(); schedule = new(clock); target = Target(); schedule.Configure(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)); }

    [Test] public void StartsDisabledAndFirstSaveWaitsForInterval()
    {
        schedule.Synchronize(new[] { target }); clock.Advance(600); Assert.That(schedule.TakeDue(), Is.Null);
        schedule.Enable(target, true, false); clock.Advance(59); Assert.That(schedule.TakeDue(), Is.Null);
        clock.Advance(1); Assert.That(schedule.TakeDue()!.Kind, Is.EqualTo(ScheduledOperationKind.Save));
    }
    [Test] public void LongSaveHasNoOverlappingOrAccumulatingTicks()
    {
        schedule.Enable(target, true, false); clock.Advance(60); var work = schedule.TakeDue()!;
        clock.Advance(3600); Assert.That(schedule.TakeDue(), Is.Null);
        schedule.Complete(work, true); Assert.That(schedule.TakeDue(), Is.Null);
        Assert.That(schedule.Remaining(target, ScheduledOperationKind.Save), Is.EqualTo(TimeSpan.FromMinutes(1)));
    }
    [Test] public void PauseResumeStartsFreshWithoutCatchup()
    {
        schedule.Enable(target, true, true); clock.Advance(59); schedule.SetPaused(true); clock.Advance(3600);
        Assert.That(schedule.TakeDue(), Is.Null); schedule.SetPaused(false); Assert.That(schedule.TakeDue(), Is.Null);
        clock.Advance(60); Assert.That(schedule.TakeDue()!.Kind, Is.EqualTo(ScheduledOperationKind.Save));
    }
    [Test] public void DisableStopsSave()
    {
        schedule.Enable(target, true, false); schedule.Enable(target, false, false); clock.Advance(600); Assert.That(schedule.TakeDue(), Is.Null);
    }
    [TestCase("session")][TestCase("path")][TestCase("pid")][TestCase("start")][TestCase("status")]
    public void ChangedIdentityCannotInheritEnabledState(string change)
    {
        schedule.Enable(target, true, true); var changed = Target();
        if (change == "session") changed.SessionId = "new";
        if (change == "path") changed.ProjectPath = @"D:\Other.ap21";
        if (change == "pid") changed.ProcessId++;
        if (change == "start") changed.StartedUtcTicks++;
        if (change == "status") changed.Status = "Disconnected";
        schedule.Synchronize(new[] { changed }); clock.Advance(600);
        Assert.That(schedule.EnabledCount, Is.Zero); Assert.That(schedule.TakeDue(), Is.Null);
    }
    [Test] public void OnlyExplicitlyEnabledInstanceIsSaved()
    {
        var second = Target("two"); schedule.Synchronize(new[] { target, second }); schedule.Enable(second, true, false); clock.Advance(60);
        Assert.That(schedule.TakeDue()!.Target.ProcessId, Is.EqualTo(2));
    }
    [Test] public void BackupFailureDoesNotSuppressDueRegularSave()
    {
        schedule.Enable(target, true, true); clock.Advance(120); var backup = schedule.TakeDue()!;
        Assert.That(backup.Kind, Is.EqualTo(ScheduledOperationKind.Backup)); schedule.Complete(backup, false);
        Assert.That(schedule.TakeDue()!.Kind, Is.EqualTo(ScheduledOperationKind.Save));
    }
    [Test] public void BackupWithSuccessfulSaveResetsBothIntervals()
    {
        schedule.Enable(target, true, true); clock.Advance(120); schedule.Complete(schedule.TakeDue()!, true);
        Assert.That(schedule.TakeDue(), Is.Null);
        Assert.That(schedule.Remaining(target, ScheduledOperationKind.Save), Is.EqualTo(TimeSpan.FromMinutes(1)));
    }
    [Test] public void ManualSaveResetsAutomaticCountdown()
    {
        schedule.Enable(target, true, false); clock.Advance(59); schedule.Complete(new(target, ScheduledOperationKind.Save), true); clock.Advance(1);
        Assert.That(schedule.TakeDue(), Is.Null);
    }
    [Test] public void ChangingIntervalResetsExistingCountdown()
    {
        schedule.Enable(target, true, false); clock.Advance(59); schedule.Configure(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));
        Assert.That(schedule.Remaining(target, ScheduledOperationKind.Save), Is.EqualTo(TimeSpan.FromMinutes(5)));
    }
    [Test] public void WallClockChangeDoesNotChangeDueTime()
    {
        schedule.Enable(target, true, false); clock.Wall = clock.Wall.AddDays(-1); clock.Advance(60);
        Assert.That(schedule.TakeDue(), Is.Not.Null);
    }
    [Test] public void ChangingSaveIntervalDoesNotPostponeIndependentBackup()
    {
        schedule.Enable(target, true, true); clock.Advance(119);
        schedule.Configure(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(2)); clock.Advance(1);
        Assert.That(schedule.TakeDue()!.Kind, Is.EqualTo(ScheduledOperationKind.Backup));
    }
    [Test] public void StaleCompletionCannotReactivateNewSession()
    {
        schedule.Enable(target, true, false); clock.Advance(60); var old = schedule.TakeDue()!; schedule.DisableAll();
        var newTarget = Target("new"); schedule.Enable(newTarget, true, false); schedule.Complete(old, true);
        Assert.That(schedule.Enabled(newTarget).Save, Is.True); Assert.That(schedule.TakeDue(), Is.Null);
    }
}

public class BackupTests
{
    private sealed class NamingClock : TimeProvider
    {
        public DateTimeOffset Utc = new(2026,11,20,5,35,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Utc;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("TestLocal",TimeSpan.FromHours(3),"TestLocal","TestLocal");
    }
    private sealed class ArchiveClient : IOpennessClient
    {
        public string Mode = "valid";
        public int Calls;
        public HostRequest? LastRequest;
        public Task<HostResponse> SendAsync(HostRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            Assert.That(request.Operation, Is.EqualTo("Backup"));
            // Model the real native constraint missed by the earlier mock.
            ArchivePathRules.Validate(request.ArchiveDirectory, request.ArchiveName);
            string path = Path.Combine(request.ArchiveDirectory, request.ArchiveName);
            if (Mode == "valid")
            {
                using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
                using var writer = new StreamWriter(zip.CreateEntry("System/test-data.txt").Open()); writer.Write("Test-only archive; not a TIA project.");
            }
            else File.WriteAllText(path, "partial/corrupt");
            return Task.FromResult(new HostResponse { Id = request.Id, Success = Mode != "native-failure", SaveCompleted = true,
                Outcome = Mode == "native-failure" ? "BackupFailed" : "Success", ArchivePath = path, Portals = new[] { request.Target }, Message = "Test adapter response" });
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private string root = null!;
    private ArchiveClient client = null!;
    private ProjectBackupService service = null!;
    private ManagerSettings settings = null!;
    private PortalSnapshot target = null!;
    private Serilog.Core.Logger logger = null!;
    private NamingClock namingClock = null!;
    [SetUp] public void Setup()
    {
        root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "artifacts", "bt", Guid.NewGuid().ToString("N")[..8]));
        Directory.CreateDirectory(root); client = new(); logger = new LoggerConfiguration().CreateLogger();
        namingClock = new(); service = new(new(client), logger, namingClock); settings = new() { BackupDirectory = Path.Combine(root, "backups"), BackupRetention = 5 };
        target = new() { ProcessId = 1, StartedUtcTicks = 100, SessionId = "one", ProjectName = "Machine", ProjectPath = Path.Combine(root, "source", "Machine.ap21"), Status = "Connected" };
    }
    [TearDown] public void Cleanup() { logger.Dispose(); Directory.Delete(root, true); }
    private string[] Archives() => Directory.Exists(settings.BackupDirectory) ? Directory.GetFiles(settings.BackupDirectory, "*.zap21", SearchOption.AllDirectories).Where(p => !p.Contains(".pending-")).ToArray() : Array.Empty<string>();

    [Test] public async Task SuccessfulArchiveIsValidatedAndHasManifest()
    {
        var result = await service.CreateAsync(target, settings);
        Assert.That(result.Success, Is.True, result.Message); Assert.That(File.Exists(result.ArchivePath + ".manifest.json"), Is.True);
        Assert.That(Directory.GetDirectories(settings.BackupDirectory, ".pending-*", SearchOption.AllDirectories), Is.Empty);
    }
    [Test] public async Task LongProjectUsesShortNativeNameAndReadableFinalName()
    {
        target.ProjectName = "Demo_Press_19112026_V11";
        var result = await service.CreateAsync(target, settings);
        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(client.LastRequest!.ArchiveName, Is.EqualTo("a.zap21"));
        Assert.That(Path.GetDirectoryName(client.LastRequest.ArchiveDirectory), Is.EqualTo(settings.BackupDirectory));
        Assert.That(Path.Combine(client.LastRequest.ArchiveDirectory, client.LastRequest.ArchiveName).Length, Is.LessThanOrEqualTo(143));
        Assert.That(result.ArchivePath.Length, Is.LessThanOrEqualTo(143));
        Assert.That(Path.GetFileName(result.ArchivePath), Does.Not.EqualTo("a.zap21"));
    }
    [Test] public async Task ExcessivelyLongRootFailsBeforeSaveOrArchiveAndPreservesBackups()
    {
        var first = await service.CreateAsync(target, settings); int calls = client.Calls;
        var result = await service.CreateAsync(target, settings with { BackupDirectory = Path.Combine(root, new string('x', 100)) });
        Assert.That(result.Success, Is.False); Assert.That(result.Response.SaveCompleted, Is.False);
        Assert.That(client.Calls, Is.EqualTo(calls)); Assert.That(File.Exists(first.ArchivePath), Is.True);
        Assert.That(result.Message, Does.Contain("143"));
    }
    [Test] public async Task RotationRunsOnlyAfterNewArchiveIsCommitted()
    {
        for (int i = 0; i < 7; i++) Assert.That((await service.CreateAsync(target, settings)).Success, Is.True);
        Assert.That(Archives(), Has.Length.EqualTo(5));
    }
    [TestCase("native-failure")][TestCase("corrupt")]
    public async Task FailedBackupPreservesOldBackupsAndKnownSaveResult(string mode)
    {
        for (int i = 0; i < 5; i++) await service.CreateAsync(target, settings);
        var before = Archives().Order().ToArray(); client.Mode = mode;
        var result = await service.CreateAsync(target, settings);
        Assert.That(result.Success, Is.False); Assert.That(result.Response.SaveCompleted, Is.True);
        Assert.That(Archives().Order(), Is.EqualTo(before));
    }
    [Test] public async Task ForeignArchiveIsNeverRotated()
    {
        var first = await service.CreateAsync(target, settings);
        string foreign = Path.Combine(Path.GetDirectoryName(first.ArchivePath)!, "foreign.zap21"); File.WriteAllText(foreign, "user file");
        for (int i = 0; i < 6; i++) await service.CreateAsync(target, settings);
        Assert.That(File.ReadAllText(foreign), Is.EqualTo("user file")); Assert.That(Archives(), Has.Length.EqualTo(6));
    }
    [Test] public async Task ArchiveModifiedSinceReceiptIsPreserved()
    {
        var first = await service.CreateAsync(target, settings); File.WriteAllText(first.ArchivePath, "replaced by user");
        for (int i = 0; i < 5; i++) await service.CreateAsync(target, settings);
        Assert.That(File.ReadAllText(first.ArchivePath), Is.EqualTo("replaced by user"));
    }
    [Test] public async Task UnlimitedRetentionKeepsEverything()
    {
        settings = settings with { BackupRetention = 0 };
        for (int i = 0; i < 7; i++) await service.CreateAsync(target, settings);
        Assert.That(Archives(), Has.Length.EqualTo(7));
    }
    [Test] public async Task SameNamedProjectsHaveSeparateBackupDirectories()
    {
        var first = await service.CreateAsync(target, settings);
        target.ProjectPath = Path.Combine(root, "other", "Machine.ap21");
        var second = await service.CreateAsync(target, settings);
        Assert.That(Path.GetDirectoryName(first.ArchivePath), Is.Not.EqualTo(Path.GetDirectoryName(second.ArchivePath)));
    }
    [Test] public async Task FolderAndArchiveUseRequestedLocalMinuteWithoutVisibleHash()
    {
        var result = await service.CreateAsync(target, settings);
        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(Path.GetFileName(Path.GetDirectoryName(result.ArchivePath)), Is.EqualTo("Machine_20261120_0835"));
        Assert.That(Path.GetFileName(result.ArchivePath), Is.EqualTo("Machine_20261120_0835.zap21"));
        var receipt = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(result.ArchivePath + ".manifest.json"))!;
        Assert.That(receipt["ProjectKey"]!.GetValue<string>(), Has.Length.EqualTo(16));
    }
    [Test] public async Task SecondBackupInSameMinuteUsesNumericSuffixAndKeepsFirst()
    {
        var first = await service.CreateAsync(target, settings); var second = await service.CreateAsync(target, settings);
        Assert.That(second.Success, Is.True, second.Message);
        Assert.That(Path.GetFileName(second.ArchivePath), Is.EqualTo("Machine_20261120_0835_02.zap21"));
        Assert.That(File.Exists(first.ArchivePath), Is.True); Assert.That(Archives(), Has.Length.EqualTo(2));
    }
    [Test] public async Task ExistingCommitLockOrForeignFileIsNotOverwrittenOrDeleted()
    {
        var first=await service.CreateAsync(target,settings);
        string existing=Path.Combine(settings.BackupDirectory,".tia-autosave-commit.lock");
        File.WriteAllText(existing,"preserve existing file");
        var result=await service.CreateAsync(target,settings);
        Assert.That(result.Success,Is.False); Assert.That(File.ReadAllText(existing),Is.EqualTo("preserve existing file"));
        Assert.That(File.Exists(first.ArchivePath),Is.True);
    }
    [Test] public async Task SameNamedOtherProjectIsNotDeletedByRotation()
    {
        var other = await service.CreateAsync(target, settings);
        target.ProjectPath = Path.Combine(root,"other","Machine.ap21");
        for(int i=0;i<7;i++) Assert.That((await service.CreateAsync(target,settings)).Success,Is.True);
        Assert.That(File.Exists(other.ArchivePath),Is.True); Assert.That(Archives(),Has.Length.EqualTo(6));
    }
    [Test] public async Task RotationSpansDatedFoldersAcrossMidnight()
    {
        namingClock.Utc = new DateTimeOffset(2026,11,20,20,59,0,TimeSpan.Zero);
        var first = await service.CreateAsync(target,settings); BackupResult? latest=null;
        for(int i=0;i<5;i++) { namingClock.Utc=namingClock.Utc.AddMinutes(1); latest=await service.CreateAsync(target,settings); }
        Assert.That(Archives(),Has.Length.EqualTo(5)); Assert.That(File.Exists(first.ArchivePath),Is.False);
        Assert.That(Path.GetFileName(latest!.ArchivePath),Is.EqualTo("Machine_20261121_0004.zap21"));
    }
    [Test] public async Task LegacyHashFolderParticipatesInRetentionWithoutRenamingOrDeletingForeignData()
    {
        var first=await service.CreateAsync(target,settings);
        var receipt=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(first.ArchivePath+".manifest.json"))!;
        string oldDirectory=Path.GetDirectoryName(first.ArchivePath)!;
        string legacy=Path.Combine(settings.BackupDirectory,"Machine-"+receipt["ProjectKey"]!.GetValue<string>());
        Assert.That(Path.GetFullPath(oldDirectory),Does.StartWith(root+Path.DirectorySeparatorChar));
        Assert.That(Path.GetFullPath(legacy),Does.StartWith(root+Path.DirectorySeparatorChar));
        Directory.Move(oldDirectory,legacy);
        string foreign=Path.Combine(legacy,"user-notes.txt"); File.WriteAllText(foreign,"preserve");
        for(int i=0;i<5;i++) Assert.That((await service.CreateAsync(target,settings)).Success,Is.True);
        Assert.That(Archives(),Has.Length.EqualTo(5));
        Assert.That(File.Exists(Path.Combine(legacy,Path.GetFileName(first.ArchivePath))),Is.False);
        Assert.That(File.ReadAllText(foreign),Is.EqualTo("preserve"));
    }
    [Test] public async Task BackupInsideOpenProjectIsRejectedBeforeApiCall()
    {
        var result = await service.CreateAsync(target, settings with { BackupDirectory = Path.Combine(root, "source", "backups") });
        Assert.That(result.Success, Is.False); Assert.That(client.Calls, Is.Zero);
    }
    [Test] public void CancelledBackupMakesNoApiCall()
    {
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.That(async () => await service.CreateAsync(target, settings, token.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(client.Calls, Is.Zero);
    }
    [Test] public async Task MalformedReceiptCannotCrashRotationOrDeleteUnmanagedFile()
    {
        var first = await service.CreateAsync(target, settings);
        var receipt = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(first.ArchivePath + ".manifest.json"))!;
        receipt["ArchiveFile"] = null;
        File.WriteAllText(first.ArchivePath + ".manifest.json", receipt.ToJsonString());
        for (int i = 0; i < 6; i++) Assert.That((await service.CreateAsync(target, settings)).Success, Is.True);
        Assert.That(File.Exists(first.ArchivePath), Is.True);
    }
    [Test] public async Task NewBackupIsProtectedWhenOlderReceiptsHaveFutureTimestamps()
    {
        for (int i = 0; i < 5; i++)
        {
            var archive = await service.CreateAsync(target, settings);
            string manifest = archive.ArchivePath + ".manifest.json";
            var receipt = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifest))!;
            receipt["CreatedUtc"] = DateTimeOffset.UtcNow.AddYears(10).ToString("O");
            File.WriteAllText(manifest, receipt.ToJsonString());
        }
        var newest = await service.CreateAsync(target, settings);
        Assert.That(newest.Success, Is.True); Assert.That(File.Exists(newest.ArchivePath), Is.True);
        Assert.That(Archives(), Has.Length.EqualTo(5));
    }
}

public class ArchivePathTests
{
    [Test] public void ExactReportedRootAndProjectFitConservativeLimit()
    {
        var paths = BackupPathPlanner.Create(@"C:\Users\tester\Desktop\Sample_Siemens\AutoBackups", "Demo_Press_19112026_V11", new DateTimeOffset(2026,11,20,8,35,0,TimeSpan.FromHours(3)));
        Assert.That(Path.Combine(paths.StagingDirectory, paths.StagingName).Length, Is.LessThanOrEqualTo(143));
        Assert.That(Path.Combine(paths.Directory, paths.FinalName).Length, Is.LessThanOrEqualTo(143));
        Assert.That(paths.Directory, Is.EqualTo(@"C:\Users\tester\Desktop\Sample_Siemens\AutoBackups\Demo_Press_19112026_V11_20261120_0835"));
        Assert.That(paths.FinalName,Is.EqualTo("Demo_Press_19112026_V11_20261120_0835.zap21"));
    }
    [TestCase(143, true)][TestCase(144, false)]
    public void FullPathBoundaryIncludesZapExtension(int length, bool accepted)
    {
        const string name = "a.zap21";
        string directory = @"C:\" + new string('x', length - 3 - 1 - name.Length);
        Assert.That(Path.Combine(directory, name).Length, Is.EqualTo(length));
        if (accepted) Assert.DoesNotThrow(() => ArchivePathRules.Validate(directory, name));
        else Assert.Throws<InvalidDataException>(() => ArchivePathRules.Validate(directory, name));
    }
    [Test] public void LongProjectFolderAndFilenameAreBothAdapted()
    {
        string root = @"C:\" + new string('x', 65);
        var paths = BackupPathPlanner.Create(root, new string('P', 60), DateTimeOffset.Now);
        Assert.That(Path.Combine(paths.Directory, paths.FinalName).Length, Is.LessThanOrEqualTo(143));
        Assert.DoesNotThrow(() => ArchivePathRules.Validate(paths.StagingDirectory, paths.StagingName));
    }
}

