using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace TIAAutoSaveManager.Contracts
{
    [DataContract]
    public sealed class PortalSnapshot
    {
        [DataMember] public int ProcessId { get; set; }
        [DataMember] public long StartedUtcTicks { get; set; }
        [DataMember] public string Version { get; set; } = "V21";
        [DataMember] public string SessionId { get; set; } = "";
        [DataMember] public string ProjectName { get; set; } = "";
        [DataMember] public string ProjectPath { get; set; } = "";
        [DataMember] public string Status { get; set; } = "Detected";
        [DataMember] public string Message { get; set; } = "";
        [DataMember] public string LastSavedUtc { get; set; } = "";

        public bool SameProcess(PortalSnapshot other) => other != null &&
            ProcessId == other.ProcessId && StartedUtcTicks == other.StartedUtcTicks && Version == other.Version;

        public bool SameSelection(PortalSnapshot other) => SameProcess(other) &&
            !string.IsNullOrEmpty(SessionId) && SessionId == other.SessionId &&
            !string.IsNullOrEmpty(ProjectPath) &&
            string.Equals(ProjectPath, other.ProjectPath, StringComparison.OrdinalIgnoreCase);
    }

    [DataContract]
    public sealed class HostRequest
    {
        [DataMember] public int Version { get; set; } = WireProtocol.Version;
        [DataMember] public string Id { get; set; } = Guid.NewGuid().ToString("N");
        [DataMember] public string Operation { get; set; } = "Discover";
        [DataMember] public PortalSnapshot Target { get; set; }
        [DataMember] public string ArchiveDirectory { get; set; } = "";
        [DataMember] public string ArchiveName { get; set; } = "";
    }

    [DataContract]
    public sealed class HostResponse
    {
        [DataMember] public int Version { get; set; } = WireProtocol.Version;
        [DataMember] public string Id { get; set; } = "";
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Outcome { get; set; } = "Failed";
        [DataMember] public string Message { get; set; } = "";
        [DataMember] public PortalSnapshot[] Portals { get; set; } = new PortalSnapshot[0];
        [DataMember] public bool SaveCompleted { get; set; }
        [DataMember] public string ArchivePath { get; set; } = "";
    }

    // One bounded length-prefixed JSON message per request/response. No Siemens types cross IPC.
    public static class WireProtocol
    {
        public const int Version = 2;
        public const int MaximumBytes = 1024 * 1024;

        public static void Write<T>(Stream stream, T value)
        {
            using (var buffer = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(buffer, value);
                if (buffer.Length > MaximumBytes) throw new InvalidDataException("IPC message too large.");
                var payload = buffer.ToArray();
                var length = BitConverter.GetBytes(payload.Length);
                stream.Write(length, 0, length.Length);
                stream.Write(payload, 0, payload.Length);
                stream.Flush();
            }
        }

        public static T Read<T>(Stream stream)
        {
            var header = ReadExactly(stream, 4);
            int length = BitConverter.ToInt32(header, 0);
            if (length <= 0 || length > MaximumBytes) throw new InvalidDataException("Invalid IPC message size.");
            using (var buffer = new MemoryStream(ReadExactly(stream, length)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(buffer);
        }

        private static byte[] ReadExactly(Stream stream, int length)
        {
            var bytes = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int count = stream.Read(bytes, offset, length - offset);
                if (count == 0) throw new EndOfStreamException("Openness host connection closed.");
                offset += count;
            }
            return bytes;
        }
    }
}
