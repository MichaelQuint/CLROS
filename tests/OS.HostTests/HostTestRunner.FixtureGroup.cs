using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

internal static partial class HostTestRunner
{
    internal sealed record FixtureGroup(string Id, HostLane Lane, Fixture[] Fixtures);
}
