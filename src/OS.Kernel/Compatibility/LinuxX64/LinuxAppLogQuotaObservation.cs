namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>
/// Observes the explicit records64 logging control in one serialized process
/// lifetime. Counts actual copied callbacks and joins rejected budget charges
/// to real EIO results. Descriptor writev coalesces its vectors into one sink
/// callback, so a rejected charge cannot fabricate a partial vector result.
/// Owns value-only counters/catalog state;
/// allocates nothing and retains no borrowed pointer. It neither charges a
/// budget nor sends output, performs a syscall or grants HTTP/guest authority.
/// Malformed, early, unjoined or contradictory observations fail permanently.
/// </summary>
public unsafe struct LinuxAppLogQuotaObservation
{
    private LinuxAppLogObservation catalog;
    private uint records, bytes, callBytes, callStream, deniedStream, deniedBytes;
    private uint deniedCalls, eioCalls;
    private ulong callWriter, deniedWriter;
    private bool pendingDenial, stopped, failed;

    /// <summary>Actual successfully copied sink callbacks; bounded by 64.</summary>
    public readonly uint CopiedRecords => records;
    /// <summary>Actual successfully copied bytes; rejected bytes are excluded.</summary>
    public readonly uint CopiedBytes => bytes;
    /// <summary>Copied bytes in the current not-yet-recorded syscall.</summary>
    public readonly uint CopiedCallBytes => callBytes;
    /// <summary>Complete stdout catalog rows before exhaustion.</summary>
    public readonly uint StdoutRecords => catalog.StdoutRecords;
    /// <summary>Complete stderr catalog rows before exhaustion.</summary>
    public readonly uint StderrRecords => catalog.StderrRecords;
    /// <summary>Denied calls joined to actual results, excluding the pending one.</summary>
    public readonly uint DeniedCalls => deniedCalls;
    /// <summary>Actual EIO returns joined to rejected charges with no copied bytes.</summary>
    public readonly uint EioCalls => eioCalls;
    /// <summary>Sticky invalid observation; no later event restores readiness.</summary>
    public readonly bool Failed => failed;
    /// <summary>Exhausted 64 callbacks, a catalog prefix and a joined real EIO.</summary>
    public readonly bool Complete => !failed && stopped && !pendingDenial && callBytes == 0 && records == LinuxAppLogBudget.QuotaRecordLimit && eioCalls != 0;

    /// <summary>
    /// Borrows 1..8192 readable copied fd1/2 bytes after successful serial delivery.
    /// Identity is the nonzero verified current writer. Call RecordCall after each
    /// actual syscall before observing a later one. Output after the first quota
    /// rejection is invalid; failed delivery is not a copied observation.
    /// </summary>
    public bool Observe(uint stream, ulong identity, byte* source, uint count)
    {
        if (failed)
            return false;
        if (stopped || records == LinuxAppLogBudget.QuotaRecordLimit || stream is not (1 or 2) || identity == 0 ||
            source == null || count is 0 or > LinuxAppLogBudget.MaximumRecordBytes ||
            count > LinuxAppLogBudget.DefaultByteLimit - bytes || callBytes != 0)
            return Fail();
        records++;
        bytes += count;
        callBytes = count;
        callStream = stream;
        callWriter = identity;
        return catalog.Observe(stream, identity, source, count) || Fail();
    }

    /// <summary>
    /// Records a real pre-delivery QuotaExceeded charge without reading/publishing
    /// its rejected payload. Supplied owner counters must match all 64 successful
    /// copied callbacks. Require a prior catalog row; exhausting startup alone
    /// cannot qualify this exercise. A syscall has exactly one coalesced sink
    /// callback, so copied bytes and denial cannot share a call. A later
    /// RecordCall must join its actual result and writer/fd.
    /// </summary>
    public bool Deny(uint stream, ulong identity, uint count, uint chargedRecords, uint chargedBytes)
    {
        if (failed)
            return false;
        if (pendingDenial || callBytes != 0 || stream is not (1 or 2) || identity == 0 || count is 0 or > 8192 ||
            records != LinuxAppLogBudget.QuotaRecordLimit || chargedRecords != records || chargedBytes != bytes ||
            catalog.StdoutRecords + catalog.StderrRecords == 0)
            return Fail();
        pendingDenial = true;
        stopped = true;
        deniedStream = stream;
        deniedWriter = identity;
        deniedBytes = count;
        return true;
    }

    /// <summary>
    /// Joins the actual captured call/result in the same serialized order. A
    /// rejected coalesced write/writev callback must return EIO5, with no copied
    /// bytes from that call. Never refund/reset counters.
    /// Ordinary pre-quota rows retain the complete catalog/writev checks. After
    /// exhaustion no positive output return or unobserved EIO is accepted.
    /// Invalid input returns false and permanently fails this value.
    /// </summary>
    public bool RecordCall(ulong identity, LinuxSyscallFrame call, LinuxSyscallResult result)
    {
        if (failed)
            return false;
        if (identity == 0)
            return Fail();
        bool output = call.Number is 1 or 20 && call.Argument0 is 1 or 2;
        if (callBytes != 0 && (!output || identity != callWriter || call.Argument0 != callStream ||
            result.Action != LinuxSyscallAction.Return || result.Value != callBytes ||
            call.Number == 1 && (call.Argument2 == 0 || call.Argument2 > LinuxDescriptorServices.MaximumIo ||
                (call.Argument2 < LinuxAppLogBudget.MaximumRecordBytes ? call.Argument2 : LinuxAppLogBudget.MaximumRecordBytes) != callBytes) ||
            call.Number == 20 && (call.Argument2 == 0 || call.Argument2 > LinuxDescriptorServices.MaximumVectors)))
            return Fail();
        if (output && result.Action == LinuxSyscallAction.Return && result.Value > 0 && callBytes == 0)
            return Fail();
        if (pendingDenial)
        {
            if (!output || identity != deniedWriter || call.Argument0 != deniedStream || result.Action != LinuxSyscallAction.Return ||
                result.Value != -5 ||
                call.Number == 1 && (call.Argument2 == 0 || call.Argument2 > LinuxDescriptorServices.MaximumIo ||
                    (call.Argument2 < LinuxAppLogBudget.MaximumRecordBytes ? call.Argument2 : LinuxAppLogBudget.MaximumRecordBytes) != deniedBytes) ||
                call.Number == 20 && (call.Argument2 == 0 || call.Argument2 > LinuxDescriptorServices.MaximumVectors))
                return Fail();
            if (deniedCalls == uint.MaxValue || eioCalls == uint.MaxValue)
                return Fail();
            deniedCalls++;
            eioCalls++;
            pendingDenial = false;
            deniedWriter = 0;
            deniedStream = 0;
            deniedBytes = 0;
        }
        else if (!stopped)
        {
            uint previous = catalog.WritevRecords;
            if (!catalog.RecordCall(identity, call, result) ||
                catalog.WritevRecords != previous && call.Argument2 != 2)
                return Fail();
        }
        else if (output && result.Action == LinuxSyscallAction.Return && result.Value == -5 ||
            call.Number == 20 && call.Argument0 == 1234)
            return Fail();
        callBytes = 0;
        callStream = 0;
        callWriter = 0;
        return true;
    }

    private bool Fail()
    {
        failed = true;
        return false;
    }
}
