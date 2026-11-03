using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Manifest;

namespace OS.Tooling;

public sealed partial class NativeAotWebProgram
{
    internal readonly record struct Configuration(string Body, string Json, uint Arguments, uint Environment, uint SettingsBytes, bool LogQuota);
}
