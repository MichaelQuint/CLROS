using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;

internal static partial class KestrelSmokePackageContractTests
{
    private sealed class RunningMinimalApiSmoke : IDisposable
    {
        private readonly Process process;
        private readonly Task<string> stdout;
        private readonly Task<string> stderr;
        private bool disposed;
        public RunningMinimalApiSmoke(Process process, Task<string> stdout, Task<string> stderr, Uri baseUri)
        {
            this.process = process;
            this.stdout = stdout;
            this.stderr = stderr;
            this.BaseUri = baseUri;
        }

        public Uri BaseUri { get; }
        public bool HasExited => this.process.HasExited;

        public string StopAndCaptureOutput()
        {
            if (!this.process.HasExited)
            {
                try
                {
                    this.process.Kill(entireProcessTree: true);
                }
                catch
                {
                // The process may have exited between the check and the kill request.
                }
            }

            try
            {
                this.process.WaitForExit(10000);
            }
            catch
            {
            }

            string output = string.Empty;
            try
            {
                if (this.stdout.Wait(TimeSpan.FromSeconds(2)))
                {
                    output += this.stdout.GetAwaiter().GetResult();
                }
            }
            catch
            {
            }

            try
            {
                if (this.stderr.Wait(TimeSpan.FromSeconds(2)))
                {
                    output += this.stderr.GetAwaiter().GetResult();
                }
            }
            catch
            {
            }

            return output;
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.StopAndCaptureOutput();
            this.process.Dispose();
        }
    }
}
