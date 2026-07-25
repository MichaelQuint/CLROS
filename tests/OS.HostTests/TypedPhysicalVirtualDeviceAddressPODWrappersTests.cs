using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        Run("m01.001 wrappers preserve raw values", TestRawValuePreservation, ref failures);
        Run("m01.001 equality is domain local", TestDomainLocalEquality, ref failures);
        Run("m01.001 wrappers prohibit cross-domain conversion", TestNoCrossDomainConversions, ref failures);
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
        PropertyInfo? valueProperty = type.GetProperty(
            "Value",
            BindingFlags.Instance | BindingFlags.Public);

        return type.IsValueType
            && type.IsLayoutSequential
            && type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false)
            && fields.Length == 1
            && fields[0].IsPrivate
            && fields[0].FieldType == typeof(ulong)
            && fields[0].IsInitOnly
            && Marshal.OffsetOf<T>(fields[0].Name).ToInt64() == 0
            && type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Length == 0
            && valueProperty is not null
            && valueProperty.PropertyType == typeof(ulong)
            && valueProperty.SetMethod is null
            && Marshal.SizeOf<T>() == sizeof(ulong)
            && Unsafe.SizeOf<T>() == sizeof(ulong)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    }

    private static bool TestDefaultAndZero()
    {
        AddressValueStatus physicalStatus = PhysicalAddress.TryCreate(
            0,
            out PhysicalAddress physical);
        AddressValueStatus virtualStatus = VirtualAddress.TryCreate(
            0,
            out VirtualAddress virtualAddress);
        AddressValueStatus deviceStatus = DeviceAddress.TryCreate(
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

    private static bool TestRawValuePreservation()
    {
        ulong[] values = [1, 0x1000, 0xFEDCBA9876543210, ulong.MaxValue];

        foreach (ulong value in values)
        {
            if (PhysicalAddress.TryCreate(value, out PhysicalAddress physical)
                    != AddressValueStatus.Success
                || VirtualAddress.TryCreate(value, out VirtualAddress virtualAddress)
                    != AddressValueStatus.Success
                || DeviceAddress.TryCreate(value, out DeviceAddress device)
                    != AddressValueStatus.Success
                || physical.Value != value
                || virtualAddress.Value != value
                || device.Value != value
                || !physical.IsValid
                || !virtualAddress.IsValid
                || !device.IsValid)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestDomainLocalEquality()
    {
        PhysicalAddress.TryCreate(0x2000, out PhysicalAddress physical);
        PhysicalAddress.TryCreate(0x2000, out PhysicalAddress samePhysical);
        PhysicalAddress.TryCreate(0x3000, out PhysicalAddress otherPhysical);
        VirtualAddress.TryCreate(0x2000, out VirtualAddress virtualAddress);
        VirtualAddress.TryCreate(0x2000, out VirtualAddress sameVirtual);
        VirtualAddress.TryCreate(0x3000, out VirtualAddress otherVirtual);
        DeviceAddress.TryCreate(0x2000, out DeviceAddress device);
        DeviceAddress.TryCreate(0x2000, out DeviceAddress sameDevice);
        DeviceAddress.TryCreate(0x3000, out DeviceAddress otherDevice);

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

    private static bool TestNoCrossDomainConversions()
    {
        return HasNoConversionOperators(typeof(PhysicalAddress))
            && HasNoConversionOperators(typeof(VirtualAddress))
            && HasNoConversionOperators(typeof(DeviceAddress))
            && HasSameDomainOutParameter(typeof(PhysicalAddress))
            && HasSameDomainOutParameter(typeof(VirtualAddress))
            && HasSameDomainOutParameter(typeof(DeviceAddress))
            && HasSameDomainFactories(typeof(PhysicalAddress))
            && HasSameDomainFactories(typeof(VirtualAddress))
            && HasSameDomainFactories(typeof(DeviceAddress));
    }

    private static bool HasNoConversionOperators(Type type)
    {
        MethodInfo[] methods = type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        foreach (MethodInfo method in methods)
        {
            if (method.Name == "op_Implicit" || method.Name == "op_Explicit")
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSameDomainFactories(Type type)
    {
        string[] methodNames = ["TryCreate", "TryCreateAligned"];

        foreach (string methodName in methodNames)
        {
            MethodInfo? method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            ParameterInfo[]? parameters = method?.GetParameters();

            if (method is null
                || method.ReturnType != typeof(AddressValueStatus)
                || parameters is null
                || parameters.Length < 2
                || !parameters[^1].IsOut
                || parameters[^1].ParameterType.GetElementType() != type)
            {
                return false;
            }
        }

        return true;
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

    private static bool TestOffsetSuccess()
    {
        PhysicalAddress.TryCreate(0x1000, out PhysicalAddress physical);
        VirtualAddress.TryCreate(0x1000, out VirtualAddress virtualAddress);
        DeviceAddress.TryCreate(0x1000, out DeviceAddress device);

        PhysicalAddress.TryCreate(ulong.MaxValue - 1, out PhysicalAddress highPhysical);
        VirtualAddress.TryCreate(ulong.MaxValue - 1, out VirtualAddress highVirtual);
        DeviceAddress.TryCreate(ulong.MaxValue - 1, out DeviceAddress highDevice);

        return physical.TryAdd(0, out PhysicalAddress unchangedPhysical)
                == AddressValueStatus.Success
            && virtualAddress.TryAdd(0, out VirtualAddress unchangedVirtual)
                == AddressValueStatus.Success
            && device.TryAdd(0, out DeviceAddress unchangedDevice)
                == AddressValueStatus.Success
            && unchangedPhysical.Value == 0x1000
            && unchangedVirtual.Value == 0x1000
            && unchangedDevice.Value == 0x1000
            && physical.TryAdd(0x2000, out PhysicalAddress addedPhysical)
                == AddressValueStatus.Success
            && virtualAddress.TryAdd(0x2000, out VirtualAddress addedVirtual)
                == AddressValueStatus.Success
            && device.TryAdd(0x2000, out DeviceAddress addedDevice)
                == AddressValueStatus.Success
            && addedPhysical.Value == 0x3000
            && addedVirtual.Value == 0x3000
            && addedDevice.Value == 0x3000
            && highPhysical.TryAdd(1, out PhysicalAddress maximumPhysical)
                == AddressValueStatus.Success
            && highVirtual.TryAdd(1, out VirtualAddress maximumVirtual)
                == AddressValueStatus.Success
            && highDevice.TryAdd(1, out DeviceAddress maximumDevice)
                == AddressValueStatus.Success
            && maximumPhysical.Value == ulong.MaxValue
            && maximumVirtual.Value == ulong.MaxValue
            && maximumDevice.Value == ulong.MaxValue;
    }

    private static bool TestOffsetFailures()
    {
        PhysicalAddress.TryCreate(ulong.MaxValue, out PhysicalAddress maximumPhysical);
        VirtualAddress.TryCreate(ulong.MaxValue, out VirtualAddress maximumVirtual);
        DeviceAddress.TryCreate(ulong.MaxValue, out DeviceAddress maximumDevice);

        PhysicalAddress.TryCreate(ulong.MaxValue - 1, out PhysicalAddress nearMaximumPhysical);
        VirtualAddress.TryCreate(ulong.MaxValue - 1, out VirtualAddress nearMaximumVirtual);
        DeviceAddress.TryCreate(ulong.MaxValue - 1, out DeviceAddress nearMaximumDevice);

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
        PhysicalAddress.TryCreate(0x1000, out PhysicalAddress physical);
        VirtualAddress.TryCreate(0x1000, out VirtualAddress virtualAddress);
        DeviceAddress.TryCreate(0x1000, out DeviceAddress device);

        PhysicalAddress.TryCreate(ulong.MaxValue - 0xFFF, out PhysicalAddress highPhysical);
        VirtualAddress.TryCreate(ulong.MaxValue - 0xFFF, out VirtualAddress highVirtual);
        DeviceAddress.TryCreate(ulong.MaxValue - 0xFFF, out DeviceAddress highDevice);

        return physical.TryGetEndExclusive(0x1000, out PhysicalAddress physicalEnd)
                == AddressValueStatus.Success
            && virtualAddress.TryGetEndExclusive(0x1000, out VirtualAddress virtualEnd)
                == AddressValueStatus.Success
            && device.TryGetEndExclusive(0x1000, out DeviceAddress deviceEnd)
                == AddressValueStatus.Success
            && physicalEnd.Value == 0x2000
            && virtualEnd.Value == 0x2000
            && deviceEnd.Value == 0x2000
            && highPhysical.TryGetEndExclusive(0xFFF, out PhysicalAddress maximumPhysical)
                == AddressValueStatus.Success
            && highVirtual.TryGetEndExclusive(0xFFF, out VirtualAddress maximumVirtual)
                == AddressValueStatus.Success
            && highDevice.TryGetEndExclusive(0xFFF, out DeviceAddress maximumDevice)
                == AddressValueStatus.Success
            && maximumPhysical.Value == ulong.MaxValue
            && maximumVirtual.Value == ulong.MaxValue
            && maximumDevice.Value == ulong.MaxValue;
    }

    private static bool TestEndExclusiveFailures()
    {
        PhysicalAddress.TryCreate(0x1000, out PhysicalAddress physical);
        VirtualAddress.TryCreate(0x1000, out VirtualAddress virtualAddress);
        DeviceAddress.TryCreate(0x1000, out DeviceAddress device);

        PhysicalAddress.TryCreate(ulong.MaxValue - 0xFFF, out PhysicalAddress highPhysical);
        VirtualAddress.TryCreate(ulong.MaxValue - 0xFFF, out VirtualAddress highVirtual);
        DeviceAddress.TryCreate(ulong.MaxValue - 0xFFF, out DeviceAddress highDevice);

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
        PhysicalAddress.TryCreate(0x2001, out PhysicalAddress oddPhysical);
        VirtualAddress.TryCreate(0x2001, out VirtualAddress oddVirtual);
        DeviceAddress.TryCreate(0x2001, out DeviceAddress oddDevice);

        return PhysicalAddress.TryCreateAligned(0x2000, 4096, out PhysicalAddress physical)
                == AddressValueStatus.Success
            && VirtualAddress.TryCreateAligned(0x2000, 4096, out VirtualAddress virtualAddress)
                == AddressValueStatus.Success
            && DeviceAddress.TryCreateAligned(0x2000, 4096, out DeviceAddress device)
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
            && PhysicalAddress.TryCreateAligned(0x2001, 4096, out PhysicalAddress misalignedPhysical)
                == AddressValueStatus.MisalignedAddress
            && VirtualAddress.TryCreateAligned(0x2001, 4096, out VirtualAddress misalignedVirtual)
                == AddressValueStatus.MisalignedAddress
            && DeviceAddress.TryCreateAligned(0x2001, 4096, out DeviceAddress misalignedDevice)
                == AddressValueStatus.MisalignedAddress
            && PhysicalAddress.TryCreateAligned(0x2000, 0, out PhysicalAddress zeroAlignmentPhysical)
                == AddressValueStatus.InvalidAlignment
            && VirtualAddress.TryCreateAligned(0x2000, 3, out VirtualAddress invalidAlignmentVirtual)
                == AddressValueStatus.InvalidAlignment
            && DeviceAddress.TryCreateAligned(0x2000, 4095, out DeviceAddress invalidAlignmentDevice)
                == AddressValueStatus.InvalidAlignment
            && PhysicalAddress.TryCreateAligned(0, 0, out PhysicalAddress zeroAndInvalidPhysical)
                == AddressValueStatus.ZeroAddress
            && VirtualAddress.TryCreateAligned(0, 3, out VirtualAddress zeroAndInvalidVirtual)
                == AddressValueStatus.ZeroAddress
            && DeviceAddress.TryCreateAligned(0, 4095, out DeviceAddress zeroAndInvalidDevice)
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
        PhysicalAddress.TryCreate(0x2000, out PhysicalAddress alignedPhysical);
        VirtualAddress.TryCreate(0x2000, out VirtualAddress alignedVirtual);
        DeviceAddress.TryCreate(0x2000, out DeviceAddress alignedDevice);

        PhysicalAddress.TryCreate(0x1001, out PhysicalAddress physical);
        VirtualAddress.TryCreate(0x1001, out VirtualAddress virtualAddress);
        DeviceAddress.TryCreate(0x1001, out DeviceAddress device);

        PhysicalAddress.TryCreate(1, out PhysicalAddress lowPhysical);
        VirtualAddress.TryCreate(1, out VirtualAddress lowVirtual);
        DeviceAddress.TryCreate(1, out DeviceAddress lowDevice);

        return alignedPhysical.TryAlignUp(4096, out PhysicalAddress unchangedPhysical)
                == AddressValueStatus.Success
            && alignedVirtual.TryAlignUp(4096, out VirtualAddress unchangedVirtual)
                == AddressValueStatus.Success
            && alignedDevice.TryAlignUp(4096, out DeviceAddress unchangedDevice)
                == AddressValueStatus.Success
            && unchangedPhysical.Value == 0x2000
            && unchangedVirtual.Value == 0x2000
            && unchangedDevice.Value == 0x2000
            && physical.TryAlignUp(4096, out PhysicalAddress roundedPhysical)
                == AddressValueStatus.Success
            && virtualAddress.TryAlignUp(4096, out VirtualAddress roundedVirtual)
                == AddressValueStatus.Success
            && device.TryAlignUp(4096, out DeviceAddress roundedDevice)
                == AddressValueStatus.Success
            && roundedPhysical.Value == 0x2000
            && roundedVirtual.Value == 0x2000
            && roundedDevice.Value == 0x2000
            && physical.TryAlignUp(1, out PhysicalAddress unitPhysical)
                == AddressValueStatus.Success
            && virtualAddress.TryAlignUp(1, out VirtualAddress unitVirtual)
                == AddressValueStatus.Success
            && device.TryAlignUp(1, out DeviceAddress unitDevice)
                == AddressValueStatus.Success
            && unitPhysical.Value == physical.Value
            && unitVirtual.Value == virtualAddress.Value
            && unitDevice.Value == device.Value
            && lowPhysical.TryAlignUp(1UL << 63, out PhysicalAddress highPhysical)
                == AddressValueStatus.Success
            && lowVirtual.TryAlignUp(1UL << 63, out VirtualAddress highVirtual)
                == AddressValueStatus.Success
            && lowDevice.TryAlignUp(1UL << 63, out DeviceAddress highDevice)
                == AddressValueStatus.Success
            && highPhysical.Value == 1UL << 63
            && highVirtual.Value == 1UL << 63
            && highDevice.Value == 1UL << 63;
    }

    private static bool TestAlignUpFailures()
    {
        PhysicalAddress.TryCreate(ulong.MaxValue, out PhysicalAddress maximumPhysical);
        VirtualAddress.TryCreate(ulong.MaxValue, out VirtualAddress maximumVirtual);
        DeviceAddress.TryCreate(ulong.MaxValue, out DeviceAddress maximumDevice);

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

        AddressValueStatus physicalStatus = PhysicalAddress.TryCreate(
            0,
            out physical);
        AddressValueStatus virtualStatus = VirtualAddress.TryCreateAligned(
            0x1001,
            4096,
            out virtualAddress);
        AddressValueStatus deviceStatus = DeviceAddress.TryCreateAligned(
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
