namespace OS.Drivers;

public struct VirtioNetHeaderChecksumPolicyResult
{
    public VirtioNetHeaderChecksumPolicyStatus Status;
    public VirtioNetReceivedFrameParseResult Frame;
    public readonly bool Succeeded => Status == VirtioNetHeaderChecksumPolicyStatus.Success && Frame.Succeeded;
}
