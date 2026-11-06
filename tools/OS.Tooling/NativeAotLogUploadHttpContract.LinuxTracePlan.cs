using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OS.Tooling;

public static partial class NativeAotLogUploadHttpContract
{
    /// <summary>Copied immutable argv for the pinned physical BusyBox setsid
    /// provider and physical strace child. Preparing ProcessStartInfo executes
    /// nothing. The original owner must authenticate the socket recipe and keep
    /// every selected binary held/unchanged through process and pipe cleanup;
    /// copied names/hashes alone grant no tool, process or session authority.</summary>

    public sealed class LinuxTracePlan
    {
        internal LinuxTracePlan(string executable, IEnumerable<string> arguments)
        {
            Executable = executable;
            Arguments = Array.AsReadOnly(arguments.ToArray());
        }

        public string Executable
        {
            get;
        }
        public ReadOnlyCollection<string> Arguments
        {
            get;
        }
        public bool GrantsOriginalProcessOrToolAuthority => false;

        /// <summary>Create an independent, unstarted process configuration
        /// using this exact literal argv. No shell, PATH search or link lookup
        /// occurs here; unsupported working-directory spelling rejects.</summary>

        public ProcessStartInfo PrepareStartInfo(string workingDirectory)
        {
            Need(CanonicalLinux(workingDirectory), "session_directory");
            var info = new ProcessStartInfo(Executable)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string argument in Arguments)
                info.ArgumentList.Add(argument);
            info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            info.Environment["DOTNET_NOLOGO"] = "1";
            info.Environment.Remove("OS_SMOKE_EXPECTED_BODY");
            info.Environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1";
            info.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
            info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            info.Environment["DOTNET_ENVIRONMENT"] = "Production";
            info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            return info;
        }
    }
}
