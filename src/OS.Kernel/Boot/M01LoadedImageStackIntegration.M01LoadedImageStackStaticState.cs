namespace OS.Boot;
/// <summary>
/// Caller-owned mutable storage that a future EFI adapter will place in
/// durable loaded-image storage before abandoning the firmware stack.
/// </summary>

public struct M01LoadedImageStackStaticState
{
    public const uint CurrentVersion = 1;
    public const ulong RequiredStorageAlignment = 8;
    public const ulong HandoffStorageOffset = 16;
    public const ulong SwitchStateStorageOffset = 104;
    public const ulong StackLayoutStorageOffset = 640;
    public const ulong RequiredStorageBytes = 768;
    internal uint HeaderSize;
    internal uint HeaderVersion;
    internal M01LoadedImageStackStatus CoordinatorStatus;
    internal M01LoadedImageStackCheckpoint CoordinatorCheckpoint;
    internal byte Reserved0;
    internal byte Reserved1;
    internal byte Reserved2;
    internal byte Reserved3;
    internal byte Reserved4;
    internal byte Reserved5;
    internal OwnedBootStackSwitchHandoff HandoffStorage;
    internal OwnedBootStackSwitchStateRecord SwitchStateStorage;
    internal OwnedBootStackLayout StackLayoutStorage;
    public uint Size => HeaderSize;
    public uint Version => HeaderVersion;
    public M01LoadedImageStackStatus Status => CoordinatorStatus;
    public M01LoadedImageStackCheckpoint Checkpoint => CoordinatorCheckpoint;
    public OwnedBootStackSwitchHandoff Handoff => HandoffStorage;
    public OwnedBootStackSwitchStateRecord SwitchState => SwitchStateStorage;
    public OwnedBootStackLayout StackLayout => StackLayoutStorage;
    public bool IsInitialized => HeaderSize == RequiredStorageBytes && HeaderVersion == CurrentVersion && CoordinatorStatus == M01LoadedImageStackStatus.Success && CoordinatorCheckpoint >= M01LoadedImageStackCheckpoint.SwitchArmed && CoordinatorCheckpoint <= M01LoadedImageStackCheckpoint.ContinuationRspContained && Reserved0 == 0 && Reserved1 == 0 && Reserved2 == 0 && Reserved3 == 0 && Reserved4 == 0 && Reserved5 == 0 && HandoffStorage.IsValid && SwitchStateStorage.IsValid && StackLayoutStorage.IsValid && (StackLayoutStorage.Lifetime == OwnedBootStackLifetime.Ready || StackLayoutStorage.Lifetime == OwnedBootStackLifetime.Active);
}
