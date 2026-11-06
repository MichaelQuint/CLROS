using System.Text;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// A one-shot bounded diagnostic, never run by ordinary HTTP requests. Eight
// owned threads run in two groups of four so runtime threads retain capacity;
// each group rendezvous before writing and joins before the next group starts.
// The Linux-only native call observes the real writev result and errno rather
// than translating a managed stream error into an assumed descriptor result.
internal static partial class LinuxLoggingExercise
{
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct Vector
    {
        internal byte* Address;
        internal nuint Length;
    }
}
