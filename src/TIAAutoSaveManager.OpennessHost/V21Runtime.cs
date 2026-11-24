using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Siemens.Engineering;

namespace TIAAutoSaveManager.OpennessHost
{
    internal sealed class V21Runtime : IPortalRuntime
    {
        public string Version => "V21";
        public IEnumerable<IPortalProcess> GetProcesses() => TiaPortal.GetProcesses().Select(p => (IPortalProcess)new PortalProcess(p));
        private sealed class PortalProcess(TiaPortalProcess native) : IPortalProcess
        {
            public int Id => native.Id;
            public string ProjectPath => native.ProjectPath?.FullName ?? "";
            public IPortalConnection Attach() => new PortalConnection(native.Attach());
        }
        private sealed class PortalConnection : IPortalConnection
        {
            private readonly TiaPortal native;
            private int lost;
            public PortalConnection(TiaPortal portal) { native = portal; native.Disposed += (_, _) => Interlocked.Exchange(ref lost, 1); }
            public bool IsLost => Volatile.Read(ref lost) != 0;
            public IPortalProject FirstProject()
            {
                var project = native.Projects.FirstOrDefault();
                return project == null ? null : new PortalProject(project);
            }
            public void ReleaseOwnSessions(int ownProcessId)
            {
                foreach (var session in native.GetCurrentProcess().AttachedSessions.Where(s => s.ProcessId == ownProcessId).ToArray()) session.Dispose();
            }
        }
        private sealed class PortalProject(Project native) : IPortalProject
        {
            public string Name => native.Name;
            public string Path => native.Path?.FullName ?? "";
            public void Save() => native.Save();
            public void Archive(string directory, string name) => native.Archive(new DirectoryInfo(directory), name, ProjectArchivationMode.Compressed);
        }
    }
}
