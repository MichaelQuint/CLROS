namespace OS.Drivers;

public enum PciCapabilityWalkStatus : byte
{
    Uninitialized = 0,
    CompleteEmpty = 1,
    CompletePresent = 2,
    InvalidStorage = 3,
    Truncated = 4,
    AbsentDevice = 5,
    UnsupportedHeader = 6,
    OffsetOutOfRange = 7,
    OffsetUnaligned = 8,
    LoopDetected = 9
}

public readonly struct PciCapabilityWalkResult
{
    public readonly PciCapabilityWalkStatus Status;
    public readonly uint Count;
    public readonly uint FailingOffset;
    public bool IsInitialized => Status != PciCapabilityWalkStatus.Uninitialized;
    public bool Succeeded => Status == PciCapabilityWalkStatus.CompleteEmpty || Status == PciCapabilityWalkStatus.CompletePresent;

    internal PciCapabilityWalkResult(PciCapabilityWalkStatus status, uint count, uint failingOffset)
    {
        Status = status;
        Count = count;
        FailingOffset = failingOffset;
    }
}

/// <summary>
/// Validates only the conventional capability links in a stable caller-owned snapshot.
/// Requires the complete 256-byte snapshot and ignores any extended configuration bytes.
/// Unknown IDs are accepted; capability payloads and device access are outside this contract.
/// Failure Count reports completed headers; FailingOffset identifies the rejected pointer,
/// header-type byte, or first missing snapshot byte. No input bytes are written.
/// </summary>
public static unsafe class PciCapabilityWalk
{
    public const uint ConfigurationBytes = 256;
    public const uint MaximumCapabilities = 48;

    public static PciCapabilityWalkResult Validate(byte* config, uint length)
    {
        if (config == null || (ulong)config > ~0UL - ConfigurationBytes)
            return new(PciCapabilityWalkStatus.InvalidStorage, 0, 0);
        if (length < ConfigurationBytes)
            return new(PciCapabilityWalkStatus.Truncated, 0, length);
        if (config[0] == 0xff && config[1] == 0xff)
            return new(PciCapabilityWalkStatus.AbsentDevice, 0, 0);
        if ((config[0x0e] & 0x7f) > 1)
            return new(PciCapabilityWalkStatus.UnsupportedHeader, 0, 0x0e);
        if ((config[6] & 0x10) == 0)
            return new(PciCapabilityWalkStatus.CompleteEmpty, 0, 0);

        uint offset = config[0x34], count = 0;
        ulong visited = 0;
        // There are exactly 48 admitted aligned offsets. A repeated offset is rejected
        // before another header read, so even a malicious cycle cannot extend the walk.
        while (offset != 0)
        {
            if (offset < 0x40 || offset > 0xfc)
                return new(PciCapabilityWalkStatus.OffsetOutOfRange, count, offset);
            if ((offset & 3) != 0)
                return new(PciCapabilityWalkStatus.OffsetUnaligned, count, offset);
            ulong bit = 1UL << (int)(offset >> 2);
            if ((visited & bit) != 0)
                return new(PciCapabilityWalkStatus.LoopDetected, count, offset);
            visited |= bit;
            // The ID at offset is unrestricted. Its next byte completes the header.
            offset = config[offset + 1];
            count++;
        }
        return new(count == 0 ? PciCapabilityWalkStatus.CompleteEmpty : PciCapabilityWalkStatus.CompletePresent, count, 0);
    }
}
