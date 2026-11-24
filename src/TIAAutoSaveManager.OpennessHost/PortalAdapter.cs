#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Serilog;

using TIAAutoSaveManager.Contracts;

namespace TIAAutoSaveManager.OpennessHost
{
    // Shared V20/V21 engine. All methods run on the host's single MTA thread.
    internal sealed class PortalAdapter
    {
        private sealed class Connection
        {
            public IPortalConnection Portal;
            public PortalSnapshot Snapshot;
        }

        private readonly IPortalRuntime runtime;
        private readonly Func<bool> hasPermission;
        public PortalAdapter(IPortalRuntime runtime, Func<bool> hasPermission = null) { this.runtime = runtime; this.hasPermission = hasPermission ?? HasOpennessPermission; }

        private readonly Dictionary<int, Connection> connections = new Dictionary<int, Connection>();

        public HostResponse Handle(HostRequest request)
        {
            if (request.Target != null && request.Target.Version != runtime.Version)
                return Failed(request.Id, "Stale", "TIA sürümü ile host eşleşmiyor; işlem yapılmadı.");
            switch (request.Operation)
            {
                case "Discover": return Discover(request.Id);
                case "Attach": return Attach(request);
                case "Save": return Save(request);
                case "Backup": return Backup(request);
                case "Disconnect":
                    DisconnectAll();
                    return Ok(request.Id, "Uygulamanın bağlantıları bırakıldı; kullanıcı projeleri kapatılmadı.");
                default: throw new InvalidDataException("Unsupported operation.");
            }
        }

        private HostResponse Discover(string requestId)
        {
            var results = new List<PortalSnapshot>();
            // CRITICAL: Never Dispose()/using a TiaPortalProcess from GetProcesses(). It can close TIA.
            foreach (var process in runtime.GetProcesses())
            {
                try
                {
                    long started = ReadStart(process.Id);
                    var snapshot = new PortalSnapshot { Version = runtime.Version, ProcessId = process.Id, StartedUtcTicks = started,
                        ProjectPath = process.ProjectPath, ProjectName = string.IsNullOrEmpty(process.ProjectPath) ? "Proje açık değil" : Path.GetFileName(process.ProjectPath) };
                    if (connections.TryGetValue(process.Id, out var connection))
                    {
                        if (!connection.Portal.IsLost && connection.Snapshot.StartedUtcTicks == started)
                        {
                            var project = connection.Portal.FirstProject();
                            string path = project?.Path ?? "";
                            if (string.Equals(path, connection.Snapshot.ProjectPath, StringComparison.OrdinalIgnoreCase))
                            {
                                snapshot = connection.Snapshot;
                                snapshot.ProjectName = project?.Name ?? "Proje açık değil";
                            }
                            else
                            {
                                snapshot.Status = "ProjectChanged";
                                snapshot.Message = "Proje değişti. Yeniden Bağlan seçin.";
                                Release(process.Id);
                            }
                        }
                        else { Release(process.Id); }
                    }
                    results.Add(snapshot);
                }
                catch (Exception e)
                {
                    results.Add(new PortalSnapshot { Version = runtime.Version, ProcessId = process.Id, Status = "Error", Message = e.Message });
                    Log.Warning(e, "Discovery metadata failed for PID {Pid}", process.Id);
                }
            }
            foreach (int stale in connections.Keys.Where(pid => !results.Any(p => p.ProcessId == pid)).ToArray()) Release(stale);
            var response = Ok(requestId, results.Count == 0 ? "Çalışan TIA Portal " + runtime.Version + " instance bulunamadı." : results.Count + " " + runtime.Version + " instance bulundu.");
            if (!hasPermission()) response.Message += " Bağlanmak için Windows hesabınız Siemens TIA Openness grubuna eklenmeli ve oturum yenilenmeli.";
            response.Portals = results.ToArray();
            return response;
        }

        private HostResponse Attach(HostRequest request)
        {
            if (!hasPermission())
                return Failed(request.Id, "AccessDenied", "Windows hesabınız Siemens TIA Openness grubunda değil. Yönetici üyeliği ekledikten sonra Windows oturumunu kapatıp açın. TIA bağlantısı denenmedi.");
            var selected = request.Target ?? throw new InvalidDataException("Instance seçilmedi.");
            var process = runtime.GetProcesses().FirstOrDefault(p => p.Id == selected.ProcessId);
            if (process == null || ReadStart(process.Id) != selected.StartedUtcTicks)
                return Failed(request.Id, "Stale", "Instance kapandı veya PID başka bir sürece geçti. Yenileyin.");
            string currentPath = process.ProjectPath;
            if (!string.Equals(currentPath, selected.ProjectPath, StringComparison.OrdinalIgnoreCase))
                return Failed(request.Id, "Stale", "Keşiften sonra proje değişti. Yenileyip tekrar seçin.");
            Release(process.Id);
            var connection = new Connection { Portal = process.Attach() };
            connections.Add(process.Id, connection);
            try
            {
                var project = connection.Portal.FirstProject();
                connection.Snapshot = new PortalSnapshot
                {
                    ProcessId = selected.ProcessId, StartedUtcTicks = selected.StartedUtcTicks,
                    SessionId = Guid.NewGuid().ToString("N"), Status = "Connected",
                    Version = runtime.Version, ProjectName = project?.Name ?? "Proje açık değil", ProjectPath = project?.Path ?? ""
                };
                if (!string.Equals(connection.Snapshot.ProjectPath, currentPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Bağlanırken proje değişti. Yeniden keşfedin.");
                var response = Ok(request.Id, runtime.Version + " bağlantısı kuruldu. Otomatik kayıt kapalı.");
                response.Portals = new[] { connection.Snapshot };
                return response;
            }
            catch { Release(process.Id); throw; }
        }

        private HostResponse Save(HostRequest request)
        {
            var target = request.Target;
            if (target == null || !connections.TryGetValue(target.ProcessId, out var connection) || connection.Snapshot == null ||
                connection.Portal.IsLost || !target.SameSelection(connection.Snapshot))
                return Failed(request.Id, "Stale", "Bağlantı/seçim geçersiz. Yeniden keşfedip bağlanın.");
            if (ReadStart(target.ProcessId) != target.StartedUtcTicks)
            {
                Release(target.ProcessId);
                return Failed(request.Id, "Stale", "Portal kimliği değişti. Kayıt yapılmadı.");
            }
            var project = connection.Portal.FirstProject();
            if (project == null || !string.Equals(project.Path, target.ProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                Release(target.ProcessId);
                return Failed(request.Id, "Stale", "Açık proje değişti veya kapandı. Kayıt yapılmadı.");
            }
            // Only supported Project.Save. Never Close, SaveAs, Compile, Download or process Dispose.
            project.Save();
            connection.Snapshot.LastSavedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var response = Ok(request.Id, "Project.Save() başarıyla tamamlandı.");
            response.SaveCompleted = true;
            response.Portals = new[] { connection.Snapshot };
            return response;
        }

        private HostResponse Backup(HostRequest request)
        {
            try { ArchivePathRules.Validate(request.ArchiveDirectory, request.ArchiveName, runtime.Version); }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is ArgumentException || e is NotSupportedException)
            { return Failed(request.Id, "BackupFailed", e.Message); }
            // Validate destination before any project write. The client creates its private staging folder.
            if (string.IsNullOrWhiteSpace(request.ArchiveDirectory) || !Path.IsPathRooted(request.ArchiveDirectory) ||
                !Directory.Exists(request.ArchiveDirectory) || string.IsNullOrWhiteSpace(request.ArchiveName) ||
                Path.GetFileName(request.ArchiveName) != request.ArchiveName || !request.ArchiveName.EndsWith(PortalVersions.ArchiveExtension(runtime.Version), StringComparison.OrdinalIgnoreCase))
                return Failed(request.Id, "BackupFailed", "Arşiv hedefi geçersiz.");
            var response = Save(request);
            if (!response.Success) return response;
            try
            {
                var connection = connections[request.Target.ProcessId];
                var project = connection.Portal.FirstProject();
                if (project == null || !string.Equals(project.Path, request.Target.ProjectPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Save sonrası proje değişti; arşiv oluşturulmadı.");
                project.Archive(request.ArchiveDirectory, request.ArchiveName);
                response.ArchivePath = Path.Combine(request.ArchiveDirectory, request.ArchiveName);
                response.Message = "Save ve Archive API çağrıları tamamlandı; arşiv doğrulaması bekleniyor.";
            }
            catch (Exception e)
            {
                response.Success = false; response.Outcome = "BackupFailed";
                response.Message = "Save başarılı, Archive başarısız: " + e.Message;
                Log.Error(e, "Archive failed after successful Save {RequestId}", request.Id);
            }
            return response;
        }

        private static long ReadStart(int pid)
        {
            // This is System.Diagnostics.Process (local OS handle), NOT TiaPortalProcess.
            using (var process = Process.GetProcessById(pid)) return process.StartTime.ToUniversalTime().Ticks;
        }

        private static bool HasOpennessPermission()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var sid = (SecurityIdentifier)new NTAccount(Environment.MachineName, "Siemens TIA Openness").Translate(typeof(SecurityIdentifier));
                    return new WindowsPrincipal(identity).IsInRole(sid);
                }
            }
            catch (IdentityNotMappedException) { return false; }
        }

        private void Release(int pid)
        {
            if (!connections.TryGetValue(pid, out var connection)) return;
            try
            {
                // Only disconnect sessions owned by this host. Never dispose portal/process handles.
                if (!connection.Portal.IsLost)
                {
                    int ownPid = Process.GetCurrentProcess().Id;
                    connection.Portal.ReleaseOwnSessions(ownPid);
                }
            }
            catch (Exception e) { Log.Warning(e, "Session disconnect failed for PID {Pid}; no portal termination attempted", pid); }
            finally { connections.Remove(pid); }
        }

        public void DisconnectAll()
        {
            foreach (int pid in connections.Keys.ToArray()) Release(pid);
        }

        private static HostResponse Ok(string id, string message) => new HostResponse { Id = id, Success = true, Outcome = "Success", Message = message };
        private static HostResponse Failed(string id, string outcome, string message) => new HostResponse { Id = id, Outcome = outcome, Message = message };
    }
}

