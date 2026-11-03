using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class HostTestAttribute(string name, HostLane lane = HostLane.Fast, int order = 0) : Attribute
{
    public string Name { get; } = name;
    public HostLane Lane { get; } = lane;
    public int Order { get; } = order;
}
