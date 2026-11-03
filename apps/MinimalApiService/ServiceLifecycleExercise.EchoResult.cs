using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal sealed partial class ServiceLifecycleExercise
{
    internal readonly record struct EchoResult(bool Accepted, int Bytes, uint Hash)
    {
        internal string Text => Accepted ? string.Create(CultureInfo.InvariantCulture, $"CLROS_LIFECYCLE_ECHO_OK bytes={Bytes} hash={Hash:x8}") : "CLROS_LIFECYCLE_BAD_BODY";
    }
}
