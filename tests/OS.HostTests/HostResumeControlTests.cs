// An execution control for checkpoint lifecycle tests. Its body has no opaque
// referenced implementation or external input, so unchanged evidence is reusable.
internal static class HostResumeControlTests
{
    [HostTest("runner pure control completes without external inputs")]
    private static bool Complete() => true;
}
