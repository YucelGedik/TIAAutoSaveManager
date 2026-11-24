using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using Serilog;
using TIAAutoSaveManager.Contracts;

namespace TIAAutoSaveManager.OpennessHost
{
    internal static class Program
    {
        [MTAThread]
        private static int Main(string[] args)
        {
            if ((args.Length != 1 && args.Length != 2) || !args[0].StartsWith("TIAAutoSaveManager-", StringComparison.Ordinal)) return 2;
            string version = args.Length == 2 ? args[1] : "V21";
            if (!PortalVersions.IsSupported(version)) return 2;
            string data = Environment.GetEnvironmentVariable("TIAAUTOSAVE_DATA_DIR");
            if (string.IsNullOrWhiteSpace(data)) data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TIAAutoSaveManager");
            try
            {
                Directory.CreateDirectory(Path.Combine(data, "Logs"));
                Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
                    .WriteTo.File(Path.Combine(data, "Logs", "openness-" + version + "-.log"), rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14, fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true).CreateLogger();
                return RunPipe(args[0], version);
            }
            catch (Exception e) { Log.Fatal(e, "Openness host stopped"); return 1; }
            finally { Log.CloseAndFlush(); }
        }

        private static int RunPipe(string name, string version)
        {
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
            using (var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous, 65536, 65536, security))
            {
                var waiting = pipe.BeginWaitForConnection(null, null);
                using (waiting.AsyncWaitHandle)
                {
                    if (!waiting.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(15))) return 3;
                    pipe.EndWaitForConnection(waiting);
                }
                Action cleanup = null;
                Func<HostRequest, HostResponse> handler = null;
                string setupError = null;
                try
                {
                    string assembly = FindOpennessAssembly(version);
                    string folder = Path.GetDirectoryName(assembly);
                    AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) => Resolve(folder, eventArgs);
                    handler = CreateAdapter(version, assembly, out cleanup); // Kept out of resolver-registration method for JIT safety.
                }
                catch (Exception e) { setupError = e.Message; Log.Error(e, "{Version} initialization failed", version); }
                try
                {
                    while (pipe.IsConnected)
                    {
                        HostRequest request;
                        try { request = WireProtocol.Read<HostRequest>(pipe); }
                        catch (EndOfStreamException) { break; }
                        var response = new HostResponse { Id = request.Id };
                        try
                        {
                            if (request.Version != WireProtocol.Version) throw new InvalidDataException("IPC protocol version mismatch.");
                            if (handler == null) throw new InvalidOperationException(version + " SDK başlatılamadı: " + setupError);
                            Log.Information("Begin {Operation} {RequestId} PID {Pid}", request.Operation, request.Id, request.Target?.ProcessId);
                            response = handler(request);
                        }
                        catch (Exception e)
                        {
                            // An exception after dispatch of Save may not prove that nothing was persisted.
                            response.Outcome = request.Operation == "Save" || request.Operation == "Backup" ? "Uncertain" : "Failed";
                            response.Message = e.GetType().Name + ": " + e.Message;
                            Log.Error(e, "Request failed {Operation} {RequestId}", request.Operation, request.Id);
                        }
                        Log.Information("End {Operation} {RequestId} {Outcome}", request.Operation, request.Id, response.Outcome);
                        WireProtocol.Write(pipe, response);
                    }
                }
                catch (IOException e) { Log.Warning(e, "Client pipe closed"); }
                finally { cleanup?.Invoke(); }
            }
            return 0;
        }

        private static string FindOpennessAssembly(string version)
        {
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = root.OpenSubKey(version == "V21" ? @"SOFTWARE\Siemens\Automation\Openness\21.0\PublicAPI\21.0.0.0\net48" : @"SOFTWARE\Siemens\Automation\Openness\20.0\PublicAPI\20.0.0.0"))
            {
                string expectedName = version == "V21" ? "Siemens.Engineering.Base" : "Siemens.Engineering";
                string assemblyPath = key?.GetValue(expectedName) as string;
                if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
                    throw new FileNotFoundException("Kurulu " + version + " Openness assembly kaydı bulunamadı. Siemens kurulumundan Openness bileşenini ekleyin.");
                var identity = AssemblyName.GetAssemblyName(assemblyPath);
                if (identity.Name != expectedName || identity.Version.Major != (version == "V21" ? 21 : 20) ||
                    BitConverter.ToString(identity.GetPublicKeyToken()).Replace("-", "").ToLowerInvariant() != (version == "V21" ? "29bfe5fdf4ba5d3b" : "d29ec89bac048f84"))
                    throw new InvalidDataException(version + " assembly kimliği doğrulanamadı.");
                Log.Information("Using {Version} API {AssemblyIdentity} at {Path}", version, identity.FullName, assemblyPath);
                return assemblyPath;
            }
        }

        private static Assembly Resolve(string folder, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            if (name.Name != "Siemens.Engineering" && !name.Name.StartsWith("Siemens.Engineering.", StringComparison.Ordinal)) return null;
            string candidate = Path.Combine(folder, name.Name + ".dll");
            if (!File.Exists(candidate)) return null;
            var identity = AssemblyName.GetAssemblyName(candidate);
            if (identity.FullName != name.FullName) throw new FileLoadException("Siemens assembly identity mismatch.", candidate);
            return Assembly.LoadFrom(candidate);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Func<HostRequest, HostResponse> CreateAdapter(string version, string assemblyPath, out Action cleanup)
        {
            var adapter = new PortalAdapter(version == "V21" ? CreateV21Runtime() : new V20Runtime(Assembly.LoadFrom(assemblyPath)));
            cleanup = adapter.DisconnectAll;
            return adapter.Handle;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IPortalRuntime CreateV21Runtime() => new V21Runtime();
    }
}
