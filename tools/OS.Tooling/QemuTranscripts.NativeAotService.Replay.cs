using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Borrow an original package, sealed prepared service program and
    /// bounded complete raw leaf (including preceding seed/bound observations).
    /// Hold current source files while replaying startup, all syscall/memory/
    /// scheduler/wait/signal generations, random/compact ownership and genuine
    /// shutdown/reaps. Invalid or incomplete inputs throw. Parent provenance,
    /// independent packets/HTTP/control witnesses and original native ABI review
    /// remain mandatory; this method alone never accepts a guest run.</summary>

    public static ServiceReplayReport ValidateNativeAotServiceTail(string root, byte[] package, NativeAotServiceProgram program, ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot)
    {
        Need(program is not null && string.Equals(Path.GetFullPath(root), program.SourceRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "service_program_owner");
        using AuthorityFiles sources = new();
        NativeAotServiceProgram.HoldSources(root, sources);
        var view = NativeAotServiceProgram.RequirePackage(package, program!.Lifecycle);
        var report = ReplayService(NativeAotThreadingOutput.ReadService(rawTail), package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).ToArray(), program.IsInstruction, program.IsSyscallReturn, userRoot, kernelRoot, program.Lifecycle, view.Manifest.MemoryBytes);
        _ = NativeAotServiceProgram.RequirePackage(package, program.Lifecycle);
        NativeAotServiceProgram.HoldSources(root, sources);
        sources.VerifyUnchanged();
        return report;
    }

    private static ServiceReplayReport ReplayService(NativeAotThreadingOutput.Capture framed, byte[] elf, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot, bool lifecycle, ulong entitlementBytes)
    {
        Need(framed.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "service_complete_terminal");
        string[] original = framed.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        var startup = ReadServiceStartup(framed, out var metadata, lifecycle);
        var gc = ReadServiceGc(framed, entitlementBytes, lifecycle, out var gcBudgetPosition, out var gcMemoryPosition);
        if (gcBudgetPosition.HasValue)
            metadata.Add(gcBudgetPosition.Value);
        if (gcMemoryPosition.HasValue)
            metadata.Add(gcMemoryPosition.Value);
        var memory = ReadServiceMemory(framed, lifecycle, gc, out var memoryPositions);
        metadata.UnionWith(memoryPositions);
        if (lifecycle)
            Need(memory.HasValue && memory.Value.GcArena.HasValue && memory.Value.NativeReserveFits && memory.Value.ConservativeHeadroomFits, "service_memory_headroom");
        var random = ReadServiceRandom(original);
        var transport = ReadServiceTransport(original, out var network);
        metadata.UnionWith(network);
        ServiceRuntimeMetadata(original, startup, metadata);
        for (int i = 0; i < original.Length; i++)
            if (original[i].StartsWith("M11_012_", StringComparison.Ordinal) || original[i] == "M07_033_SEED source=firmware-rng acquisitions=1 secretBytes=32 domain=tcp-isn state=ready firmwareCallbacks=none")
                metadata.Add(i);
        int[] start = Enumerable.Range(0, original.Length).Where(index => original[index] == "TEST_START m16-009").ToArray();
        Need(start.Length == 1, "service_complete_start");
        string[] rows = original.Where((_, index) => index >= start[0] && !metadata.Contains(index)).ToArray();
        var capture = NativeAotSocketGuest.ReadServiceReadiness(new(framed.Stdout, string.Join("\r\n", rows) + "\r\n", framed.WriteLengths.ToArray(), framed.WriteChannels.ToArray(), framed.Stderr));
        var replay = new ThreadingProtocol(NativeAotServicePackage.Application, rows, elf, instruction, syscallReturn, framed.WriteLengths, userRoot, kernelRoot, socketCapture: capture, webSettingsBytes: startup.SettingsBytes, webSettingsFd: startup.SettingsDescriptor, webSettingsCall: (startup.SettingsIdentity, startup.SettingsOrdinal), configuredServiceMemory: lifecycle);
        var protocol = replay.Validate();
        var counts = replay.ServiceSocketCounts();
        Need((ulong)protocol.Calls >= random.Requests && protocol.Reaped == protocol.Clones + 1 && transport.Connections >= 10, "service_complete_joins");
        return new(protocol, startup, random, transport, counts.Accepts, counts.Sent, counts.Received, gc, memory);
    }

    private static void ServiceRuntimeMetadata(string[] rows, ServiceStartupInspection startup, HashSet<int> metadata)
    {
        var calls = ValidateLinuxServiceCallOrdinals(rows);
        var returns = ReadLinuxServiceCallReturns(rows).ToDictionary(row => row.RecordIndex);
        int One(string prefix)
        {
            int[] found = Enumerable.Range(0, rows.Length).Where(index => rows[index].StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Need(found.Length == 1, "service_runtime_metadata_once");
            return found[0];
        }

        int progress = One("M16_014_PROGRESS "), closed = One("M16_014_LISTENER_CLOSE "), randomEnd = One("M11_012_END "), sequenceEnd = One("M07_033_END "), transportEnd = One("M07_030_END "), budgetEnd = One("M16_008_BUDGET_END ");
        const string suffix = " process=live cleanup=pending";
        Need(rows[closed].EndsWith(suffix, StringComparison.Ordinal) && closed > progress && closed + 1 < rows.Length && returns.ContainsKey(closed + 1), "service_listener_close_order");
        var close = ReadRecord(rows[closed][..^suffix.Length], "M16_014_LISTENER_CLOSE", "identity:dec", "fd:dec");
        var call = ReadRecord(calls[closed + 1], "M16_008_CALL", WebCallFields);
        Need(close["fd"] == (ulong)startup.ListenerDescriptor && close["identity"] == call["identity"] && call["nr"] == 3 && call["a0"] == close["fd"] && call["valueBits"] == 0 && call["action"] == 1 && call["status"] == 1, "service_listener_close_call");
        metadata.Add(closed);
        int[] groups = returns.Keys.Where(index => ReadRecord(calls[index], "M16_008_CALL", WebCallFields)["nr"] == 231).ToArray();
        Need(groups.Length == 1, "service_group_exit_once");
        int group = groups[0];
        Need(group > closed && transportEnd > group && randomEnd > transportEnd && sequenceEnd == randomEnd + 1 && budgetEnd > sequenceEnd && Enumerable.Range(group + 1, randomEnd - group - 1).Any(index => rows[index].StartsWith("M16_008_REAP ", StringComparison.Ordinal)) && !returns.Keys.Any(index => index > group), "service_runtime_cleanup_order");
    }

    /// <summary>Value-only complete scheduler/syscall replay seam. Caller ELF,
    /// predicates, settings and framed observations confer no source, program,
    /// application-output, transport, runner or guest authority. Unsupported or
    /// incomplete lifetimes throw; inputs are borrowed without modification.
    /// Configured-memory selection tests values only, without GC admission.</summary>

    public static ThreadingProtocolReport InspectNativeAotServiceProtocolForTests(byte[] elf, string[] rows, IReadOnlyList<int> lengths, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot, uint settingsBytes, int settingsFd, ulong settingsIdentity, ulong settingsOrdinal, bool configuredServiceMemory = false)
    {
        var framed = new NativeAotThreadingOutput.Capture("", string.Join("\r\n", rows) + "\r\n", lengths.ToArray(), Enumerable.Repeat(1U, lengths.Count).ToArray(), "");
        var capture = NativeAotSocketGuest.ReadServiceReadiness(framed);
        return new ThreadingProtocol(NativeAotServicePackage.Application, Rows(capture.Output), elf, instruction, syscallReturn, capture.Output.WriteLengths, userRoot, kernelRoot, socketCapture: capture, webSettingsBytes: settingsBytes, webSettingsFd: settingsFd, webSettingsCall: (settingsIdentity, settingsOrdinal), configuredServiceMemory: configuredServiceMemory).Validate();
    }
}
