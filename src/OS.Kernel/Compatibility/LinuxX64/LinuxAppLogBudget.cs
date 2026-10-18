using OS.Manifest;
using OS.Std.Text;

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>
/// Finite stdout/stderr byte and record charges for one Linux process lifetime.
/// Keep one instance in the process owner and serialize every access through the
/// existing syscall gate. No allocation, buffering, delivery or synchronization
/// occurs here. Copies are independent budgets, not additional process authority.
/// </summary>
public struct LinuxAppLogBudget
{
    /// <summary>Default process-lifetime byte charge, shared by stdout and stderr.</summary>
    public const uint DefaultByteLimit = 1024 * 1024;
    /// <summary>Default process-lifetime sink-record charge across both streams.</summary>
    public const uint DefaultRecordLimit = 4096;
    /// <summary>Explicit records64 diagnostic limit; never selected by absence.</summary>
    public const uint QuotaRecordLimit = 64;

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
    /// Reads the exact CLROS_LOG_BUDGET_CASE=records64 selector from an already
    /// validated immutable manifest; absence selects DefaultRecordLimit. The
    /// caller owns readable lower-canonical bytes and matching parsed offsets,
    /// retains both unchanged for this call, and separately admits the M10
    /// diagnostic/process owner. No allocation, I/O or execution authority is
    /// supplied. Other environment keys are ignored after bounds checks.
    /// Invalid views, extents, duplicate selectors or values return false/zero.
    /// </summary>
    public static unsafe bool TryReadRecordLimit(byte* bytes, uint length, LinuxElfManifest manifest, out uint records)
    {
        records = 0;
        if (bytes == null || length == 0 || length > LinuxElfPackage.MaximumManifestBytes ||
            (ulong)bytes > 0x0000800000000000UL - length || !manifest.Succeeded ||
            manifest.ByteCount != length || manifest.EnvironmentCount > 16) return false;
        bool found = false;
        for (uint i = 0; i < manifest.EnvironmentCount; i++)
        {
            uint row = i * 4, key = manifest.EnvironmentRanges[row], keyBytes = manifest.EnvironmentRanges[row + 1];
            uint value = manifest.EnvironmentRanges[row + 2], valueBytes = manifest.EnvironmentRanges[row + 3];
            if (keyBytes == 0 || key > length || keyBytes > length - key || value > length || valueBytes > length - value) return false;
            // CLROS_LOG_BUDGET_CASE, packed independently of managed strings.
            if (!AsciiPointerMatcher.Matches(bytes + key, keyBytes, 21,
                0x4f4c5f534f524c43UL, 0x5445474455425f47UL, 0x000000455341435fUL)) continue;
            if (found || !AsciiPointerMatcher.Matches(bytes + value, valueBytes, 9, 0x367364726f636572UL, 0x34UL)) return false;
            found = true;
        }
        records = found ? QuotaRecordLimit : DefaultRecordLimit;
        return true;
    }

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
