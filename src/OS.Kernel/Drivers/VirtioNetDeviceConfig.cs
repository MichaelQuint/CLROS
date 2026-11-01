namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public readonly struct VirtioNetDeviceConfig
{
    public const int MacAddressOffset = 0;
    public const int MacAddressLength = MacAddress.Length;
    public const int MinimumLength = MacAddressOffset + MacAddressLength;
    public const int StatusOffset = MinimumLength;
    public const int StatusLength = sizeof(ushort);
    public const int StatusConfigLength = StatusOffset + StatusLength;
    public const ushort StatusLinkUp = 0x0001;
    public const ushort StatusAnnounce = 0x0002;
    public VirtioNetDeviceConfig(MacAddress macAddress, bool hasStatus, ushort statusBits)
    {
        MacAddress = macAddress;
        HasStatus = hasStatus;
        StatusBits = statusBits;
    }

    public MacAddress MacAddress { get; }
    public bool HasStatus { get; }
    public ushort StatusBits { get; }
    public bool IsLinkUp => HasStatus && (StatusBits & StatusLinkUp) != 0;
    public bool HasAnnounce => HasStatus && (StatusBits & StatusAnnounce) != 0;

    public static VirtioNetDeviceConfigParseResult TryParse(ReadOnlySpan<byte> source)
    {
        if (source.Length < MinimumLength || source.Length == StatusConfigLength - 1)
        {
            return VirtioNetDeviceConfigParseResult.Fail(VirtioNetDeviceConfigStatus.BufferTooSmall, source.Length);
        }

        MacAddress macAddress = new MacAddress(source[0], source[1], source[2], source[3], source[4], source[5]);
        if (macAddress.IsZero)
        {
            return VirtioNetDeviceConfigParseResult.Fail(VirtioNetDeviceConfigStatus.ZeroMacAddress, source.Length);
        }

        if (macAddress.IsBroadcast)
        {
            return VirtioNetDeviceConfigParseResult.Fail(VirtioNetDeviceConfigStatus.BroadcastMacAddress, source.Length);
        }

        bool hasStatus = source.Length >= StatusConfigLength;
        ushort statusBits = hasStatus ? EndianPrimitives.ReadUInt16LittleEndian(source.Slice(StatusOffset, StatusLength)) : (ushort)0;
        VirtioNetDeviceConfig config = new VirtioNetDeviceConfig(macAddress, hasStatus, statusBits);
        int bytesConsumed = hasStatus ? StatusConfigLength : MinimumLength;
        return VirtioNetDeviceConfigParseResult.Success(config, source.Length, bytesConsumed);
    }
}
