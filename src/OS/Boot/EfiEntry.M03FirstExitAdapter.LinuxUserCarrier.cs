#if M16_027_PROBE
namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // The selected native owner retains this separate whole carrier for the
        // entire boot, including failed cleanup. Its pointees still need their
        // original mapping, callback, device and process authority checks.
        private struct LinuxUserCarrier
        {
            internal ulong Self;
            internal Storage* Boot;
            internal ulong OriginalCooperativePool;
        }

    }
}
#endif
