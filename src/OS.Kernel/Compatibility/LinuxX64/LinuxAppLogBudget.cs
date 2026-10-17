namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>
/// Finite stdout/stderr byte and record charges for one Linux process lifetime.
/// Keep one instance in the process owner and serialize every access through the
/// existing syscall gate. No allocation, buffering, delivery or synchronization
/// occurs here. Copies are independent budgets, not additional process authority.
/// </summary>
public struct LinuxAppLogBudget
{
    /// <summary>Largest body admitted by the existing descriptor serial sink.</summary>
    public const uint MaximumRecordBytes = 8192;

    /// <summary>Zero/default is rejection; exhausted quota is a distinct failure.</summary>
    public enum ChargeStatus : byte { Rejected, Accepted, QuotaExceeded }

    private uint maximumBytes, maximumRecords, chargedBytes, chargedRecords;
    private uint stdoutBytes, stderrBytes;

    /// <summary>True only after successful initialization of this owner lifetime.</summary>
    public readonly bool IsInitialized => maximumBytes != 0 && maximumRecords != 0;
    /// <summary>Body bytes charged before delivery, including failed deliveries.</summary>
    public readonly uint ChargedBytes => chargedBytes;
    /// <summary>Records charged before delivery, including failed deliveries.</summary>
    public readonly uint ChargedRecords => chargedRecords;
    /// <summary>Charged fd1 bytes; this is not evidence of successful delivery.</summary>
    public readonly uint StdoutChargedBytes => stdoutBytes;
    /// <summary>Charged fd2 bytes; this is not evidence of successful delivery.</summary>
    public readonly uint StderrChargedBytes => stderrBytes;

    /// <summary>
    /// Initializes fresh caller-owned state with positive finite limits. Invalid
    /// limits or a second initialization return false without changing state.
    /// The owner must retain the state until every writer has stopped.
    /// </summary>
    public bool Initialize(uint byteLimit, uint recordLimit)
    {
        if (IsInitialized || byteLimit == 0 || recordLimit == 0) return false;
        maximumBytes = byteLimit; maximumRecords = recordLimit; return true;
    }

    /// <summary>
    /// Charges a nonempty fd1/fd2 record before the serial side effect. Rejected
    /// and exhausted calls change nothing. An accepted charge is never refunded,
    /// including after partial or failed sink delivery, so retries stay bounded.
    /// Caller serialization is required; concurrent unsynchronized use is unsupported.
    /// </summary>
    public ChargeStatus Charge(uint stream, uint bytes)
    {
        if (!IsInitialized || (stream != 1 && stream != 2) || bytes == 0 || bytes > MaximumRecordBytes)
            return ChargeStatus.Rejected;
        if (chargedRecords == maximumRecords || bytes > maximumBytes - chargedBytes)
            return ChargeStatus.QuotaExceeded;
        chargedBytes += bytes; chargedRecords++;
        if (stream == 1) stdoutBytes += bytes; else stderrBytes += bytes;
        return ChargeStatus.Accepted;
    }
}
