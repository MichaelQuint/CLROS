namespace OS.Drivers;
public static class VirtioPciQueueNotificationFeatureBits
{
    public const int NotificationDataBit = 38;
    public const int NotificationConfigDataBit = 39;
    public const ulong NotificationData = 1UL << NotificationDataBit;
    public const ulong NotificationConfigData = 1UL << NotificationConfigDataBit;
}
