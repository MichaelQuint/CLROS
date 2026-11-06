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

internal enum ImageBuilderCommand
{
    CreateRaw,
    PlanRaw,
    ConvertQcow2,
    ConvertAzureVhd,
    PlanAwsAmi,
    DescribeImage,
    BuildPackage,
    BuildNativePayload,
    BuildNativeAppPackage,
    BuildNativeAppImage,
    ValidatePackage,
    BuildLinuxElfPackage
}
