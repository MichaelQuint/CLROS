using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

internal static partial class HostTestRunner
{
    internal sealed record Fixture(string Id, HostLane Lane, MethodInfo Method, string? Name = null, int Order = 0);
}
