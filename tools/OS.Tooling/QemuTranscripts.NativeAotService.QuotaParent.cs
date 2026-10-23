namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Original parent, complete quota leaf and final outer accounting.
    /// Does not establish native ABI, independent packet/peer overlap, process
    /// ownership, budget fit or guest/issue acceptance.</summary>
    public readonly record struct ServiceQuotaCaptureReport(WebParentReport Parent, ServiceQuotaReplayReport Leaf,
        ServiceEnvelopeInspection Envelope);

    /// <summary>Synchronously borrow original package/program/raw serial and
    /// hold the current source files while checking exact firmware/APIC/NIC
    /// parent bytes, quota scheduler/syscall/memory/GC/transport replay and outer
    /// cleanup. Only an explicitly prepared QuotaFailure program may enter;
    /// its factory separately binds current sources and the reviewed original
    /// publication/index. Invalid or incomplete inputs throw. No input
    /// is modified or executed; other evidence and acceptance remain mandatory.</summary>
    public static ServiceQuotaCaptureReport ValidateNativeAotServiceQuotaCapture(string root, byte[] package,
        NativeAotServiceProgram program, ReadOnlySpan<byte> raw)
    {
        Need(program is not null && program.Contract == NativeAotServiceContract.QuotaFailure &&
            string.Equals(Path.GetFullPath(root), program.SourceRoot,
                OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal), "service_quota_program_owner");
        Need(raw.Length is >FirmwarePrefixBytes and <=128 *1024 *1024, "service_quota_capture_extent");
        using AuthorityFiles sources = new(); NativeAotServiceProgram.HoldSources(root, sources);
        var view = NativeAotServiceProgram.RequirePackage(package, NativeAotServiceContract.QuotaFailure);
        ReadOnlySpan<byte> outer = "TEST_START m16-011\r\nTEST_START m16-012\r\n"u8;
        int at = raw.IndexOf("TEST_START m16-011\r\n"u8);
        Need(at >= FirmwarePrefixBytes && raw[at-2] ==13 && raw[at-1] ==10 && raw[at..].StartsWith(outer), "service_quota_capture_start");
        var parent = InspectNativeAotWebParentForTests(package, raw[..at]);
        var framed = NativeAotThreadingOutput.ReadService(raw[at..]);
        var leaf = ReplayServiceQuota(framed, package.AsSpan((int)view.PayloadOffset,(int)view.PayloadBytes).ToArray(),
            program!.IsInstruction, program!.IsSyscallReturn, parent.UserRoot, parent.KernelRoot, view.Manifest.MemoryBytes);
        string[] rows = framed.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        string prefix = DecodeUtf8(raw[FirmwarePrefixBytes..at].ToArray());
        string[] parents = prefix[..^2].Split("\r\n", StringSplitOptions.None);
        ulong[] allocator = PrefixRow(parents, "M07_ARP A",4), binding = PrefixRow(parents, "M07_ARP 0",4);
        var peer = ReadRecord(parents.Single(row => row.StartsWith("M16_011_PEER ", StringComparison.Ordinal)), "M16_011_PEER",
            "mac:dec", "sent:dec", "received:dec", "start:dec", "end:dec");
        var envelope = InspectNativeAotServiceQuotaEnvelopeForTests(rows, parent, leaf.Protocol, leaf.Random, leaf.Transport, leaf.Denial,
            (allocator[0],allocator[1],allocator[2],binding[2],peer["sent"],peer["received"]));
        _ = NativeAotServiceProgram.RequirePackage(package, NativeAotServiceContract.QuotaFailure);
        NativeAotServiceProgram.HoldSources(root, sources); sources.VerifyUnchanged(); return new(parent, leaf, envelope);
    }
}
