using TIAAutoSaveManager.Contracts;

namespace TIAAutoSaveManager.Core;

public enum ScheduledOperationKind { Save, Backup }
public sealed record ScheduledOperation(PortalSnapshot Target, ScheduledOperationKind Kind);

// Pure scheduling state. No timers, API calls, UI dependencies, or catch-up queues.
// All due-time arithmetic uses the monotonic TimeProvider timestamp.
public sealed class AutoSaveSchedule(TimeProvider? timeProvider = null)
{
    private sealed class Entry(PortalSnapshot target, long now)
    {
        public PortalSnapshot Target = target;
        public bool Save, Backup, Running;
        public long SaveAnchor = now, BackupAnchor = now;
    }

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<string, Entry> entries = new();
    private readonly object sync = new();
    private TimeSpan saveInterval = TimeSpan.FromMinutes(10), backupInterval = TimeSpan.FromHours(1);
    private bool paused;
    public bool IsPaused { get { lock (sync) return paused; } }
    public int EnabledCount { get { lock (sync) return entries.Values.Count(e => e.Save || e.Backup); } }

    public void Configure(TimeSpan save, TimeSpan backup)
    {
        if (save < TimeSpan.FromMinutes(1) || backup < TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(save));
        lock (sync)
        {
            bool saveChanged = saveInterval != save, backupChanged = backupInterval != backup;
            if (!saveChanged && !backupChanged) return;
            saveInterval = save; backupInterval = backup;
            foreach (var e in entries.Values)
            {
                if (saveChanged) e.SaveAnchor = clock.GetTimestamp();
                if (backupChanged) e.BackupAnchor = clock.GetTimestamp();
            }
        }
    }

    public void Synchronize(IEnumerable<PortalSnapshot> targets)
    {
        lock (sync)
        {
            var valid = targets.Where(t => t.Status == "Connected" && t.SameSelection(t)).ToArray();
            foreach (string key in entries.Keys.ToArray())
            {
                var current = valid.FirstOrDefault(t => entries[key].Target.SameSelection(t));
                if (current == null) entries.Remove(key); else entries[key].Target = current;
            }
        }
    }

    public void Enable(PortalSnapshot target, bool save, bool backup)
    {
        if (target.Status != "Connected" || !target.SameSelection(target)) throw new InvalidOperationException("Önce açık projeye bağlanın.");
        lock (sync)
        {
            if (!entries.TryGetValue(target.SessionId, out var entry) || !entry.Target.SameSelection(target))
                entries[target.SessionId] = entry = new(target, clock.GetTimestamp());
            if (save && !entry.Save) entry.SaveAnchor = clock.GetTimestamp();
            if (backup && !entry.Backup) entry.BackupAnchor = clock.GetTimestamp();
            entry.Save = save; entry.Backup = backup;
        }
    }

    public (bool Save, bool Backup) Enabled(PortalSnapshot? target)
    {
        lock (sync) return target != null && entries.TryGetValue(target.SessionId, out var e) && e.Target.SameSelection(target)
            ? (e.Save, e.Backup) : (false, false);
    }

    public void SetPaused(bool value)
    {
        lock (sync)
        {
            if (paused == value) return;
            paused = value;
            // Resume starts fresh intervals; suspended ticks are never replayed.
            if (!paused) foreach (var e in entries.Values) e.SaveAnchor = e.BackupAnchor = clock.GetTimestamp();
        }
    }

    public void DisableAll() { lock (sync) entries.Clear(); }

    public TimeSpan? Remaining(PortalSnapshot? target, ScheduledOperationKind kind)
    {
        lock (sync)
        {
            if (target == null || !entries.TryGetValue(target.SessionId, out var e) || !e.Target.SameSelection(target) ||
                (kind == ScheduledOperationKind.Save ? !e.Save : !e.Backup)) return null;
            var remaining = (kind == ScheduledOperationKind.Save ? saveInterval : backupInterval) -
                clock.GetElapsedTime(kind == ScheduledOperationKind.Save ? e.SaveAnchor : e.BackupAnchor);
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
    }

    public ScheduledOperation? TakeDue()
    {
        lock (sync)
        {
            if (paused) return null;
            foreach (var entry in entries.Values)
            {
                if (entry.Running) continue;
                bool backupDue = entry.Backup && clock.GetElapsedTime(entry.BackupAnchor) >= backupInterval;
                bool saveDue = entry.Save && clock.GetElapsedTime(entry.SaveAnchor) >= saveInterval;
                if (!backupDue && !saveDue) continue;
                entry.Running = true;
                return new(entry.Target, backupDue ? ScheduledOperationKind.Backup : ScheduledOperationKind.Save);
            }
            return null;
        }
    }

    public void Complete(ScheduledOperation operation, bool saveCompleted, bool keepEnabled = true)
    {
        lock (sync)
        {
            if (!entries.TryGetValue(operation.Target.SessionId, out var entry) || !entry.Target.SameSelection(operation.Target)) return;
            if (!keepEnabled) { entries.Remove(operation.Target.SessionId); return; }
            entry.Running = false;
            if (operation.Kind == ScheduledOperationKind.Save || saveCompleted) entry.SaveAnchor = clock.GetTimestamp();
            if (operation.Kind == ScheduledOperationKind.Backup) entry.BackupAnchor = clock.GetTimestamp();
        }
    }
}
