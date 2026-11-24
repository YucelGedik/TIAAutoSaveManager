using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace TIAAutoSaveManager.OpennessHost
{
    // V20 is bound to the customer's genuine, identity-checked local SDK.
    // Reflection avoids shipping Siemens binaries or requiring V21 on a V20-only PC.
    internal sealed class V20Runtime : IPortalRuntime
    {
        private readonly MethodInfo getProcesses;
        private readonly object compressed;
        public string Version => "V20";

        public V20Runtime(Assembly sdk)
        {
            getProcesses = RequiredMethod(sdk.GetType("Siemens.Engineering.TiaPortal", true), "GetProcesses", Type.EmptyTypes);
            var project = sdk.GetType("Siemens.Engineering.Project", true);
            RequiredMethod(project, "Save", Type.EmptyTypes);
            var mode = sdk.GetType("Siemens.Engineering.ProjectArchivationMode", true);
            RequiredMethod(project, "Archive", new[] { typeof(DirectoryInfo), typeof(string), mode });
            compressed = Enum.Parse(mode, "Compressed");
        }
        public IEnumerable<IPortalProcess> GetProcesses() => ((IEnumerable)Invoke(getProcesses, null)).Cast<object>().Select(p => (IPortalProcess)new PortalProcess(p, compressed));
        private sealed class PortalProcess(object native, object compressed) : IPortalProcess
        {
            public int Id => (int)Property(native, "Id");
            public string ProjectPath => (Property(native, "ProjectPath") as FileInfo)?.FullName ?? "";
            public IPortalConnection Attach() => new PortalConnection(Call(native, "Attach"), compressed);
        }
        private sealed class PortalConnection : IPortalConnection
        {
            private readonly object native, compressed;
            private readonly EventInfo disposed;
            private readonly Delegate handler;
            private int lost;
            public PortalConnection(object native, object compressed)
            {
                this.native = native; this.compressed = compressed;
                disposed = native.GetType().GetEvent("Disposed") ?? throw new MissingMemberException("V20 Disposed event missing.");
                EventHandler callback = OnDisposed;
                handler = Delegate.CreateDelegate(disposed.EventHandlerType, callback.Target, callback.Method);
                disposed.AddEventHandler(native, handler);
            }
            private void OnDisposed(object sender, EventArgs args) => Interlocked.Exchange(ref lost, 1);
            public bool IsLost => Volatile.Read(ref lost) != 0;
            public IPortalProject FirstProject()
            {
                var project = ((IEnumerable)Property(native, "Projects")).Cast<object>().FirstOrDefault();
                return project == null ? null : new PortalProject(project, compressed);
            }
            public void ReleaseOwnSessions(int ownProcessId)
            {
                try
                {
                    var process = Call(native, "GetCurrentProcess");
                    var ownSessions = ((IEnumerable)Property(process, "AttachedSessions")).Cast<object>()
                        .Where(s => (int)Property(s, "ProcessId") == ownProcessId).ToArray();
                    // Only attached session objects are disposed. Never a TiaPortal/Process/Project.
                    foreach (var session in ownSessions) Call(session, "Dispose");
                }
                finally { disposed.RemoveEventHandler(native, handler); }
            }
        }
        private sealed class PortalProject(object native, object compressed) : IPortalProject
        {
            public string Name => (string)Property(native, "Name");
            public string Path => (Property(native, "Path") as FileInfo)?.FullName ?? "";
            public void Save() => Call(native, "Save");
            public void Archive(string directory, string name) => Invoke(
                RequiredMethod(native.GetType(), "Archive", new[] { typeof(DirectoryInfo), typeof(string), compressed.GetType() }),
                native, new DirectoryInfo(directory), name, compressed);
        }
        private static object Property(object target, string name) =>
            (target.GetType().GetProperty(name) ?? throw new MissingMemberException(target.GetType().FullName, name)).GetValue(target, null);
        private static object Call(object target, string name) => Invoke(RequiredMethod(target.GetType(), name, Type.EmptyTypes), target);
        private static MethodInfo RequiredMethod(Type type, string name, Type[] arguments) =>
            type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, null, arguments, null)
                ?? throw new MissingMethodException(type.FullName, name);
        private static object Invoke(MethodInfo method, object target, params object[] args)
        {
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
    }
}
