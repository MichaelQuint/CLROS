using OS.Tooling;
using OS.Tooling.Native;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal static partial class BuildEfiShellPreflightTests
{
    private sealed class ToolFixture
    {
        public required string Root { get; init; }
        public required string MsvcLinkerSearchRoot { get; set; }
        public required string LinkerPath { get; init; }
        public required string WindowsSdkLibSearchRoot { get; set; }
        public required string SdkLibPath { get; init; }
        public required string IlCompilerSearchRoot { get; init; }
        public required string IlCompilerPath { get; set; }
        public required string NativeAotRuntimePackSearchRoot { get; init; }
        public required string NativeAotRuntimePackPath { get; set; }
        public required string QemuPath { get; set; }
        public required string QemuImgPath { get; set; }
        public required string OvmfSearchRoot { get; init; }
        public required string OvmfCodePath { get; set; }
        public string? PowerShellModulePath { get; set; }
    }
}
