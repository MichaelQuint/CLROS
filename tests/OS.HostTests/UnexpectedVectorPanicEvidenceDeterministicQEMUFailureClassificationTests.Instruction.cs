using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Diagnostics;
using Leaf = OS.Kernel.Diagnostics.UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification;

internal static unsafe partial class UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassificationTests
{
    private readonly record struct Instruction(int At, int End, string Op, int Register = 0, ulong Value = 0);
}
