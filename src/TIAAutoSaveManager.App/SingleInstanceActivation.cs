using System;
using System.Threading;

namespace TIAAutoSaveManager.App;

// Named objects stay in this Windows session and are keyed by the user's SID.
public sealed class SingleInstanceActivation : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle? request, acknowledgement;
    private readonly RegisteredWaitHandle? registration;
    private bool disposed;
    public bool IsPrimary { get; }

    public SingleInstanceActivation(string key, Action activate)
    {
        mutex = new Mutex(false, key);
        try { IsPrimary = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
        if (!IsPrimary) return;
        request = new EventWaitHandle(false, EventResetMode.AutoReset, key + "-Activate");
        acknowledgement = new EventWaitHandle(false, EventResetMode.AutoReset, key + "-Activated");
        registration = ThreadPool.RegisterWaitForSingleObject(request, (_, _) =>
        {
            try { activate(); acknowledgement.Set(); }
            catch (ObjectDisposedException) { }
            catch (System.Threading.Tasks.TaskCanceledException) { }
            catch (Exception error) { Serilog.Log.Warning(error, "Existing instance activation failed"); }
        }, null, Timeout.Infinite, false);
    }
    public static bool TryActivate(string key, TimeSpan timeout)
    {
        try
        {
            using var request = EventWaitHandle.OpenExisting(key + "-Activate");
            using var acknowledgement = EventWaitHandle.OpenExisting(key + "-Activated");
            acknowledgement.Reset(); request.Set();
            return acknowledgement.WaitOne(timeout);
        }
        catch (WaitHandleCannotBeOpenedException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; registration?.Unregister(null);
        request?.Dispose(); acknowledgement?.Dispose();
        if (IsPrimary) mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
