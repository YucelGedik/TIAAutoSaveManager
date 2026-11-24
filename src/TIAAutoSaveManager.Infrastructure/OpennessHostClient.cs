using System.Diagnostics;
using System.IO.Pipes;
using Serilog;
using TIAAutoSaveManager.Contracts;
using TIAAutoSaveManager.Core;

namespace TIAAutoSaveManager.Infrastructure;

public sealed class OpennessHostClient(string hostPath, ILogger log, string portalVersion = "V21") : IOpennessClient
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private NamedPipeClientStream? pipe;
    private Process? host;
    private bool disposed;
    private bool uncertainWrite;

    public async Task<HostResponse> SendAsync(HostRequest request, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (uncertainWrite)
                return new() { Id = request.Id, Outcome = "Uncertain", Message = "Önceki kayıt sonucu bilinmiyor. TIA projesini kontrol edin ve uygulamayı yeniden açın; otomatik tekrar yapılmadı." };
            await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var timer = Stopwatch.StartNew();
            log.Information("TIA request {Operation} {RequestId} PID {ProcessId}", request.Operation, request.Id, request.Target?.ProcessId);
            try
            {
                // Never cancel or time out an in-flight write. The gate stays held until it really returns.
                var response = await Task.Run(() =>
                {
                    WireProtocol.Write(pipe!, request);
                    return WireProtocol.Read<HostResponse>(pipe!);
                }).ConfigureAwait(false);
                if (response.Version != WireProtocol.Version || response.Id != request.Id)
                    throw new InvalidDataException("Openness host protocol mismatch.");
                log.Information("TIA result {Operation} {RequestId} {Outcome} in {ElapsedMs} ms: {Message}",
                    request.Operation, request.Id, response.Outcome, timer.ElapsedMilliseconds, response.Message);
                if (request.Operation is "Save" or "Backup" && response.Outcome == "Uncertain") uncertainWrite = true;
                return response;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or System.Runtime.Serialization.SerializationException or ObjectDisposedException)
            {
                bool write = request.Operation is "Save" or "Backup";
                uncertainWrite |= write;
                pipe?.Dispose(); pipe = null;
                log.Error(e, "Host connection lost {Operation} {RequestId}; write outcome uncertain {Uncertain}", request.Operation, request.Id, write);
                return new() { Id = request.Id, Outcome = write ? "Uncertain" : "Disconnected", Message = write
                    ? "Bağlantı koptu; kaydın sonucu bilinmiyor. Tekrar kayıt gönderilmedi. TIA projesini kontrol edin."
                    : "Openness bağlantısı kesildi. Yenile ile yeniden keşfedebilirsiniz. " + e.Message };
            }
        }
        finally { gate.Release(); }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (pipe?.IsConnected == true) return;
        // Do not create another host while an earlier one may still be completing a call.
        if (host is { HasExited: false })
            throw new IOException("Önceki Openness host hâlâ çalışıyor; güvenlik için ikinci host başlatılmadı.");
        if (!File.Exists(hostPath)) throw new FileNotFoundException("Openness host bulunamadı. Tam uygulama paketini derleyin.", hostPath);
        host?.Dispose();
        string pipeName = "TIAAutoSaveManager-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(hostPath) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(hostPath)! };
        start.ArgumentList.Add(pipeName);
        start.ArgumentList.Add(portalVersion);
        host = Process.Start(start) ?? throw new IOException("Openness host başlatılamadı.");
        pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try { await pipe.ConnectAsync(10000, cancellationToken).ConfigureAwait(false); }
        catch { pipe.Dispose(); pipe = null; throw; }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            disposed = true;
            // EOF asks the host to release only its own attached sessions after the last call finishes.
            pipe?.Dispose(); pipe = null;
            if (host is { HasExited: false }) await host.WaitForExitAsync().ConfigureAwait(false);
            host?.Dispose();
        }
        finally { gate.Release(); }
    }
}
