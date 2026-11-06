using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

internal static partial class QemuRunnerTests
{
    private enum M01RawObservationCorruption
    {
        None,
        PodHashMismatch,
        SerialHashMismatch,
        RuntimeAddressMisaligned,
        RuntimeAddressOutOfRange,
        CallGateAddressMismatch,
        SemanticRuntimeCr0Mismatch
    }
}
