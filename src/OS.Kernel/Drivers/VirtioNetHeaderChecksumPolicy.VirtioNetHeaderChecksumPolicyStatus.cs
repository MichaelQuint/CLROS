namespace OS.Drivers;

public enum VirtioNetHeaderChecksumPolicyStatus : byte
{
    Uninitialized,
    Success,
    UnsupportedFeatures,
    HeaderRejected
}
