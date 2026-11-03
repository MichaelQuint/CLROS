using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class HostFixtureAttribute(HostLane lane = HostLane.Fast) : Attribute
{
    public HostLane Lane { get; } = lane;
}
