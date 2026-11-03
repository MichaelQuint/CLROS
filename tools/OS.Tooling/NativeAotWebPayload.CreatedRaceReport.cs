using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using OS.Tooling.Native;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    // Keep actual creation identities alive across writer closure and the first
    // immutable AuthorityFiles open. The bridge grants read access while the
    // creator still excludes other writers, and never permits delete sharing.
    internal sealed class CreatedRaceReport : IDisposable
    {
        private readonly FileStream bridge;
        private readonly FileIdentity identity;
        private readonly string hash;
        private CreatedRaceReport(FileStream bridge, FileIdentity identity, string hash)
        {
            this.bridge = bridge;
            this.identity = identity;
            this.hash = hash;
        }

        internal static CreatedRaceReport Retain(FileStream creator, byte[] written)
        {
            var identity = NativeFile.GetIdentity(creator.SafeFileHandle);
            FileStream? bridge = null;
            try
            {
                RaceNeed(identity.LinkCount == 1 && identity.Length == (ulong)written.Length, "created_report_shape");
                bridge = new(creator.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var held = NativeFile.GetIdentity(bridge.SafeFileHandle);
                RaceNeed(held.Key == identity.Key && held.LinkCount == 1 && held.Length == identity.Length, "created_report_identity");
                var result = new CreatedRaceReport(bridge, identity, Convert.ToHexString(SHA256.HashData(written)));
                bridge = null;
                return result;
            }
            finally
            {
                bridge?.Dispose();
            }
        }

        internal void Verify(AuthorityFile file)
        {
            var held = NativeFile.GetIdentity(bridge.SafeFileHandle);
            RaceNeed(file.InitialIdentity.Key == identity.Key && held.Key == identity.Key && held.LinkCount == 1 && file.InitialIdentity.LinkCount == 1 && held.Length == identity.Length && file.Length == (long)identity.Length && file.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase), "created_report_changed");
        }

        public void Dispose() => bridge.Dispose();
    }
}
