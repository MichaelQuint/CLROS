using System.Collections;
using System.Globalization;
using OS.Tooling;
using Reads = OS.Tooling.NativeDiskReadObservations;

internal static partial class NativeDiskReadObservationTests
{
    private sealed record Fixture(byte[] Image, List<string> Rows, Reads.Boundary Boundary);
}
