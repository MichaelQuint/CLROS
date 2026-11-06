namespace OS.Drivers;

using OS.Std.Binary;

public enum EnaAdminFeatureId : byte
{
    DeviceAttributes = 1,
    MaxQueuesNumber = 2,
    HardwareHints = 3,
    LowLatencyQueue = 4,
    ExtraPropertiesStrings = 5,
    ExtraPropertiesFlags = 6,
    MaxQueuesExtended = 7,
    RssHashFunction = 10,
    StatelessOffloadConfiguration = 11,
    RssIndirectionTableConfiguration = 12,
    Mtu = 14,
    RssHashInput = 18,
    InterruptModeration = 20,
    AenqConfiguration = 26,
    LinkConfiguration = 27,
    HostAttributesConfiguration = 28,
    PhcConfiguration = 29
}
