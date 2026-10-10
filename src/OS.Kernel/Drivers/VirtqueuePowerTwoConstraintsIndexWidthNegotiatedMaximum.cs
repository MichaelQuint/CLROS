namespace OS.Drivers;

public enum VirtqueueSizeStatus : byte
{
    Uninitialized, Success, InvalidRequested, InvalidMaximum, Unavailable,
    ExceedsMaximum, InvalidIndex, InvalidProgress
}

/// <summary>Split-queue numeric admission only. The adapter supplies the actual
/// selected queue's maximum and owns register readback and queue lifetime.</summary>
public static class BoundedVirtqueueSize
{
    public const uint Maximum = 32768;

    public static VirtqueueSizeStatus Validate(uint requested, uint maximum, out ushort size)
    {
        size = 0;
        if (!PowerOfTwo(requested)) return VirtqueueSizeStatus.InvalidRequested;
        if (maximum == 0) return VirtqueueSizeStatus.Unavailable;
        if (!PowerOfTwo(maximum)) return VirtqueueSizeStatus.InvalidMaximum;
        if (requested > maximum) return VirtqueueSizeStatus.ExceedsMaximum;
        size = (ushort)requested;
        return VirtqueueSizeStatus.Success;
    }

    public static VirtqueueSizeStatus TryDescriptor(uint value, ushort size, out ushort index)
    {
        index = 0;
        if (!PowerOfTwo(size)) return VirtqueueSizeStatus.InvalidRequested;
        if (value >= size) return VirtqueueSizeStatus.InvalidIndex;
        index = (ushort)value;
        return VirtqueueSizeStatus.Success;
    }

    // A bounded exclusive producer cannot have more than size outstanding
    // entries. This distance alone does not authenticate epochs or completion.
    public static VirtqueueSizeStatus CountPending(ushort consumed, ushort produced,
        ushort size, out ushort count)
    {
        count = 0;
        if (!PowerOfTwo(size)) return VirtqueueSizeStatus.InvalidRequested;
        ushort distance = unchecked((ushort)(produced - consumed));
        if (distance > size) return VirtqueueSizeStatus.InvalidProgress;
        count = distance;
        return VirtqueueSizeStatus.Success;
    }

    private static bool PowerOfTwo(uint value) =>
        value != 0 && value <= Maximum && (value & (value - 1)) == 0;
}
