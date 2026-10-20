using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Internal QemuSmoke-only original-output leases and copied bytes.
    /// Creation requires the actual privately constructed observed peer owner,
    /// its identical runner object and an explicitly prepared quota program.
    /// Existing input/native locks remain caller-owned throughout this lifetime.
    /// Holding files proves no protocol/packet/syscall/overlap/cleanup verdict or
    /// guest acceptance. Inspect must wrap the future complete consumer so every
    /// held output, original Completion and borrowed input is reverified even
    /// when inspection throws. Dispose releases only this bundle's output set.</summary>
    internal sealed class ServiceQuotaOwnedEvidence : IDisposable
    {
        private readonly AuthorityFiles files, inputs;
        private readonly AuthorityFile[] originalInputs;
        private readonly NativeAotServiceQuotaPeer.Completion peer;
        private readonly byte[][] snapshots;
        private bool disposed;
        private ServiceQuotaOwnedEvidence(AuthorityFiles held, AuthorityFiles borrowed,
            NativeAotServiceQuotaPeer.Completion completion, byte[][] copied, AuthorityFile[] sources)
        { files = held; inputs = borrowed; peer = completion; snapshots = copied; originalInputs = sources; }

        // Read-only views retain the copied original bytes, never a later path
        // read. A consumer needing byte[] must take its own ToArray copy.
        internal ReadOnlySpan<byte> Serial => Snapshot(0);
        internal ReadOnlySpan<byte> Summary => Snapshot(1);
        internal ReadOnlySpan<byte> Packets => Snapshot(2);
        internal ReadOnlySpan<byte> PeerOutput => Snapshot(3);
        internal ReadOnlySpan<byte> PeerError => Snapshot(4);
        internal ReadOnlySpan<byte> RunnerOutput => Snapshot(5);
        internal ReadOnlySpan<byte> RunnerError => Snapshot(6);
        internal ReadOnlySpan<byte> Wire => Snapshot(7);

        internal NativeAotServiceQuotaPeer.Completion Completion => peer;

        // Read the actual borrowed pre-run package record under the same
        // surrounding Inspect verification; no retrospective path open.
        internal byte[] ReadPackage()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return originalInputs[0].ReadBytes(OS.Manifest.LinuxElfPackage.MaximumBytes, "service_quota_owned_package");
        }

        private ReadOnlySpan<byte> Snapshot(int index)
        { ObjectDisposedException.ThrowIf(disposed, this); return snapshots[index]; }

        internal void Verify()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            inputs.VerifyUnchanged();
            foreach (var source in originalInputs) source.VerifyUnchanged();
            peer.Verify(files); files.VerifyUnchanged();
        }

        internal T Inspect<T>(Func<ServiceQuotaOwnedEvidence, T> inspect)
        {
            ArgumentNullException.ThrowIfNull(inspect); Verify();
            try { return inspect(this); }
            finally { Verify(); }
        }

        public void Dispose()
        { if (disposed) return; disposed = true; files.Dispose(); }

        internal static ServiceQuotaOwnedEvidence Create(string root, QemuSmokeOptions options,
            NativeAotServiceProgram program, NativeAotServiceQuotaPeer.Completion completion,
            ProcessResult runner, ServiceRunnerContext context, AuthorityFiles inputLocks,
            AuthorityFile packageInput, AuthorityFile dumpInput, AuthorityFile imageInput, AuthorityFile efiInput)
        {
            if (options is null || program is null || completion is null || runner is null || inputLocks is null ||
                packageInput is null || dumpInput is null || imageInput is null || efiInput is null)
                throw new ToolFailure("service_quota_owned_input");
            root = Path.GetFullPath(root);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            string serialPath = Path.GetFullPath(options.SerialLog, root), peerPath = serialPath + ".peer.log",
                wirePath = peerPath + ".wire.json";
            Need(options.ServiceQuotaFailure && !options.ServiceLifecycle && !options.DryRun && !options.SignalEpollRace &&
                program.Contract == NativeAotServiceContract.QuotaFailure &&
                string.Equals(root, program.SourceRoot, comparison) && string.Equals(root, completion.Root, comparison) &&
                string.Equals(serialPath, completion.SerialPath, comparison) && string.Equals(peerPath, completion.Path, comparison) &&
                string.Equals(wirePath, completion.WirePath, comparison) && completion.WireSha256 is { Length: 64 } &&
                completion.Wire is { Purpose: "quota-run" } && completion.Wire.Port == context.ForwardPort &&
                completion.Port == context.ForwardPort && context.ForwardPort is >= 1 and <= 65535 &&
                ReferenceEquals(completion.Runner, runner), "service_quota_owned_owner");
            RequireCompleteProcess(runner, "service_quota_owned_runner");
            RequireCompleteProcess(completion.Peer, "service_quota_owned_peer");
            Need(runner.ExitCode == 22 && runner.StandardError.Length == 0 && completion.Peer.ExitCode == 0,
                "service_quota_owned_disposition");
            inputLocks.VerifyUnchanged(); // Preserve the original pre-run locks.
            AuthorityFiles held = new();
            try
            {
                string[] paths = [serialPath, Path.GetFullPath(options.SummaryLog, root), serialPath + ".pcap", peerPath,
                    peerPath + ".stderr.log", completion.RunnerOutputPath, completion.RunnerErrorPath, wirePath];
                long[] limits = [SerialMarkerStateMachine.ContinuingServiceByteCapacity,
                    SerialMarkerStateMachine.ContinuingServiceByteCapacity, 4 * 1024 * 1024, 65_538, 256,
                    8 * 1024 * 1024, 8 * 1024 * 1024, NativeAotServiceQuotaPeer.WireMaximumReportBytes];
                List<AuthorityFile> outputs = [];
                for (int i = 0; i < paths.Length; i++)
                    outputs.Add(held.Open(root, paths[i], "original quota output " + i, "service_quota_owned_output",
                        limits[i], allowEmpty: i is 4 or 6, singleLink: true));
                // Borrow the exact original pre-run records; never substitute
                // fresh retrospective input opens for QemuSmoke's existing locks.
                string[] sourcePaths = [options.LinuxElfPackage, options.LinuxThreadingDisassembly, options.Image, options.EfiArtifact];
                long[] sourceLimits = [OS.Manifest.LinuxElfPackage.MaximumBytes, 128 * 1024 * 1024, 64 * 1024 * 1024, 16 * 1024 * 1024];
                AuthorityFile[] sources = [packageInput, dumpInput, imageInput, efiInput];
                for (int i = 0; i < sourcePaths.Length; i++)
                {
                    Need(sourcePaths[i].Length > 0, "service_quota_owned_source_path");
                    string path = Path.GetFullPath(sourcePaths[i], root);
                    Need(string.Equals(path, sources[i].Path, comparison), "service_quota_owned_source_path");
                    sources[i].CheckSize(sourceLimits[i], allowEmpty: false); sources[i].VerifyUnchanged();
                }
                AuthorityFiles.RequireDistinct([.. outputs, .. sources]);
                _ = NativeAotServiceProgram.RequirePackage(sources[0].ReadBytes(sourceLimits[0], "service_quota_owned_package"),
                    NativeAotServiceContract.QuotaFailure);
                completion.Verify(held);
                byte[][] snapshots = new byte[outputs.Count][];
                for (int i = 0; i < snapshots.Length; i++)
                    snapshots[i] = outputs[i].ReadBytes(limits[i], "service_quota_owned_snapshot");
                var result = new ServiceQuotaOwnedEvidence(held, inputLocks, completion, snapshots, sources);
                result.Verify(); return result;
            }
            catch { held.Dispose(); throw; }
        }
    }

    // No public/value-only authority factory or QemuSmoke caller is added.
    // The existing capture-owner guard and original source/native/image locks
    // remain in place until a complete owned negative consumer is integrated.
    internal static ServiceQuotaOwnedEvidence HoldServiceQuotaEvidence(string root, QemuSmokeOptions options,
        NativeAotServiceProgram program, NativeAotServiceQuotaPeer.Completion completion, ProcessResult runner,
        ServiceRunnerContext context, AuthorityFiles inputLocks, AuthorityFile packageInput, AuthorityFile dumpInput,
        AuthorityFile imageInput, AuthorityFile efiInput) =>
        ServiceQuotaOwnedEvidence.Create(root, options, program, completion, runner, context, inputLocks,
            packageInput, dumpInput, imageInput, efiInput);
}
