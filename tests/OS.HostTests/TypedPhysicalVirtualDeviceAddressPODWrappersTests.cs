using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;

internal static class TypedPhysicalVirtualDeviceAddressPODWrappersTests
{
    private const string IssueTestName = "m01.001";

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run("m01.001 wrappers are stable pods", TestStablePodLayouts, ref failures);
        Run("m01.001 wrappers reject default and zero", TestDefaultAndZero, ref failures);
        Run("m01.001 named boundaries preserve raw values", TestNamedBoundaryRawValuePreservation, ref failures);
        Run("m01.001 default raw extraction fails closed", TestDefaultRawExtraction, ref failures);
        Run("m01.001 equality is domain local", TestDomainLocalEquality, ref failures);
        Run("m01.001 ordering is domain local", TestDomainLocalOrdering, ref failures);
        Run("m01.001 wrappers expose only named raw boundaries", TestNamedBoundarySurface, ref failures);
        Run("m01.001 source excludes managed runtime dependencies", TestNoRuntimeSourceSurface, ref failures);
        Run("m01.001 exact source compiles under nostdlib efi", TestNoStdLibEfiCompileClosure, ref failures);
        Run("m01.001 prohibited expressions fail compilation", TestProhibitedExpressionsFailCompilation, ref failures);
        Run("m01.001 offsets accept nonoverflowing boundaries", TestOffsetSuccess, ref failures);
        Run("m01.001 offsets reject wrap", TestOffsetFailures, ref failures);
        Run("m01.001 end-exclusive arithmetic succeeds", TestEndExclusiveSuccess, ref failures);
        Run("m01.001 end-exclusive arithmetic rejects invalid ranges", TestEndExclusiveFailures, ref failures);
        Run("m01.001 alignment validates inputs", TestAlignmentValidation, ref failures);
        Run("m01.001 align-up succeeds", TestAlignUpSuccess, ref failures);
        Run("m01.001 align-up rejects wrap", TestAlignUpFailures, ref failures);
        Run("m01.001 failures leave deterministic default outputs", TestFailureOutputs, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-001 typed address wrapper host tests failed: " + failures);
        }

        Console.WriteLine("CLROS_M01_001_READY");
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL "
                + name
                + " "
                + ex.GetType().Name
                + ": "
                + ex.Message);
        }
    }

    private static bool TestStablePodLayouts()
    {
        return IsStablePod<PhysicalAddress>()
            && IsStablePod<VirtualAddress>()
            && IsStablePod<DeviceAddress>();
    }

    private static bool IsStablePod<T>()
        where T : struct
    {
        Type type = typeof(T);
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return type.IsValueType
            && type.IsLayoutSequential
            && type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false)
            && fields.Length == 1
            && fields[0].IsPrivate
            && fields[0].FieldType == typeof(ulong)
            && fields[0].IsInitOnly
            && Marshal.OffsetOf<T>(fields[0].Name).ToInt64() == 0
            && type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Length == 0
            && type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length == 1
            && type.GetProperty("IsValid", BindingFlags.Instance | BindingFlags.Public) is not null
            && Marshal.SizeOf<T>() == sizeof(ulong)
            && Unsafe.SizeOf<T>() == sizeof(ulong)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    }

    private static bool TestDefaultAndZero()
    {
        AddressValueStatus physicalStatus = TryCreatePhysical(
            0,
            out PhysicalAddress physical);
        AddressValueStatus virtualStatus = TryCreateVirtual(
            0,
            out VirtualAddress virtualAddress);
        AddressValueStatus deviceStatus = TryCreateDevice(
            0,
            out DeviceAddress device);

        return AddressValueStatus.Uninitialized != AddressValueStatus.Success
            && !default(PhysicalAddress).IsValid
            && !default(VirtualAddress).IsValid
            && !default(DeviceAddress).IsValid
            && physicalStatus == AddressValueStatus.ZeroAddress
            && virtualStatus == AddressValueStatus.ZeroAddress
            && deviceStatus == AddressValueStatus.ZeroAddress
            && physical.Equals(default(PhysicalAddress))
            && virtualAddress.Equals(default(VirtualAddress))
            && device.Equals(default(DeviceAddress));
    }

    private static bool TestNamedBoundaryRawValuePreservation()
    {
        ulong[] values = [1, 0x1000, 0xFEDCBA9876543210, ulong.MaxValue];

        foreach (ulong value in values)
        {
            if (PhysicalAddress.TryCreateFromUefiBoundary(value, out PhysicalAddress physical)
                    != AddressValueStatus.Success
                || VirtualAddress.TryCreateFromUefiBoundary(value, out VirtualAddress virtualAddress)
                    != AddressValueStatus.Success
                || DeviceAddress.TryCreateFromHardwareRegisterBoundary(value, out DeviceAddress device)
                    != AddressValueStatus.Success
                || physical.TryGetRawValueForUefiBoundary(out ulong physicalValue)
                    != AddressValueStatus.Success
                || virtualAddress.TryGetRawValueForUefiBoundary(out ulong virtualValue)
                    != AddressValueStatus.Success
                || device.TryGetRawValueForHardwareRegisterBoundary(out ulong deviceValue)
                    != AddressValueStatus.Success
                || physicalValue != value
                || virtualValue != value
                || deviceValue != value
                || !physical.IsValid
                || !virtualAddress.IsValid
                || !device.IsValid)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestDefaultRawExtraction()
    {
        AddressValueStatus physicalStatus =
            default(PhysicalAddress).TryGetRawValueForUefiBoundary(out ulong physicalValue);
        AddressValueStatus virtualStatus =
            default(VirtualAddress).TryGetRawValueForHardwareRegisterBoundary(out ulong virtualValue);
        AddressValueStatus deviceStatus =
            default(DeviceAddress).TryGetRawValueForHardwareRegisterBoundary(out ulong deviceValue);

        return physicalStatus == AddressValueStatus.InvalidAddress
            && virtualStatus == AddressValueStatus.InvalidAddress
            && deviceStatus == AddressValueStatus.InvalidAddress
            && physicalValue == 0
            && virtualValue == 0
            && deviceValue == 0;
    }

    private static bool TestDomainLocalEquality()
    {
        TryCreatePhysical(0x2000, out PhysicalAddress physical);
        TryCreatePhysical(0x2000, out PhysicalAddress samePhysical);
        TryCreatePhysical(0x3000, out PhysicalAddress otherPhysical);
        TryCreateVirtual(0x2000, out VirtualAddress virtualAddress);
        TryCreateVirtual(0x2000, out VirtualAddress sameVirtual);
        TryCreateVirtual(0x3000, out VirtualAddress otherVirtual);
        TryCreateDevice(0x2000, out DeviceAddress device);
        TryCreateDevice(0x2000, out DeviceAddress sameDevice);
        TryCreateDevice(0x3000, out DeviceAddress otherDevice);

        object boxedPhysical = physical;
        object boxedVirtual = virtualAddress;
        object boxedDevice = device;

        return physical.Equals(samePhysical)
            && !physical.Equals(otherPhysical)
            && virtualAddress.Equals(sameVirtual)
            && !virtualAddress.Equals(otherVirtual)
            && device.Equals(sameDevice)
            && !device.Equals(otherDevice)
            && !boxedPhysical.Equals(boxedVirtual)
            && !boxedPhysical.Equals(boxedDevice)
            && !boxedVirtual.Equals(boxedDevice)
            && typeof(PhysicalAddress) != typeof(VirtualAddress)
            && typeof(PhysicalAddress) != typeof(DeviceAddress)
            && typeof(VirtualAddress) != typeof(DeviceAddress);
    }

    private static bool TestDomainLocalOrdering()
    {
        TryCreatePhysical(0x1000, out PhysicalAddress lowPhysical);
        TryCreatePhysical(0x2000, out PhysicalAddress highPhysical);
        TryCreateVirtual(0x1000, out VirtualAddress lowVirtual);
        TryCreateVirtual(0x2000, out VirtualAddress highVirtual);
        TryCreateDevice(0x1000, out DeviceAddress lowDevice);
        TryCreateDevice(0x2000, out DeviceAddress highDevice);

        return lowPhysical.TryCompareTo(highPhysical, out int physicalLess)
                == AddressValueStatus.Success
            && highPhysical.TryCompareTo(lowPhysical, out int physicalGreater)
                == AddressValueStatus.Success
            && lowPhysical.TryCompareTo(lowPhysical, out int physicalEqual)
                == AddressValueStatus.Success
            && lowVirtual.TryCompareTo(highVirtual, out int virtualLess)
                == AddressValueStatus.Success
            && highVirtual.TryCompareTo(lowVirtual, out int virtualGreater)
                == AddressValueStatus.Success
            && lowDevice.TryCompareTo(highDevice, out int deviceLess)
                == AddressValueStatus.Success
            && highDevice.TryCompareTo(lowDevice, out int deviceGreater)
                == AddressValueStatus.Success
            && default(PhysicalAddress).TryCompareTo(
                highPhysical,
                out int invalidPhysicalLeft) == AddressValueStatus.InvalidAddress
            && lowVirtual.TryCompareTo(
                default,
                out int invalidVirtualRight) == AddressValueStatus.InvalidAddress
            && default(DeviceAddress).TryCompareTo(
                default,
                out int invalidDeviceBoth) == AddressValueStatus.InvalidAddress
            && physicalLess == -1
            && physicalGreater == 1
            && physicalEqual == 0
            && virtualLess == -1
            && virtualGreater == 1
            && deviceLess == -1
            && deviceGreater == 1
            && invalidPhysicalLeft == 0
            && invalidVirtualRight == 0
            && invalidDeviceBoth == 0;
    }

    private static bool TestNamedBoundarySurface()
    {
        return HasNoOperatorMethods(typeof(PhysicalAddress))
            && HasNoOperatorMethods(typeof(VirtualAddress))
            && HasNoOperatorMethods(typeof(DeviceAddress))
            && HasSameDomainOutParameter(typeof(PhysicalAddress))
            && HasSameDomainOutParameter(typeof(VirtualAddress))
            && HasSameDomainOutParameter(typeof(DeviceAddress))
            && HasSameDomainComparison(typeof(PhysicalAddress))
            && HasSameDomainComparison(typeof(VirtualAddress))
            && HasSameDomainComparison(typeof(DeviceAddress))
            && HasExactPublicApi(
                typeof(PhysicalAddress),
                [
                    "TryCreateFromUefiBoundary(UInt64,out PhysicalAddress)->AddressValueStatus",
                    "TryCreateAlignedFromUefiBoundary(UInt64,UInt64,out PhysicalAddress)->AddressValueStatus",
                    "TryCreateFromHardwareRegisterBoundary(UInt64,out PhysicalAddress)->AddressValueStatus",
                    "TryCreateAlignedFromHardwareRegisterBoundary(UInt64,UInt64,out PhysicalAddress)->AddressValueStatus",
                    "TryGetRawValueForUefiBoundary(out UInt64)->AddressValueStatus",
                    "TryGetRawValueForHardwareRegisterBoundary(out UInt64)->AddressValueStatus",
                    "IsAligned(UInt64)->Boolean",
                    "TryAdd(UInt64,out PhysicalAddress)->AddressValueStatus",
                    "TryGetEndExclusive(UInt64,out PhysicalAddress)->AddressValueStatus",
                    "TryAlignUp(UInt64,out PhysicalAddress)->AddressValueStatus",
                    "TryCompareTo(PhysicalAddress,out Int32)->AddressValueStatus",
                    "Equals(PhysicalAddress)->Boolean"
                ])
            && HasExactPublicApi(
                typeof(VirtualAddress),
                [
                    "TryCreateFromUefiBoundary(UInt64,out VirtualAddress)->AddressValueStatus",
                    "TryCreateAlignedFromUefiBoundary(UInt64,UInt64,out VirtualAddress)->AddressValueStatus",
                    "TryCreateFromHardwareRegisterBoundary(UInt64,out VirtualAddress)->AddressValueStatus",
                    "TryCreateAlignedFromHardwareRegisterBoundary(UInt64,UInt64,out VirtualAddress)->AddressValueStatus",
                    "TryGetRawValueForUefiBoundary(out UInt64)->AddressValueStatus",
                    "TryGetRawValueForHardwareRegisterBoundary(out UInt64)->AddressValueStatus",
                    "IsAligned(UInt64)->Boolean",
                    "TryAdd(UInt64,out VirtualAddress)->AddressValueStatus",
                    "TryGetEndExclusive(UInt64,out VirtualAddress)->AddressValueStatus",
                    "TryAlignUp(UInt64,out VirtualAddress)->AddressValueStatus",
                    "TryCompareTo(VirtualAddress,out Int32)->AddressValueStatus",
                    "Equals(VirtualAddress)->Boolean"
                ])
            && HasExactPublicApi(
                typeof(DeviceAddress),
                [
                    "TryCreateFromHardwareRegisterBoundary(UInt64,out DeviceAddress)->AddressValueStatus",
                    "TryCreateAlignedFromHardwareRegisterBoundary(UInt64,UInt64,out DeviceAddress)->AddressValueStatus",
                    "TryGetRawValueForHardwareRegisterBoundary(out UInt64)->AddressValueStatus",
                    "IsAligned(UInt64)->Boolean",
                    "TryAdd(UInt64,out DeviceAddress)->AddressValueStatus",
                    "TryGetEndExclusive(UInt64,out DeviceAddress)->AddressValueStatus",
                    "TryAlignUp(UInt64,out DeviceAddress)->AddressValueStatus",
                    "TryCompareTo(DeviceAddress,out Int32)->AddressValueStatus",
                    "Equals(DeviceAddress)->Boolean"
                ])
            && typeof(PhysicalAddress).GetProperty(
                "Value",
                BindingFlags.Public | BindingFlags.Instance) is null
            && typeof(VirtualAddress).GetProperty(
                "Value",
                BindingFlags.Public | BindingFlags.Instance) is null
            && typeof(DeviceAddress).GetProperty(
                "Value",
                BindingFlags.Public | BindingFlags.Instance) is null;
    }

    private static bool HasNoOperatorMethods(Type type)
    {
        MethodInfo[] methods = type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        foreach (MethodInfo method in methods)
        {
            if (method.Name.StartsWith("op_", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExactPublicApi(Type type, string[] expectedSignatures)
    {
        HashSet<string> expected = new HashSet<string>(
            expectedSignatures,
            StringComparer.Ordinal);
        HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);

        foreach (MethodInfo method in type.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.DeclaredOnly))
        {
            if (!method.IsSpecialName)
            {
                actual.Add(GetMethodSignature(method));
            }
        }

        return actual.SetEquals(expected);
    }

    private static string GetMethodSignature(MethodInfo method)
    {
        StringBuilder signature = new StringBuilder();
        signature.Append(method.Name);
        signature.Append('(');

        ParameterInfo[] parameters = method.GetParameters();
        for (int index = 0; index < parameters.Length; index++)
        {
            if (index != 0)
            {
                signature.Append(',');
            }

            ParameterInfo parameter = parameters[index];
            Type parameterType = parameter.ParameterType;
            if (parameter.IsOut)
            {
                signature.Append("out ");
            }
            else if (parameterType.IsByRef)
            {
                signature.Append("ref ");
            }

            if (parameterType.IsByRef)
            {
                parameterType = parameterType.GetElementType()!;
            }

            signature.Append(parameterType.Name);
        }

        signature.Append(")->");
        signature.Append(method.ReturnType.Name);
        return signature.ToString();
    }

    private static bool HasSameDomainOutParameter(Type type)
    {
        string[] methodNames = ["TryAdd", "TryGetEndExclusive", "TryAlignUp"];

        foreach (string methodName in methodNames)
        {
            MethodInfo? method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance);
            ParameterInfo[]? parameters = method?.GetParameters();

            if (method is null
                || method.ReturnType != typeof(AddressValueStatus)
                || parameters is null
                || parameters.Length != 2
                || !parameters[1].IsOut
                || parameters[1].ParameterType.GetElementType() != type)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSameDomainComparison(Type type)
    {
        MethodInfo? method = type.GetMethod(
            "TryCompareTo",
            BindingFlags.Public | BindingFlags.Instance);
        ParameterInfo[]? parameters = method?.GetParameters();

        return method is not null
            && method.ReturnType == typeof(AddressValueStatus)
            && parameters is not null
            && parameters.Length == 2
            && parameters[0].ParameterType == type
            && parameters[1].IsOut
            && parameters[1].ParameterType.GetElementType() == typeof(int);
    }

    private static bool TestNoRuntimeSourceSurface()
    {
        string source = File.ReadAllText(GetProductionSourcePath());
        string sourceWithoutComments = RemoveLineComments(source);
        string[] forbiddenFragments =
        [
            "using ",
            "using System",
            "System.",
            "System::",
            "throw ",
            "catch ",
            "finally ",
            "Reflection",
            "Activator",
            "Array.",
            "\\u",
            "\\U",
            "stackalloc",
            "typeof"
        ];

        foreach (string fragment in forbiddenFragments)
        {
            if (sourceWithoutComments.Contains(fragment, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (Regex.IsMatch(
                sourceWithoutComments,
                @"(?m)^\s*(?:global\s+)?using\b",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                sourceWithoutComments,
                @"\b(string|object|dynamic|delegate|interface|record|var|is|as|Object|Delegate|MulticastDelegate|Attribute|String|Array|Type|Exception|Span|ReadOnlySpan|Memory|ReadOnlyMemory)\b",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                sourceWithoutComments,
                @"\[\s*\]",
                RegexOptions.CultureInvariant)
            || Regex.Matches(
                sourceWithoutComments,
                @"\bclass\b",
                RegexOptions.CultureInvariant).Count != 1
            || !sourceWithoutComments.Contains(
                "internal static class AddressValueOperations",
                StringComparison.Ordinal))
        {
            return false;
        }

        foreach (Match allocation in Regex.Matches(
            sourceWithoutComments,
            @"\bnew\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant))
        {
            string targetType = allocation.Groups[1].Value;
            if (targetType != nameof(PhysicalAddress)
                && targetType != nameof(VirtualAddress)
                && targetType != nameof(DeviceAddress))
            {
                return false;
            }
        }

        if (Regex.IsMatch(
            sourceWithoutComments,
            @"\bnew\s*\(",
            RegexOptions.CultureInvariant))
        {
            return false;
        }

        return HasOnlyValueTypePublicSurface(typeof(PhysicalAddress))
            && HasOnlyValueTypePublicSurface(typeof(VirtualAddress))
            && HasOnlyValueTypePublicSurface(typeof(DeviceAddress))
            && HasExactAddressOperationsSurface();
    }

    private static string RemoveLineComments(string source)
    {
        StringBuilder result = new StringBuilder(source.Length);
        foreach (string line in source.Split('\n'))
        {
            string trimmed = line.TrimStart();
            if (!trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                result.AppendLine(line);
            }
        }

        return result.ToString();
    }

    private static bool HasOnlyValueTypePublicSurface(Type type)
    {
        foreach (FieldInfo field in type.GetFields(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic))
        {
            if (!field.FieldType.IsValueType)
            {
                return false;
            }
        }

        foreach (MethodInfo method in type.GetMethods(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.DeclaredOnly))
        {
            if (method.ReturnType != typeof(void) && !method.ReturnType.IsValueType)
            {
                return false;
            }

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                Type parameterType = parameter.ParameterType;
                if (parameterType.IsByRef)
                {
                    parameterType = parameterType.GetElementType()!;
                }

                if (!parameterType.IsValueType)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasExactAddressOperationsSurface()
    {
        Type? operations = typeof(PhysicalAddress).Assembly.GetType(
            "OS.Boot.AddressValueOperations",
            throwOnError: false,
            ignoreCase: false);
        if (operations is null
            || !operations.IsAbstract
            || !operations.IsSealed
            || operations.GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static
                    | BindingFlags.DeclaredOnly).Length != 0)
        {
            return false;
        }

        FieldInfo[] fields = operations.GetFields(
            BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.DeclaredOnly);
        if (fields.Length != 1
            || fields[0].Name != "MaximumValue"
            || fields[0].FieldType != typeof(ulong)
            || !fields[0].IsPrivate
            || !fields[0].IsStatic
            || !fields[0].IsLiteral)
        {
            return false;
        }

        HashSet<string> expected = new HashSet<string>(
            [
                "ValidateAddress(UInt64)->AddressValueStatus",
                "TryGetRawValue(UInt64,out UInt64)->AddressValueStatus",
                "TryCompare(UInt64,UInt64,out Int32)->AddressValueStatus",
                "ValidateAlignedAddress(UInt64,UInt64)->AddressValueStatus",
                "IsAligned(UInt64,UInt64)->Boolean",
                "TryAdd(UInt64,UInt64,out UInt64)->AddressValueStatus",
                "TryGetEndExclusive(UInt64,UInt64,out UInt64)->AddressValueStatus",
                "TryAlignUp(UInt64,UInt64,out UInt64)->AddressValueStatus",
                "IsPowerOfTwo(UInt64)->Boolean"
            ],
            StringComparer.Ordinal);
        HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);

        foreach (MethodInfo method in operations.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName
                || (method.ReturnType != typeof(void) && !method.ReturnType.IsValueType))
            {
                return false;
            }

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                Type parameterType = parameter.ParameterType.IsByRef
                    ? parameter.ParameterType.GetElementType()!
                    : parameter.ParameterType;
                if (!parameterType.IsValueType)
                {
                    return false;
                }
            }

            actual.Add(GetMethodSignature(method));
        }

        return actual.SetEquals(expected);
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string workspace = CreateNoStdLibWorkspace();

        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                using OS.Boot;

                internal static class PositiveBoundaryProbe
                {
                    public static AddressValueStatus Exercise()
                    {
                        AddressValueStatus status =
                            PhysicalAddress.TryCreateFromUefiBoundary(
                                0x1000UL,
                                out PhysicalAddress physical);
                        if (status != AddressValueStatus.Success)
                        {
                            return status;
                        }

                        status = physical.TryAdd(0x1000UL, out PhysicalAddress end);
                        if (status != AddressValueStatus.Success)
                        {
                            return status;
                        }

                        status = physical.TryCompareTo(end, out int comparison);
                        if (status != AddressValueStatus.Success || comparison >= 0)
                        {
                            return AddressValueStatus.InvalidAddress;
                        }

                        return end.TryGetRawValueForHardwareRegisterBoundary(
                            out ulong rawValue);
                    }
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            NoStdLibBuildResult result = RunNoStdLibBuild(workspace, restore: true);
            string outputAssembly = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.AddressCompileClosure.dll");
            if (result.TimedOut
                || result.ExitCode != 0
                || ContainsInfrastructureFailure(result.Output)
                || !HasZeroAssemblyReferences(outputAssembly))
            {
                Console.WriteLine(
                    "M01_001_NOSTDLIB_COMPILE_DIAGNOSTIC "
                    + NormalizeCompilerOutput(result.Output));
                return false;
            }

            return true;
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static bool TestProhibitedExpressionsFailCompilation()
    {
        string workspace = CreateNoStdLibWorkspace();

        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                using OS.Boot;

                internal static class ProhibitedBoundaryProbe
                {
                #line 1 "unnamed-ingress.cs"
                    private static void UnnamedIngress() { PhysicalAddress.TryCreate(1UL, out PhysicalAddress value); }
                #line 1 "unnamed-egress.cs"
                    private static void UnnamedEgress(PhysicalAddress value) { ulong raw = value.Value; }
                #line 1 "implicit-raw.cs"
                    private static void ImplicitRaw() { PhysicalAddress value = 1UL; }
                #line 1 "explicit-raw.cs"
                    private static void ExplicitRaw() { PhysicalAddress value = (PhysicalAddress)1UL; }
                #line 1 "cross-assignment.cs"
                    private static void CrossAssignment(PhysicalAddress physical) { VirtualAddress value = physical; }
                #line 1 "cross-add.cs"
                    private static void CrossAdd(PhysicalAddress physical, VirtualAddress virtualAddress) { PhysicalAddress value = physical + virtualAddress; }
                #line 1 "cross-subtract.cs"
                    private static void CrossSubtract(PhysicalAddress physical, DeviceAddress device) { PhysicalAddress value = physical - device; }
                #line 1 "cross-equality.cs"
                    private static void CrossEquality(VirtualAddress virtualAddress, DeviceAddress device) { bool value = virtualAddress == device; }
                #line 1 "cross-order.cs"
                    private static void CrossOrder(PhysicalAddress physical, VirtualAddress virtualAddress) { bool value = physical < virtualAddress; }
                #line 1 "wrong-domain-out.cs"
                    private static void WrongDomainOut(PhysicalAddress physical) { physical.TryAdd(1UL, out VirtualAddress value); }
                #line 1 "device-uefi-boundary.cs"
                    private static void DeviceUefiBoundary() { DeviceAddress.TryCreateFromUefiBoundary(1UL, out DeviceAddress value); }
                #line default
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            NoStdLibBuildResult result = RunNoStdLibBuild(workspace, restore: true);
            if (result.TimedOut
                || result.ExitCode == 0
                || ContainsInfrastructureFailure(result.Output))
            {
                Console.WriteLine(
                    "M01_001_NEGATIVE_COMPILE_DIAGNOSTIC "
                    + NormalizeCompilerOutput(result.Output));
                return false;
            }

            (string FileName, string DiagnosticId)[] expectedDiagnostics =
            [
                ("unnamed-ingress.cs", "CS0117"),
                ("unnamed-egress.cs", "CS1061"),
                ("implicit-raw.cs", "CS0029"),
                ("explicit-raw.cs", "CS0030"),
                ("cross-assignment.cs", "CS0029"),
                ("cross-add.cs", "CS0019"),
                ("cross-subtract.cs", "CS0019"),
                ("cross-equality.cs", "CS0019"),
                ("cross-order.cs", "CS0019"),
                ("wrong-domain-out.cs", "CS1503"),
                ("device-uefi-boundary.cs", "CS0117")
            ];

            foreach ((string fileName, string diagnosticId) in expectedDiagnostics)
            {
                if (!HasCompilerDiagnostic(result.Output, fileName, diagnosticId))
                {
                    Console.WriteLine(
                        "M01_001_MISSING_NEGATIVE_DIAGNOSTIC file="
                        + fileName
                        + " diagnostic="
                        + diagnosticId
                        + " output="
                        + NormalizeCompilerOutput(result.Output));
                    return false;
                }
            }

            return true;
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-001-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.Copy(
            Path.Combine(repositoryRoot, "global.json"),
            Path.Combine(workspace, "global.json"));
            File.WriteAllText(
            Path.Combine(workspace, "NoStdLibShim.cs"),
            """
            namespace System
            {
                public class Object { }
                public abstract class ValueType : Object { }
                public abstract class Enum : ValueType { }
                public abstract class Delegate : Object { }
                public abstract class MulticastDelegate : Delegate { }
                public class Attribute : Object { }
                public struct Void { }
                public struct Boolean { }
                public struct Byte { }
                public struct Int32 { }
                public struct UInt64 { }
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
            }
            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string projectText =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.AddressCompileClosure</AssemblyName>
                <TargetFramework>net10.0</TargetFramework>
                <LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType>
                <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <ImplicitUsings>disable</ImplicitUsings>
                <Nullable>disable</Nullable>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <NoStdLib>true</NoStdLib>
                <NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles>
                <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
                <EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
                <DefineConstants>NOSTDLIB_EFI</DefineConstants>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <Deterministic>true</Deterministic>
                <DebugType>none</DebugType>
                <DebugSymbols>false</DebugSymbols>
                <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
                <OutputPath>$(MSBuildProjectDirectory)\out\</OutputPath>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="NoStdLibShim.cs" />
                <Compile Include="__TYPED_ADDRESSES__" Link="Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs" />
                <Compile Include="Probe.cs" />
              </ItemGroup>
              <Target Name="CustomizeReferences"
                      BeforeTargets="BeforeCompile"
                      AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup>
                  <ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" />
                </ItemGroup>
              </Target>
            </Project>
            """
            .Replace(
                "__TYPED_ADDRESSES__",
                EscapeXml(GetProductionSourcePath()),
                StringComparison.Ordinal);

            File.WriteAllText(
            Path.Combine(workspace, "M01.001.NoStdLib.csproj"),
            projectText,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return workspace;
        }
        catch
        {
            try
            {
                DeleteNoStdLibWorkspace(workspace);
            }
            catch
            {
                // Preserve the workspace-creation failure as the primary evidence.
            }

            throw;
        }
    }

    private static NoStdLibBuildResult RunNoStdLibBuild(string workspace, bool restore)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workspace
        };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add("M01.001.NoStdLib.csproj");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-m:1");
        start.ArgumentList.Add("--no-dependencies");
        start.ArgumentList.Add("--no-incremental");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-v:minimal");
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
        start.ArgumentList.Add("-p:NuGetAudit=false");
        if (!restore)
        {
            start.ArgumentList.Add("--no-restore");
        }
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";

        using Process process = new Process { StartInfo = start };
        process.Start();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        bool exited = process.WaitForExit(90_000);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Timeout remains the primary failure.
                }
            }
        }

        bool processStopped = exited || process.WaitForExit(5_000);
        bool streamsCompleted = Task.WaitAll(
            [standardOutput, standardError],
            millisecondsTimeout: 5_000);
        string output = streamsCompleted
            ? standardOutput.GetAwaiter().GetResult()
                + Environment.NewLine
                + standardError.GetAwaiter().GetResult()
            : "Compiler output streams did not close within the bounded timeout.";
        return new NoStdLibBuildResult(
            processStopped ? process.ExitCode : -1,
            timedOut: !exited || !processStopped || !streamsCompleted,
            output);
    }

    private static bool ContainsInfrastructureFailure(string output)
    {
        string[] infrastructureDiagnostics =
        [
            "error MSB",
            "error NETSDK",
            "error NU",
            "CS0518",
            "CS0656",
            "CS8021"
        ];

        return infrastructureDiagnostics.Any(diagnostic =>
            output.Contains(diagnostic, StringComparison.Ordinal));
    }

    private static bool HasZeroAssemblyReferences(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
        {
            return false;
        }

        MetadataReader metadata = peReader.GetMetadataReader();
        return metadata.AssemblyReferences.Count == 0;
    }

    private static bool HasCompilerDiagnostic(
        string output,
        string fileName,
        string diagnosticId)
    {
        foreach (string line in output.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains(fileName + "(", StringComparison.OrdinalIgnoreCase)
                && line.Contains("error " + diagnosticId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeCompilerOutput(string output)
    {
        return output
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string GetProductionSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "TypedPhysicalVirtualDeviceAddressPODWrappers.cs");
    }

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string fullWorkspace = Path.GetFullPath(workspace);
        string expectedPrefix = Path.TrimEndingDirectorySeparator(tempRoot)
            + Path.DirectorySeparatorChar;
        string leafName = Path.GetFileName(fullWorkspace);

        if (!fullWorkspace.StartsWith(
                expectedPrefix,
                StringComparison.OrdinalIgnoreCase)
            || !leafName.StartsWith(
                "clros-m01-001-",
                StringComparison.Ordinal)
            || leafName.Length <= "clros-m01-001-".Length)
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected NoStdLib workspace.");
        }

        if (Directory.Exists(fullWorkspace))
        {
            Directory.Delete(fullWorkspace, recursive: true);
        }
    }

    private readonly struct NoStdLibBuildResult
    {
        public NoStdLibBuildResult(int exitCode, bool timedOut, string output)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Output = output;
        }

        public int ExitCode { get; }

        public bool TimedOut { get; }

        public string Output { get; }
    }

    private static AddressValueStatus TryCreatePhysical(
        ulong value,
        out PhysicalAddress address)
    {
        return PhysicalAddress.TryCreateFromUefiBoundary(value, out address);
    }

    private static AddressValueStatus TryCreateVirtual(
        ulong value,
        out VirtualAddress address)
    {
        return VirtualAddress.TryCreateFromUefiBoundary(value, out address);
    }

    private static AddressValueStatus TryCreateDevice(
        ulong value,
        out DeviceAddress address)
    {
        return DeviceAddress.TryCreateFromHardwareRegisterBoundary(value, out address);
    }

    private static AddressValueStatus TryCreateAlignedPhysical(
        ulong value,
        ulong alignment,
        out PhysicalAddress address)
    {
        return PhysicalAddress.TryCreateAlignedFromUefiBoundary(
            value,
            alignment,
            out address);
    }

    private static AddressValueStatus TryCreateAlignedVirtual(
        ulong value,
        ulong alignment,
        out VirtualAddress address)
    {
        return VirtualAddress.TryCreateAlignedFromUefiBoundary(
            value,
            alignment,
            out address);
    }

    private static AddressValueStatus TryCreateAlignedDevice(
        ulong value,
        ulong alignment,
        out DeviceAddress address)
    {
        return DeviceAddress.TryCreateAlignedFromHardwareRegisterBoundary(
            value,
            alignment,
            out address);
    }

    private static ulong GetRaw(PhysicalAddress address)
    {
        AddressValueStatus status =
            address.TryGetRawValueForUefiBoundary(out ulong value);
        return status == AddressValueStatus.Success ? value : 0;
    }

    private static ulong GetRaw(VirtualAddress address)
    {
        AddressValueStatus status =
            address.TryGetRawValueForUefiBoundary(out ulong value);
        return status == AddressValueStatus.Success ? value : 0;
    }

    private static ulong GetRaw(DeviceAddress address)
    {
        AddressValueStatus status =
            address.TryGetRawValueForHardwareRegisterBoundary(out ulong value);
        return status == AddressValueStatus.Success ? value : 0;
    }

    private static bool TestOffsetSuccess()
    {
        TryCreatePhysical(0x1000, out PhysicalAddress physical);
        TryCreateVirtual(0x1000, out VirtualAddress virtualAddress);
        TryCreateDevice(0x1000, out DeviceAddress device);

        TryCreatePhysical(ulong.MaxValue - 1, out PhysicalAddress highPhysical);
        TryCreateVirtual(ulong.MaxValue - 1, out VirtualAddress highVirtual);
        TryCreateDevice(ulong.MaxValue - 1, out DeviceAddress highDevice);

        return physical.TryAdd(0, out PhysicalAddress unchangedPhysical)
                == AddressValueStatus.Success
            && virtualAddress.TryAdd(0, out VirtualAddress unchangedVirtual)
                == AddressValueStatus.Success
            && device.TryAdd(0, out DeviceAddress unchangedDevice)
                == AddressValueStatus.Success
            && GetRaw(unchangedPhysical) == 0x1000
            && GetRaw(unchangedVirtual) == 0x1000
            && GetRaw(unchangedDevice) == 0x1000
            && physical.TryAdd(0x2000, out PhysicalAddress addedPhysical)
                == AddressValueStatus.Success
            && virtualAddress.TryAdd(0x2000, out VirtualAddress addedVirtual)
                == AddressValueStatus.Success
            && device.TryAdd(0x2000, out DeviceAddress addedDevice)
                == AddressValueStatus.Success
            && GetRaw(addedPhysical) == 0x3000
            && GetRaw(addedVirtual) == 0x3000
            && GetRaw(addedDevice) == 0x3000
            && highPhysical.TryAdd(1, out PhysicalAddress maximumPhysical)
                == AddressValueStatus.Success
            && highVirtual.TryAdd(1, out VirtualAddress maximumVirtual)
                == AddressValueStatus.Success
            && highDevice.TryAdd(1, out DeviceAddress maximumDevice)
                == AddressValueStatus.Success
            && GetRaw(maximumPhysical) == ulong.MaxValue
            && GetRaw(maximumVirtual) == ulong.MaxValue
            && GetRaw(maximumDevice) == ulong.MaxValue;
    }

    private static bool TestOffsetFailures()
    {
        TryCreatePhysical(ulong.MaxValue, out PhysicalAddress maximumPhysical);
        TryCreateVirtual(ulong.MaxValue, out VirtualAddress maximumVirtual);
        TryCreateDevice(ulong.MaxValue, out DeviceAddress maximumDevice);

        TryCreatePhysical(ulong.MaxValue - 1, out PhysicalAddress nearMaximumPhysical);
        TryCreateVirtual(ulong.MaxValue - 1, out VirtualAddress nearMaximumVirtual);
        TryCreateDevice(ulong.MaxValue - 1, out DeviceAddress nearMaximumDevice);

        return maximumPhysical.TryAdd(1, out PhysicalAddress physicalOverflow)
                == AddressValueStatus.AddressOverflow
            && maximumVirtual.TryAdd(1, out VirtualAddress virtualOverflow)
                == AddressValueStatus.AddressOverflow
            && maximumDevice.TryAdd(1, out DeviceAddress deviceOverflow)
                == AddressValueStatus.AddressOverflow
            && nearMaximumPhysical.TryAdd(2, out PhysicalAddress physicalSecondOverflow)
                == AddressValueStatus.AddressOverflow
            && nearMaximumVirtual.TryAdd(2, out VirtualAddress virtualSecondOverflow)
                == AddressValueStatus.AddressOverflow
            && nearMaximumDevice.TryAdd(2, out DeviceAddress deviceSecondOverflow)
                == AddressValueStatus.AddressOverflow
            && default(PhysicalAddress).TryAdd(1, out PhysicalAddress invalidPhysical)
                == AddressValueStatus.InvalidAddress
            && default(VirtualAddress).TryAdd(1, out VirtualAddress invalidVirtual)
                == AddressValueStatus.InvalidAddress
            && default(DeviceAddress).TryAdd(1, out DeviceAddress invalidDevice)
                == AddressValueStatus.InvalidAddress
            && physicalOverflow.Equals(default(PhysicalAddress))
            && virtualOverflow.Equals(default(VirtualAddress))
            && deviceOverflow.Equals(default(DeviceAddress))
            && physicalSecondOverflow.Equals(default(PhysicalAddress))
            && virtualSecondOverflow.Equals(default(VirtualAddress))
            && deviceSecondOverflow.Equals(default(DeviceAddress))
            && invalidPhysical.Equals(default(PhysicalAddress))
            && invalidVirtual.Equals(default(VirtualAddress))
            && invalidDevice.Equals(default(DeviceAddress));
    }

    private static bool TestEndExclusiveSuccess()
    {
        TryCreatePhysical(0x1000, out PhysicalAddress physical);
        TryCreateVirtual(0x1000, out VirtualAddress virtualAddress);
        TryCreateDevice(0x1000, out DeviceAddress device);

        TryCreatePhysical(ulong.MaxValue - 0xFFF, out PhysicalAddress highPhysical);
        TryCreateVirtual(ulong.MaxValue - 0xFFF, out VirtualAddress highVirtual);
        TryCreateDevice(ulong.MaxValue - 0xFFF, out DeviceAddress highDevice);

        return physical.TryGetEndExclusive(0x1000, out PhysicalAddress physicalEnd)
                == AddressValueStatus.Success
            && virtualAddress.TryGetEndExclusive(0x1000, out VirtualAddress virtualEnd)
                == AddressValueStatus.Success
            && device.TryGetEndExclusive(0x1000, out DeviceAddress deviceEnd)
                == AddressValueStatus.Success
            && GetRaw(physicalEnd) == 0x2000
            && GetRaw(virtualEnd) == 0x2000
            && GetRaw(deviceEnd) == 0x2000
            && highPhysical.TryGetEndExclusive(0xFFF, out PhysicalAddress maximumPhysical)
                == AddressValueStatus.Success
            && highVirtual.TryGetEndExclusive(0xFFF, out VirtualAddress maximumVirtual)
                == AddressValueStatus.Success
            && highDevice.TryGetEndExclusive(0xFFF, out DeviceAddress maximumDevice)
                == AddressValueStatus.Success
            && GetRaw(maximumPhysical) == ulong.MaxValue
            && GetRaw(maximumVirtual) == ulong.MaxValue
            && GetRaw(maximumDevice) == ulong.MaxValue;
    }

    private static bool TestEndExclusiveFailures()
    {
        TryCreatePhysical(0x1000, out PhysicalAddress physical);
        TryCreateVirtual(0x1000, out VirtualAddress virtualAddress);
        TryCreateDevice(0x1000, out DeviceAddress device);

        TryCreatePhysical(ulong.MaxValue - 0xFFF, out PhysicalAddress highPhysical);
        TryCreateVirtual(ulong.MaxValue - 0xFFF, out VirtualAddress highVirtual);
        TryCreateDevice(ulong.MaxValue - 0xFFF, out DeviceAddress highDevice);

        return physical.TryGetEndExclusive(0, out PhysicalAddress zeroLengthPhysical)
                == AddressValueStatus.ZeroLength
            && virtualAddress.TryGetEndExclusive(0, out VirtualAddress zeroLengthVirtual)
                == AddressValueStatus.ZeroLength
            && device.TryGetEndExclusive(0, out DeviceAddress zeroLengthDevice)
                == AddressValueStatus.ZeroLength
            && highPhysical.TryGetEndExclusive(0x1000, out PhysicalAddress overflowPhysical)
                == AddressValueStatus.AddressOverflow
            && highVirtual.TryGetEndExclusive(0x1000, out VirtualAddress overflowVirtual)
                == AddressValueStatus.AddressOverflow
            && highDevice.TryGetEndExclusive(0x1000, out DeviceAddress overflowDevice)
                == AddressValueStatus.AddressOverflow
            && default(PhysicalAddress).TryGetEndExclusive(1, out PhysicalAddress invalidPhysical)
                == AddressValueStatus.InvalidAddress
            && default(VirtualAddress).TryGetEndExclusive(1, out VirtualAddress invalidVirtual)
                == AddressValueStatus.InvalidAddress
            && default(DeviceAddress).TryGetEndExclusive(1, out DeviceAddress invalidDevice)
                == AddressValueStatus.InvalidAddress
            && zeroLengthPhysical.Equals(default(PhysicalAddress))
            && zeroLengthVirtual.Equals(default(VirtualAddress))
            && zeroLengthDevice.Equals(default(DeviceAddress))
            && overflowPhysical.Equals(default(PhysicalAddress))
            && overflowVirtual.Equals(default(VirtualAddress))
            && overflowDevice.Equals(default(DeviceAddress))
            && invalidPhysical.Equals(default(PhysicalAddress))
            && invalidVirtual.Equals(default(VirtualAddress))
            && invalidDevice.Equals(default(DeviceAddress));
    }

    private static bool TestAlignmentValidation()
    {
        TryCreatePhysical(0x2001, out PhysicalAddress oddPhysical);
        TryCreateVirtual(0x2001, out VirtualAddress oddVirtual);
        TryCreateDevice(0x2001, out DeviceAddress oddDevice);

        return TryCreateAlignedPhysical(0x2000, 4096, out PhysicalAddress physical)
                == AddressValueStatus.Success
            && TryCreateAlignedVirtual(0x2000, 4096, out VirtualAddress virtualAddress)
                == AddressValueStatus.Success
            && TryCreateAlignedDevice(0x2000, 4096, out DeviceAddress device)
                == AddressValueStatus.Success
            && physical.IsAligned(1)
            && virtualAddress.IsAligned(1)
            && device.IsAligned(1)
            && physical.IsAligned(4096)
            && virtualAddress.IsAligned(4096)
            && device.IsAligned(4096)
            && !oddPhysical.IsAligned(4096)
            && !oddVirtual.IsAligned(4096)
            && !oddDevice.IsAligned(4096)
            && !physical.IsAligned(0)
            && !virtualAddress.IsAligned(3)
            && !device.IsAligned(4095)
            && TryCreateAlignedPhysical(0x2001, 4096, out PhysicalAddress misalignedPhysical)
                == AddressValueStatus.MisalignedAddress
            && TryCreateAlignedVirtual(0x2001, 4096, out VirtualAddress misalignedVirtual)
                == AddressValueStatus.MisalignedAddress
            && TryCreateAlignedDevice(0x2001, 4096, out DeviceAddress misalignedDevice)
                == AddressValueStatus.MisalignedAddress
            && TryCreateAlignedPhysical(0x2000, 0, out PhysicalAddress zeroAlignmentPhysical)
                == AddressValueStatus.InvalidAlignment
            && TryCreateAlignedVirtual(0x2000, 3, out VirtualAddress invalidAlignmentVirtual)
                == AddressValueStatus.InvalidAlignment
            && TryCreateAlignedDevice(0x2000, 4095, out DeviceAddress invalidAlignmentDevice)
                == AddressValueStatus.InvalidAlignment
            && TryCreateAlignedPhysical(0, 0, out PhysicalAddress zeroAndInvalidPhysical)
                == AddressValueStatus.ZeroAddress
            && TryCreateAlignedVirtual(0, 3, out VirtualAddress zeroAndInvalidVirtual)
                == AddressValueStatus.ZeroAddress
            && TryCreateAlignedDevice(0, 4095, out DeviceAddress zeroAndInvalidDevice)
                == AddressValueStatus.ZeroAddress
            && !default(PhysicalAddress).IsAligned(1)
            && !default(VirtualAddress).IsAligned(4096)
            && !default(DeviceAddress).IsAligned(1UL << 63)
            && misalignedPhysical.Equals(default(PhysicalAddress))
            && misalignedVirtual.Equals(default(VirtualAddress))
            && misalignedDevice.Equals(default(DeviceAddress))
            && zeroAlignmentPhysical.Equals(default(PhysicalAddress))
            && invalidAlignmentVirtual.Equals(default(VirtualAddress))
            && invalidAlignmentDevice.Equals(default(DeviceAddress))
            && zeroAndInvalidPhysical.Equals(default(PhysicalAddress))
            && zeroAndInvalidVirtual.Equals(default(VirtualAddress))
            && zeroAndInvalidDevice.Equals(default(DeviceAddress));
    }

    private static bool TestAlignUpSuccess()
    {
        TryCreatePhysical(0x2000, out PhysicalAddress alignedPhysical);
        TryCreateVirtual(0x2000, out VirtualAddress alignedVirtual);
        TryCreateDevice(0x2000, out DeviceAddress alignedDevice);

        TryCreatePhysical(0x1001, out PhysicalAddress physical);
        TryCreateVirtual(0x1001, out VirtualAddress virtualAddress);
        TryCreateDevice(0x1001, out DeviceAddress device);

        TryCreatePhysical(1, out PhysicalAddress lowPhysical);
        TryCreateVirtual(1, out VirtualAddress lowVirtual);
        TryCreateDevice(1, out DeviceAddress lowDevice);

        return alignedPhysical.TryAlignUp(4096, out PhysicalAddress unchangedPhysical)
                == AddressValueStatus.Success
            && alignedVirtual.TryAlignUp(4096, out VirtualAddress unchangedVirtual)
                == AddressValueStatus.Success
            && alignedDevice.TryAlignUp(4096, out DeviceAddress unchangedDevice)
                == AddressValueStatus.Success
            && GetRaw(unchangedPhysical) == 0x2000
            && GetRaw(unchangedVirtual) == 0x2000
            && GetRaw(unchangedDevice) == 0x2000
            && physical.TryAlignUp(4096, out PhysicalAddress roundedPhysical)
                == AddressValueStatus.Success
            && virtualAddress.TryAlignUp(4096, out VirtualAddress roundedVirtual)
                == AddressValueStatus.Success
            && device.TryAlignUp(4096, out DeviceAddress roundedDevice)
                == AddressValueStatus.Success
            && GetRaw(roundedPhysical) == 0x2000
            && GetRaw(roundedVirtual) == 0x2000
            && GetRaw(roundedDevice) == 0x2000
            && physical.TryAlignUp(1, out PhysicalAddress unitPhysical)
                == AddressValueStatus.Success
            && virtualAddress.TryAlignUp(1, out VirtualAddress unitVirtual)
                == AddressValueStatus.Success
            && device.TryAlignUp(1, out DeviceAddress unitDevice)
                == AddressValueStatus.Success
            && GetRaw(unitPhysical) == GetRaw(physical)
            && GetRaw(unitVirtual) == GetRaw(virtualAddress)
            && GetRaw(unitDevice) == GetRaw(device)
            && lowPhysical.TryAlignUp(1UL << 63, out PhysicalAddress highPhysical)
                == AddressValueStatus.Success
            && lowVirtual.TryAlignUp(1UL << 63, out VirtualAddress highVirtual)
                == AddressValueStatus.Success
            && lowDevice.TryAlignUp(1UL << 63, out DeviceAddress highDevice)
                == AddressValueStatus.Success
            && GetRaw(highPhysical) == 1UL << 63
            && GetRaw(highVirtual) == 1UL << 63
            && GetRaw(highDevice) == 1UL << 63;
    }

    private static bool TestAlignUpFailures()
    {
        TryCreatePhysical(ulong.MaxValue, out PhysicalAddress maximumPhysical);
        TryCreateVirtual(ulong.MaxValue, out VirtualAddress maximumVirtual);
        TryCreateDevice(ulong.MaxValue, out DeviceAddress maximumDevice);

        return maximumPhysical.TryAlignUp(2, out PhysicalAddress overflowPhysical)
                == AddressValueStatus.AddressOverflow
            && maximumVirtual.TryAlignUp(2, out VirtualAddress overflowVirtual)
                == AddressValueStatus.AddressOverflow
            && maximumDevice.TryAlignUp(2, out DeviceAddress overflowDevice)
                == AddressValueStatus.AddressOverflow
            && maximumPhysical.TryAlignUp(0, out PhysicalAddress invalidPhysical)
                == AddressValueStatus.InvalidAlignment
            && maximumVirtual.TryAlignUp(3, out VirtualAddress invalidVirtual)
                == AddressValueStatus.InvalidAlignment
            && maximumDevice.TryAlignUp(4095, out DeviceAddress invalidDevice)
                == AddressValueStatus.InvalidAlignment
            && default(PhysicalAddress).TryAlignUp(1, out PhysicalAddress defaultPhysical)
                == AddressValueStatus.InvalidAddress
            && default(VirtualAddress).TryAlignUp(1, out VirtualAddress defaultVirtual)
                == AddressValueStatus.InvalidAddress
            && default(DeviceAddress).TryAlignUp(1, out DeviceAddress defaultDevice)
                == AddressValueStatus.InvalidAddress
            && default(PhysicalAddress).TryAlignUp(0, out PhysicalAddress defaultAndInvalidPhysical)
                == AddressValueStatus.InvalidAddress
            && overflowPhysical.Equals(default(PhysicalAddress))
            && overflowVirtual.Equals(default(VirtualAddress))
            && overflowDevice.Equals(default(DeviceAddress))
            && invalidPhysical.Equals(default(PhysicalAddress))
            && invalidVirtual.Equals(default(VirtualAddress))
            && invalidDevice.Equals(default(DeviceAddress))
            && defaultPhysical.Equals(default(PhysicalAddress))
            && defaultVirtual.Equals(default(VirtualAddress))
            && defaultDevice.Equals(default(DeviceAddress))
            && defaultAndInvalidPhysical.Equals(default(PhysicalAddress));
    }

    private static bool TestFailureOutputs()
    {
        PhysicalAddress physical = default;
        VirtualAddress virtualAddress = default;
        DeviceAddress device = default;

        AddressValueStatus physicalStatus = TryCreatePhysical(
            0,
            out physical);
        AddressValueStatus virtualStatus = TryCreateAlignedVirtual(
            0x1001,
            4096,
            out virtualAddress);
        AddressValueStatus deviceStatus = TryCreateAlignedDevice(
            0x1000,
            0,
            out device);

        return physicalStatus == AddressValueStatus.ZeroAddress
            && virtualStatus == AddressValueStatus.MisalignedAddress
            && deviceStatus == AddressValueStatus.InvalidAlignment
            && physical.Equals(default(PhysicalAddress))
            && virtualAddress.Equals(default(VirtualAddress))
            && device.Equals(default(DeviceAddress));
    }
}
