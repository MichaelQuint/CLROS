namespace OS.Drivers;

public enum VirtioFeatureStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    UnsupportedRequested = 2,
    MissingVersion1 = 3,
    InvalidStatus = 4,
    FeaturesRejected = 5,
    DeviceFailed = 6,
    MissingReceiveBuffers = 7
}

/// <summary>Pure admission for the bounded VERSION_1-only feature profile.</summary>
public static class BoundedVirtioFeatures
{
    public const ulong Version1 = 1UL << 32;
    public const ulong BlockReadOnly = 1UL << 5;
    public const ulong NetSingleBufferReceive = Version1 | (1UL << 15);

    /// <summary>Explicit receive profile. MRG_RXBUF makes the device write the
    /// buffer count; the receive adapter still posts one buffer and rejects
    /// any count other than one. No checksum/GSO/other optional feature.</summary>
    public static VirtioFeatureStatus SelectNetReceive(ulong offered, out ulong selected)
    {
        selected = 0;
        if ((offered & Version1) == 0) return VirtioFeatureStatus.MissingVersion1;
        if ((offered & NetSingleBufferReceive) != NetSingleBufferReceive)
            return VirtioFeatureStatus.MissingReceiveBuffers;
        selected = NetSingleBufferReceive;
        return VirtioFeatureStatus.Success;
    }

    /// <summary>One modern block queue: VERSION_1 and RO when offered. All other
    /// device and transport features, including MQ, packed and event-index, are declined.</summary>
    public static VirtioFeatureStatus SelectBlock(ulong offered, out ulong selected)
    {
        selected = 0;
        if ((offered & Version1) == 0) return VirtioFeatureStatus.MissingVersion1;
        selected = Version1 | (offered & BlockReadOnly);
        return VirtioFeatureStatus.Success;
    }

    public static VirtioFeatureStatus Select(ulong offered, ulong requested, out ulong selected)
    {
        selected = 0;
        if (requested != Version1) return VirtioFeatureStatus.UnsupportedRequested;
        if ((offered & Version1) == 0) return VirtioFeatureStatus.MissingVersion1;
        selected = Version1;
        return VirtioFeatureStatus.Success;
    }

    /// <summary>Checks the device status read back immediately after requesting FEATURES_OK.</summary>
    public static VirtioFeatureStatus ValidateFeaturesOk(byte status)
    {
        if ((status & 0xc0) != 0) return VirtioFeatureStatus.DeviceFailed;
        if (status == 3) return VirtioFeatureStatus.FeaturesRejected;
        if (status == 11) return VirtioFeatureStatus.Success;
        return VirtioFeatureStatus.InvalidStatus;
    }
}
