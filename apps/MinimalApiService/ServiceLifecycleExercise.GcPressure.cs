using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal sealed partial class ServiceLifecycleExercise
{
    private readonly record struct GcPressure(long Bytes, long Heap, long Committed, int Collections, bool Caught, bool DataIntact);
}
