using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Tooling.Native;

namespace OS.Tooling;

public sealed record EfiPreflightResult(string Root, string Configuration, string Runtime, EfiProfile Profile, string DotnetHostPath, string DotnetSdkVersion, string DotnetSdkDirectory, string LinkerPath, string LinkerVersion, string WindowsSdkLibraryPath, string WindowsSdkVersion, string IlCompilerDiscoverySourcePath, string IlCompilerPackageVersion, string IlCompilerFileVersion, string NativeAotRuntimePackPath, string NativeAotRuntimePackVersion);
