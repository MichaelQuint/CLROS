using System.Globalization;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class NativeAotSocketMessages
{
    /// <summary>One immutable observed input joined to a current call ordinal.
    /// Record indices refer to the caller's complete original row array.</summary>

    public sealed record Message(int RecordIndex, int CallRecordIndex, ulong Identity, ulong Ordinal, ulong Number, uint Descriptor, uint Generation, ulong Header, ulong Flags, ulong Table, uint Total, IReadOnlyList<Vector> Vectors);
}
