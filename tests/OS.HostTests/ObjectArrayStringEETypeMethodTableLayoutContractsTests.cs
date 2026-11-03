using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using OS.Kernel.Runtime;
using Layout = OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts;

internal static class ObjectArrayStringEETypeMethodTableLayoutContractsTests
{
    private const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian;

    [HostFixture(HostLane.Native)]
    internal static void Run()
    {
        HostCheck.Run("m04.005", () =>
        {
            CheckIdentityAndConstants();
            CheckIndependentBytes();
            CheckSizeMatrix();
            CheckRejectionsAndFlags();
            CheckPodAndAllocation();
            CheckNoStdLibCompilation();
            Console.WriteLine("CLROS_M04_005_READY profile=ilc" + OS.Tooling.EfiContracts.NativePackageVersion + "-win-x64-le shapes=3 prefixBytes=24"
                + " referenceFilesRequired=false hostConformance=true guestIntegrated=false gate4=Fail");
        });
    }

    private static void CheckIdentityAndConstants()
    {
        Require(Layout.ILCompilerVersion == OS.Tooling.EfiContracts.NativePackageVersion &&
            Layout.RuntimeSourceCommit == "5254d8bcf59917edc1e1abc33bdf2442b77b1c70", "pinned source identity");
        XDocument project = XDocument.Load(Path.Combine(FindRoot(), "src", "OS", "OS.csproj"));
        foreach (string package in new[] { "Microsoft.DotNet.ILCompiler", "runtime.win-x64.Microsoft.DotNet.ILCompiler" })
        {
            XElement reference = project.Descendants("PackageReference")
                .Single(e => (string?)e.Attribute("Include") == package);
            Require((string?)reference.Attribute("Version") == Layout.ILCompilerVersion, "selected compiler " + package);
        }
        Require(project.Descendants("RuntimeIdentifier").Single().Value == "win-x64", "selected target");
        Require(project.Descendants("ClrosIlcHostPackagePath").Single().Value ==
                "$(UserProfile)\\.nuget\\packages\\runtime.win-x64.microsoft.dotnet.ilcompiler\\" + OS.Tooling.EfiContracts.NativePackageVersion &&
            project.Descendants("ClrosRuntimePackagePath").Single().Value ==
                "$(UserProfile)\\.nuget\\packages\\microsoft.netcore.app.runtime.nativeaot.win-x64\\" + OS.Tooling.EfiContracts.NativePackageVersion,
            "default compiler and runtime package paths");
        Require(project.Descendants("ResolvedILCompilerPack").Single().Element("PackageDirectory")?.Value ==
                "$(ClrosIlcHostPackagePath)" &&
            project.Descendants("ResolvedTargetILCompilerPack").Single().Element("PackageDirectory")?.Value ==
                "$(ClrosRuntimePackagePath)", "resolved pack path bindings");

        Require(Layout.PointerBytes == 8 && Layout.AllocationAlignmentBytes == 8 &&
            Layout.ObjectHeaderBytes == 8 && Layout.ObjectHeaderOffsetFromReference == -8 &&
            Layout.MethodTableTokenOffsetFromReference == 0 && Layout.FixedDataOffsetFromReference == 8 &&
            Layout.ComponentLengthOffsetFromReference == 8 && Layout.ArrayDataOffsetFromReference == 16 &&
            Layout.StringDataOffsetFromReference == 12, "object-reference coordinates");
        Require(Layout.MinimumFixedSizeBytes == 24 && Layout.ArrayBaseSizeBytes == 24 &&
            Layout.StringBaseSizeBytes == 22 && Layout.StringComponentBytes == 2 &&
            Layout.MaximumArrayLength == 2147483591 && Layout.MaximumStringLength == 1073741791,
            "allocation-origin sizes and bounds");
        Require(Layout.MethodTablePrefixBytes == 24 && Layout.FlagsOffset == 0 &&
            Layout.BaseSizeOffset == 4 && Layout.RelatedTypeTokenOffset == 8 &&
            Layout.VTableSlotCountOffset == 16 && Layout.InterfaceCountOffset == 18 && Layout.TypeHashOffset == 20,
            "method-table coordinates");
        Require(Layout.HasComponentSizeFlag == 0x80000000 && Layout.HasPointersFlag == 0x01000000 &&
            Layout.ReservedCollectibleFlag == 0x00200000 && Layout.KindMask == 0x00030000 &&
            Layout.CanonicalKind == 0 && Layout.ParameterizedKind == 0x00020000 &&
            Layout.FunctionPointerKind == 0x00010000 && Layout.GenericDefinitionKind == 0x00030000 &&
            Layout.ElementTypeMask == 0x7C000000 && Layout.ElementTypeShift == 26 &&
            Layout.ClassElementType == 0x14 && Layout.SzArrayElementType == 0x18 && Layout.MdArrayElementType == 0x17,
            "selected flags, not the old reference-project ABI");
    }

    private static NativeAotMethodTablePrefix Prefix(uint flags, uint baseSize) =>
        new(flags, baseSize, 0x1122334455667788, 0x1234, 0x5678, 0x89ABCDEF);

    private static NativeAotLayoutResult Evaluate(uint flags, uint baseSize, int count, ulong capacity = ulong.MaxValue) =>
        Layout.Evaluate(Profile, Prefix(flags, baseSize), count, capacity);

    private static void CheckIndependentBytes()
    {
        // Literal primary-source-derived bytes. These are observations, never live objects.
        byte[] prefixBytes = [0x00, 0x00, 0x00, 0x50, 0x18, 0x00, 0x00, 0x00,
            0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11,
            0x34, 0x12, 0x78, 0x56, 0xEF, 0xCD, 0xAB, 0x89];
        NativeAotMethodTablePrefix prefix = DecodePrefix(prefixBytes);
        Require(prefix.Flags == 0x50000000 && prefix.BaseSize == 24 &&
            prefix.RelatedTypeToken == 0x1122334455667788 && prefix.VTableSlotCount == 0x1234 &&
            prefix.InterfaceCount == 0x5678 && prefix.TypeHash == 0x89ABCDEF, "independent prefix scalars");
        CheckResult(Layout.Evaluate(Profile, prefix, 0, 24), NativeAotLayoutShape.FixedObject, 0, false, -1, 8, 24, 24);

        byte[][] flagBytes = [[0x08, 0x00, 0x00, 0x50], [0x00, 0x00, 0x00, 0x51],
            [0x03, 0x00, 0x02, 0xE0], [0x08, 0x00, 0x02, 0xE1], [0x02, 0x00, 0x00, 0xD0],
            [0x00, 0x00, 0x20, 0x50], [0x00, 0x00, 0x01, 0x50]];
        uint[] expectedFlags = [0x50000008, 0x51000000, 0xE0020003, 0xE1020008, 0xD0000002, 0x50200000, 0x50010000];
        NativeAotLayoutStatus[] expectedStatuses = [NativeAotLayoutStatus.Success, NativeAotLayoutStatus.Success,
            NativeAotLayoutStatus.Success, NativeAotLayoutStatus.Success, NativeAotLayoutStatus.Success,
            NativeAotLayoutStatus.InvalidFlags, NativeAotLayoutStatus.UnsupportedShape];
        for (int i = 0; i < flagBytes.Length; i++)
        {
            flagBytes[i].CopyTo(prefixBytes, 0);
            prefixBytes[4] = i == 4 ? (byte)0x16 : (byte)0x18;
            prefix = DecodePrefix(prefixBytes);
            Require(prefix.Flags == expectedFlags[i] && prefix.BaseSize == (i == 4 ? 22U : 24U), "literal flag variant");
            NativeAotLayoutResult result = Layout.Evaluate(Profile, prefix, 0, 24);
            Require(result.Status == expectedStatuses[i], "literal flag status");
            if (i == 0)
                CheckResult(result, NativeAotLayoutShape.FixedObject, 0, false, -1, 8, 24, 24);
            if (i == 1)
                CheckResult(result, NativeAotLayoutShape.FixedObject, 0, true, -1, 8, 24, 24);
            if (i == 2)
                CheckResult(result, NativeAotLayoutShape.SzArray, 3, false, 8, 16, 24, 24);
            if (i == 3)
                CheckResult(result, NativeAotLayoutShape.SzArray, 8, true, 8, 16, 24, 24);
            if (i == 4)
                CheckResult(result, NativeAotLayoutShape.String, 2, false, 8, 12, 22, 24);
            if (i >= 5)
                CheckFailure(result, expectedStatuses[i]);
        }

        byte[] text = [0, 0, 0, 0, 0, 0, 0, 0, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11,
            2, 0, 0, 0, 0x41, 0, 0xA9, 3, 0, 0, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC];
        const int referenceOrigin = 8;
        Require(referenceOrigin + Layout.ObjectHeaderOffsetFromReference == 0 &&
            BinaryPrimitives.ReadUInt64LittleEndian(text.AsSpan(referenceOrigin + Layout.MethodTableTokenOffsetFromReference)) ==
                0x1122334455667788 &&
            BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(referenceOrigin + Layout.ComponentLengthOffsetFromReference)) == 2 &&
            BinaryPrimitives.ReadUInt16LittleEndian(text.AsSpan(referenceOrigin + Layout.StringDataOffsetFromReference)) == 0x41 &&
            BinaryPrimitives.ReadUInt16LittleEndian(text.AsSpan(22)) == 0x03A9 && text[24] == 0 && text[25] == 0 &&
            text.AsSpan(26).IndexOfAnyExcept((byte)0xCC) == -1, "literal string header, chars, terminator and rounding");
        CheckResult(Evaluate(0xD0000002, 22, 2, (ulong)text.Length), NativeAotLayoutShape.String, 2, false, 8, 12, 26, 32);

        byte[] array = [0, 0, 0, 0, 0, 0, 0, 0, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11,
            3, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC];
        Require(BinaryPrimitives.ReadInt32LittleEndian(array.AsSpan(referenceOrigin + Layout.ComponentLengthOffsetFromReference)) == 3 &&
            array.AsSpan(20, 4).IndexOfAnyExcept((byte)0) == -1 &&
            array.AsSpan(referenceOrigin + Layout.ArrayDataOffsetFromReference, 9).SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }) &&
            array.AsSpan(33).IndexOfAnyExcept((byte)0xCC) == -1, "literal array padding, data and rounding");
        CheckResult(Evaluate(0xE0020003, 24, 3, (ulong)array.Length), NativeAotLayoutShape.SzArray, 3, false, 8, 16, 33, 40);
    }

    // Host-only decoding tests the documented offsets; production has no parser.
    private static NativeAotMethodTablePrefix DecodePrefix(ReadOnlySpan<byte> bytes) => new(
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[Layout.FlagsOffset..]),
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[Layout.BaseSizeOffset..]),
        BinaryPrimitives.ReadUInt64LittleEndian(bytes[Layout.RelatedTypeTokenOffset..]),
        BinaryPrimitives.ReadUInt16LittleEndian(bytes[Layout.VTableSlotCountOffset..]),
        BinaryPrimitives.ReadUInt16LittleEndian(bytes[Layout.InterfaceCountOffset..]),
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[Layout.TypeHashOffset..]));

    private static void CheckSizeMatrix()
    {
        // Literal expectations include the header once and the string terminator once.
        (uint Flags, uint Base, int Count, NativeAotLayoutShape Shape, ushort Component, ulong Raw, ulong Aligned)[] cases =
        [
            (0x50000000, 24, 0, NativeAotLayoutShape.FixedObject, 0, 24, 24),
            (0x50000000, 32, 0, NativeAotLayoutShape.FixedObject, 0, 32, 32),
            (0xE0020001, 24, 0, NativeAotLayoutShape.SzArray, 1, 24, 24),
            (0xE0020001, 24, 1, NativeAotLayoutShape.SzArray, 1, 25, 32),
            (0xE0020001, 24, 8, NativeAotLayoutShape.SzArray, 1, 32, 32),
            (0xE0020003, 24, 3, NativeAotLayoutShape.SzArray, 3, 33, 40),
            (0xE0020008, 24, 1, NativeAotLayoutShape.SzArray, 8, 32, 32),
            (0xD0000002, 22, 0, NativeAotLayoutShape.String, 2, 22, 24),
            (0xD0000002, 22, 1, NativeAotLayoutShape.String, 2, 24, 24),
            (0xD0000002, 22, 2, NativeAotLayoutShape.String, 2, 26, 32),
            (0xE0020001, 24, 2147483591, NativeAotLayoutShape.SzArray, 1, 2147483615, 2147483616),
            (0xE002FFFF, 24, 2147483591, NativeAotLayoutShape.SzArray, 65535, 140735337136209, 140735337136216),
            (0xD0000002, 22, 1073741791, NativeAotLayoutShape.String, 2, 2147483604, 2147483608)
        ];
        foreach (var c in cases)
        {
            int lengthOffset = c.Shape == NativeAotLayoutShape.FixedObject ? -1 : 8;
            int dataOffset = c.Shape == NativeAotLayoutShape.FixedObject ? 8 : c.Shape == NativeAotLayoutShape.String ? 12 : 16;
            CheckResult(Evaluate(c.Flags, c.Base, c.Count, c.Aligned), c.Shape, c.Component, false,
                lengthOffset, dataOffset, c.Raw, c.Aligned);
            CheckFailure(Evaluate(c.Flags, c.Base, c.Count, c.Aligned - 1), NativeAotLayoutStatus.CapacityTooSmall);
            CheckFailure(Evaluate(c.Flags, c.Base, c.Count, 0), NativeAotLayoutStatus.CapacityTooSmall);
        }
        CheckResult(Evaluate(0x50000000, 4294967288, 0), NativeAotLayoutShape.FixedObject, 0, false, -1, 8,
            4294967288, 4294967288); // No invented fixed-class cap or signed truncation.
        CheckResult(Evaluate(0xE1020008, 24, 2), NativeAotLayoutShape.SzArray, 8, true, 8, 16, 40, 40);
    }

    private static void CheckRejectionsAndFlags()
    {
        CheckFailure(default, NativeAotLayoutStatus.Uninitialized);
        foreach (NativeAotLayoutProfile profile in new[] { (NativeAotLayoutProfile)0, NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian, (NativeAotLayoutProfile)255 })
            CheckFailure(Layout.Evaluate(profile, Prefix(0x50200000, 0), -1, 0), NativeAotLayoutStatus.UnsupportedProfile);
        CheckFailure(Layout.Evaluate(Profile, default, 0, 0), NativeAotLayoutStatus.UnsupportedShape);
        foreach (uint size in new uint[] { 0, 8, 16, 23, 25, uint.MaxValue })
            CheckFailure(Evaluate(0x50000000, size, 0), NativeAotLayoutStatus.InvalidBaseSize);
        foreach (int count in new[] { int.MinValue, -1, 1, int.MaxValue })
            CheckFailure(Evaluate(0x50000000, 24, count), NativeAotLayoutStatus.InvalidLength);
        foreach (uint size in new uint[] { 0, 16, 22, 32 })
            CheckFailure(Evaluate(0xE0020001, size, 0), NativeAotLayoutStatus.InvalidBaseSize);
        foreach (uint size in new uint[] { 0, 24 })
            CheckFailure(Evaluate(0xD0000002, size, 0), NativeAotLayoutStatus.InvalidBaseSize);
        foreach (uint component in new uint[] { 0, 1, 3, 65535 })
            CheckFailure(Evaluate(0xD0000000 | component, 22, 0), NativeAotLayoutStatus.InvalidComponentSize);
        CheckFailure(Evaluate(0xE0020000, 24, 0), NativeAotLayoutStatus.InvalidComponentSize);
        foreach (int count in new[] { int.MinValue, -1, 2147483592, int.MaxValue })
            CheckFailure(Evaluate(0xE0020001, 24, count), NativeAotLayoutStatus.InvalidLength);
        foreach (int count in new[] { int.MinValue, -1, 1073741792, int.MaxValue })
            CheckFailure(Evaluate(0xD0000002, 22, count), NativeAotLayoutStatus.InvalidLength);

        // Valid but unsupported interface, box, MD array, pointer/byref, function pointer,
        // generic definition and mismatched kind/element pairs do not become objects.
        foreach (uint flags in new uint[] { 0x54000000, 0x40000000, 0x20000000, 0xDC020001,
            0x68020000, 0x64020000, 0x6C010000, 0x50010000, 0x50030000, 0x60000000, 0x50020000 })
            CheckFailure(Evaluate(flags, 24, 0), NativeAotLayoutStatus.UnsupportedShape);
        CheckFailure(Evaluate(0x60020001, 24, 0), NativeAotLayoutStatus.InvalidFlags);
        CheckFailure(Evaluate(0xD1000002, 22, 0), NativeAotLayoutStatus.InvalidFlags);
        foreach (uint flags in new uint[] { 0x50200000, 0xE0220001, 0xD0200002 })
            CheckFailure(Evaluate(flags, 24, 0), NativeAotLayoutStatus.InvalidFlags);

        foreach (uint feature in new uint[] { 0x00080000, 0x00100000, 0x00800000, 0x02000000 })
        {
            CheckFailure(Evaluate(0x50000000 | feature, 24, 0), NativeAotLayoutStatus.UnsupportedTypeFeatures);
            CheckFailure(Evaluate(0xE0020001 | feature, 24, 0), NativeAotLayoutStatus.UnsupportedTypeFeatures);
            CheckFailure(Evaluate(0xD0000002 | feature, 22, 0), NativeAotLayoutStatus.UnsupportedTypeFeatures);
        }
        foreach (uint allowed in new uint[] { 0x00040000, 0x00400000, 0x00440008 })
            CheckResult(Evaluate(0x50000000 | allowed, 24, 0), NativeAotLayoutShape.FixedObject, 0, false, -1, 8, 24, 24);

        // Every low-ushort value distinguishes fixed extended flags from array components.
        for (uint low = 0; low <= 65535; low++)
        {
            NativeAotLayoutResult fixedResult = Evaluate(0x50000000 | low, 24, 0);
            if (low is 0 or 8)
                CheckResult(fixedResult, NativeAotLayoutShape.FixedObject, 0, false, -1, 8, 24, 24);
            else
                CheckFailure(fixedResult, (low & 0xE800) != 0 ? NativeAotLayoutStatus.InvalidFlags : NativeAotLayoutStatus.UnsupportedTypeFeatures);
            NativeAotLayoutResult arrayResult = Evaluate(0xE0020000 | low, 24, 0);
            if (low == 0)
                CheckFailure(arrayResult, NativeAotLayoutStatus.InvalidComponentSize);
            else
                CheckResult(arrayResult, NativeAotLayoutShape.SzArray, (ushort)low, false, 8, 16, 24, 24);
        }

        // Precedence uses single-cause coverage above, then these multi-error cases.
        CheckFailure(Evaluate(0x50210000, 0, -1, 0), NativeAotLayoutStatus.InvalidFlags);
        CheckFailure(Evaluate(0x50010800, 0, -1, 0), NativeAotLayoutStatus.UnsupportedShape);
        CheckFailure(Evaluate(0x50080800, 0, -1, 0), NativeAotLayoutStatus.InvalidFlags);
        CheckFailure(Evaluate(0x50080000, 0, -1, 0), NativeAotLayoutStatus.UnsupportedTypeFeatures);
        CheckFailure(Evaluate(0xD0000001, 24, -1, 0), NativeAotLayoutStatus.InvalidBaseSize);
        CheckFailure(Evaluate(0xD0000001, 22, -1, 0), NativeAotLayoutStatus.InvalidComponentSize);
        CheckFailure(Evaluate(0xD0000002, 22, -1, 0), NativeAotLayoutStatus.InvalidLength);

        // Opaque metadata cannot imply a usable address or optional table extent.
        foreach (ulong token in new ulong[] { 0, 1, ulong.MaxValue })
            CheckResult(Layout.Evaluate(Profile, new NativeAotMethodTablePrefix(0x50000008, 24, token,
                ushort.MaxValue, ushort.MaxValue, uint.MaxValue), 0, 24),
                NativeAotLayoutShape.FixedObject, 0, false, -1, 8, 24, 24);
    }

    private static void CheckPodAndAllocation()
    {
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotMethodTablePrefix>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotLayoutResult>(), "reference-free PODs");
        bool passed = true;
        for (int i = 0; i < 10000; i++)
            passed &= AllocationIteration(i);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            passed &= AllocationIteration(i);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(passed && allocated == 0, "zero allocated bytes across 40000 warmed evaluations");
    }

    private static bool AllocationIteration(int count) =>
        Evaluate(0x50000008, 24, 0).Succeeded && Evaluate(0xE002FFFF, 24, count).Succeeded &&
        Evaluate(0xD0000002, 22, count).Succeeded &&
        Evaluate(0x50200000, 24, count).Status == NativeAotLayoutStatus.InvalidFlags;

    private static void CheckResult(NativeAotLayoutResult result, NativeAotLayoutShape shape,
        ushort component, bool pointers, int lengthOffset, int dataOffset, ulong raw, ulong aligned)
    {
        Require(result.Succeeded && result.Status == NativeAotLayoutStatus.Success && result.Shape == shape &&
            result.ComponentSize == component && result.ContainsGcPointers == pointers &&
            result.LengthOffsetFromReference == lengthOffset && result.DataOffsetFromReference == dataOffset &&
            result.RawSizeBytes == raw && result.AlignedSizeBytes == aligned, "exact successful layout result");
    }

    private static void CheckFailure(NativeAotLayoutResult result, NativeAotLayoutStatus status)
    {
        Require(!result.Succeeded && result.Status == status && result.Shape == NativeAotLayoutShape.Uninitialized &&
            result.ComponentSize == 0 && !result.ContainsGcPointers && result.LengthOffsetFromReference == 0 &&
            result.DataOffsetFromReference == 0 && result.RawSizeBytes == 0 && result.AlignedSizeBytes == 0,
            "failure status and empty payload: expected " + status + ", actual " + result.Status);
    }

    private static void CheckNoStdLibCompilation()
    {
        string root = FindRoot();
        string directory = Path.Combine(root, "artifacts", "m04-005", "nostdlib", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(root, "global.json"), Path.Combine(directory, "global.json"));
        File.WriteAllText(Path.Combine(directory, "Shim.cs"), """
            namespace System
            {
                public class Object { }
                public abstract class ValueType : Object { }
                public abstract class Enum : ValueType { }
                public abstract class Delegate : Object { }
                public abstract class MulticastDelegate : Delegate { }
                public class String { }
                public class Attribute : Object { }
                public struct Void { }
                public struct Boolean { }
                public struct Byte { }
                public struct Int16 { }
                public struct UInt16 { }
                public struct Int32 { }
                public struct UInt32 { }
                public struct Int64 { }
                public struct UInt64 { }
                public struct IntPtr { }
                public struct UIntPtr { }
                public enum AttributeTargets { }
                public sealed class AttributeUsageAttribute : Attribute
                {
                    public AttributeUsageAttribute(AttributeTargets validOn) { }
                    public bool AllowMultiple { get; set; }
                    public bool Inherited { get; set; }
                }
            }
            namespace System.Runtime.CompilerServices
            {
                public sealed class IsReadOnlyAttribute : System.Attribute { }
                public sealed class CompilerGeneratedAttribute : System.Attribute { }
            }
            """, new UTF8Encoding(false));
        string production = System.Security.SecurityElement.Escape(Path.Combine(root, "src", "OS.Kernel", "Runtime",
            "ObjectArrayStringEETypeMethodTableLayoutContracts.cs"))!;
        string methodImpl = System.Security.SecurityElement.Escape(Path.Combine(root, "src", "OS", "Runtime",
            "MethodImplAttribute.cs"))!;
        File.WriteAllText(Path.Combine(directory, "Compile.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework>
                <LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType>
                <AssemblyName>CLROS.M04.Layout.NoStdLib</AssemblyName>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <NoStdLib>true</NoStdLib>
                <DefineConstants>NOSTDLIB_EFI</DefineConstants>
                <NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles>
                <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
                <EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <DebugType>none</DebugType>
                <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Shim.cs" />
                <Compile Include="{{methodImpl}}" Link="MethodImplAttribute.cs" />
                <Compile Include="{{production}}" Link="Layout.cs" />
              </ItemGroup>
              <Target Name="RemoveFrameworkReferences" BeforeTargets="BeforeCompile"
                      AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup>
                  <ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" />
                </ItemGroup>
              </Target>
            </Project>
            """, new UTF8Encoding(false));
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in new[] { "build", "Compile.csproj", "--nologo", "-m:1", "-c", "Release" })
            start.ArgumentList.Add(arg);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("compile start");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(directory, "build.stdout.log"), stdout.Result);
        File.WriteAllText(Path.Combine(directory, "build.stderr.log"), stderr.Result);
        Require(exited && process.ExitCode == 0, "no-stdlib compile; logs=" + directory + " " + stdout.Result + stderr.Result);
        using var stream = File.OpenRead(Path.Combine(directory, "bin", "Release", "net11.0", "CLROS.M04.Layout.NoStdLib.dll"));
        using PEReader image = new(stream);
        MetadataReader metadata = image.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count == 0, "zero assembly references");
        MethodDefinition evaluator = metadata.MethodDefinitions.Select(metadata.GetMethodDefinition)
            .Single(m => metadata.GetString(m.Name) == "Evaluate");
        Require((evaluator.ImplAttributes & System.Reflection.MethodImplAttributes.NoInlining) != 0,
            "actual no-stdlib evaluator preserves call identity");
    }

    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx")))
                return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("CLROS root not found");
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
