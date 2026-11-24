#nullable disable
using System.Collections.Generic;

namespace TIAAutoSaveManager.OpennessHost
{
    // Only these narrow operations are available to the shared save/backup engine.
    internal interface IPortalRuntime
    {
        string Version { get; }
        IEnumerable<IPortalProcess> GetProcesses();
    }
    internal interface IPortalProcess
    {
        int Id { get; }
        string ProjectPath { get; }
        IPortalConnection Attach();
    }
    internal interface IPortalConnection
    {
        bool IsLost { get; }
        IPortalProject FirstProject();
        void ReleaseOwnSessions(int ownProcessId);
    }
    internal interface IPortalProject
    {
        string Name { get; }
        string Path { get; }
        void Save();
        void Archive(string directory, string name);
    }
}
