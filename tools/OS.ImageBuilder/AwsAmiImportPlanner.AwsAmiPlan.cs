using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Boot;
using OS.Kernel.Build;
using OS.Manifest;
using OS.Storage;

internal static partial class AwsAmiImportPlanner
{
    private readonly record struct AwsAmiPlan(string InputPath, string Format, string Architecture, string VirtualizationType, string RootDeviceType, string BootMode, bool EnaSupport, string Region, string BucketName, string VmImportRole, long InputBytes);
}
