using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Join copied scheduler/file intervals to every original indexed
    /// sector observation using the same held image and independent inventory.
    /// Require exact one-request device lifecycle/bytes, unique positive data
    /// coverage, and physical FAT/directory-only residuals inside actual pending
    /// operations. This is a synchronous value join, not a process owner or an
    /// FS-cache replay. The caller retains original source/native/packet authority.</summary>

    public static DiskStaticSectorValues InspectNativeAotDiskStaticSectorValues(DiskStaticReplayReport replay, WebRootDiskInventory.Inspection inventory, byte[] heldImage, NativeDiskReadObservations.Boundary final, WebRootDiskInventory.VerifiedTruncated? truncated = null)
    {
        Need(replay is not null && inventory is not null && replay.Operations.Count > 0 && replay.Operations.Count <= (replay.FullProfile ? DiskStaticExecutionPolicy.MaximumCalls : 4096) && final.Owner == replay.Owner && final.Epoch == replay.Epoch && final.BootstrapCompleted == replay.BootstrapCompleted && final.BootstrapCopied == replay.BootstrapCopied, "disk_static_sector_boundary");
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(inventory);
        Need(replay.Truncated == (truncated is not null), "disk_static_sector_fault_scope");
        if (truncated is not null)
            truncated.RequireValues(inventory, heldImage);
        var device = NativeDiskReadObservations.Inspect(replay.DeviceRows.Select(r => r.Text).ToArray(), heldImage, final, diskStaticFull: replay.FullProfile, allowMultiSector: true);
        var data = NativeDiskStaticReadWindows.Inspect(inventory, device, replay.DeviceRows.Select(r => r.RecordIndex).ToArray(), replay.Reads);
        var metadata = WebRootDiskInventory.ClassifyMetadata(inventory, heldImage, data.Residual.Select(c => c.Observation.DataSector).ToArray(), truncated);
        var operations = replay.Operations.OrderBy(o => o.BeginRecordIndex).ToArray();
        Need(operations.Select(o => o.Request).Distinct().Count() == operations.Length, "disk_static_operation_requests");
        var byRequest = operations.ToDictionary(o => o.Request);
        foreach (var assigned in data.Assigned)
        {
            var window = assigned.Window;
            Need(byRequest.TryGetValue(window.RequestGeneration, out var op) && op.Identity == window.Identity && op.Ordinal == window.Ordinal && op.Number == window.Number && op.BeginRecordIndex == window.BeginRecordIndex && op.CompleteRecordIndex == window.CompleteRecordIndex && op.OutputPrefix == window.OutputPrefix && op.Value == window.ReturnedBytes && assigned.Copy.CopyRecordIndex < op.WakeRecordIndex, "disk_static_data_ready_interval");
        }

        int[] begins = new int[operations.Length], ends = new int[operations.Length];
        for (int i = 0; i < operations.Length; i++)
        {
            var op = operations[i];
            Need(op.BeginRecordIndex >= 0 && op.WakeRecordIndex > op.BeginRecordIndex && op.CompleteRecordIndex > op.WakeRecordIndex && (i == 0 || op.BeginRecordIndex > begins[i - 1]), "disk_static_operation_intervals");
            begins[i] = op.BeginRecordIndex;
            ends[i] = Math.Max(op.WakeRecordIndex, i == 0 ? -1 : ends[i - 1]);
        }

        foreach (var copy in data.Residual)
        {
            int at = Array.BinarySearch(begins, copy.SubmitRecordIndex);
            at = at >= 0 ? at - 1 : ~at - 1;
            Need(at >= 0 && ends[at] > copy.CopyRecordIndex, "disk_static_metadata_pending_interval");
        }

        if (truncated is not null)
        {
            ulong inode = inventory.Find(WebRootDiskInventory.VerifiedTruncated.Target)!.Inode;
            Need(replay.FullProfile && replay.Faults.Count >= 2 && replay.Reads.All(r => r.Inode != inode), "disk_static_fault_read_inventory");
            foreach (var fault in replay.Faults)
            {
                var op = fault.Operation;
                Need(fault.Path == WebRootDiskInventory.VerifiedTruncated.Target && byRequest.TryGetValue(op.Request, out var actual) && actual == op && op.Number is 4 or 6 && op.Value == -5, "disk_static_fault_operation");
                // The mount and raw image are immutable in this scope. An
                // earlier authenticated copy may fill the cache; neither an
                // invented fresh read nor a guessed per-request cache is used.
                foreach (ulong lba in new[]
                {
                    truncated.Fault.Fat0WordOffset / 512,
                    truncated.Fault.Fat1WordOffset / 512
                }

                )
                    Need(data.Residual.Any(c => c.Observation.DataSector == lba && c.CopyRecordIndex < op.WakeRecordIndex), "disk_static_fault_fat_observation");
            }
        }
        else
            Need(replay.Faults.Count == 0, "disk_static_unselected_fault");
        return new(device, data, metadata);
    }

    /// <summary>Borrow one complete inner m16-009 tail, original ELF, instruction
    /// predicates and independent physical inventory. Replay actual source4
    /// waits in the existing full memory/FIFO/epoll/signal model. Descriptor,
    /// request and block generation domains remain separate. Missing, unsupported
    /// or inconsistent rows reject. Inputs supply copied values only; enclosing
    /// original image/native/process/packet/HTTP owners remain mandatory.</summary>

    public static DiskStaticReplayReport InspectNativeAotDiskStaticProtocolForTests(byte[] elf, ReadOnlySpan<byte> raw, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot, WebRootDiskInventory.Inspection inventory, bool diskStaticFull = false, WebRootDiskInventory.VerifiedTruncated? truncated = null)
    {
        Need(inventory is not null, "disk_static_inventory_missing");
        var output = NativeAotThreadingOutput.ReadWeb(raw, diskStaticFull);
        var capture = NativeAotSocketGuest.ReadDiskStaticReadiness(output, diskStaticFull);
        var replay = new ThreadingProtocol(NativeAotStaticFilePublisher.Application, Rows(output), elf, instruction, syscallReturn, output.WriteLengths, userRoot, kernelRoot, socketCapture: capture, diskInventory: inventory, diskStaticFull: diskStaticFull, diskTruncated: truncated);
        return replay.DiskStaticValidate();
    }
}
