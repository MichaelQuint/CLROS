namespace Internal.Runtime
{
    internal struct MethodTable { }
}

namespace System
{
    internal static class RuntimeFailFast
    {
        public static void Halt()
        {
            while (true)
            {
            }
        }
    }

    public class Object
    {
#pragma warning disable 169
        private IntPtr _methodTable;
#pragma warning restore 169
    }

    public struct Void { }
    public struct Boolean { }
    public struct Char { }
    public struct SByte { }
    public struct Byte { }
    public struct Int16 { }
    public struct UInt16 { }
    public struct Int32 { }
    public struct UInt32 { }
    public struct Int64 { }
    public struct UInt64 { }
    public struct IntPtr { }
    public struct UIntPtr { }
    public struct Single { }
    public struct Double { }

    public abstract class ValueType { }
    public abstract class Enum : ValueType { }
    public abstract class Type { }
    public sealed class RuntimeType : Type { }
    public abstract class Array { }
    public abstract class Delegate { }
    public abstract class MulticastDelegate : Delegate { }
    public sealed class String { }
    public class Attribute { }

    public static unsafe class Buffer
    {
        public static void MemoryCopy(void* source, void* destination, long destinationSizeInBytes, long sourceBytesToCopy)
        {
            if (sourceBytesToCopy < 0 || destinationSizeInBytes < 0 || sourceBytesToCopy > destinationSizeInBytes)
            {
                Halt();
            }

            Memmove(ref *(byte*)destination, ref *(byte*)source, (nuint)sourceBytesToCopy);
        }

        public static void MemoryCopy(void* source, void* destination, ulong destinationSizeInBytes, ulong sourceBytesToCopy)
        {
            if (sourceBytesToCopy > destinationSizeInBytes)
            {
                Halt();
            }

            Memmove(ref *(byte*)destination, ref *(byte*)source, (nuint)sourceBytesToCopy);
        }

        internal static void Memmove(ref byte destination, ref byte source, nuint length)
        {
            fixed (byte* destinationPointer = &destination)
            fixed (byte* sourcePointer = &source)
            {
                if (destinationPointer < sourcePointer || destinationPointer >= sourcePointer + length)
                {
                    for (nuint i = 0; i < length; i++)
                    {
                        destinationPointer[i] = sourcePointer[i];
                    }
                }
                else
                {
                    for (nuint i = length; i > 0; i--)
                    {
                        destinationPointer[i - 1] = sourcePointer[i - 1];
                    }
                }
            }
        }

        internal static void BulkMoveWithWriteBarrier(ref byte destination, ref byte source, nuint length)
        {
            Memmove(ref destination, ref source, length);
        }

        private static void Halt()
        {
            RuntimeFailFast.Halt();
        }
    }

    internal static unsafe class SpanHelpers
    {
        internal static void ClearWithoutReferences(ref byte destination, nuint byteLength)
        {
            fixed (byte* destinationPointer = &destination)
            {
                for (nuint i = 0; i < byteLength; i++)
                {
                    destinationPointer[i] = 0;
                }
            }
        }

        internal static void ClearWithReferences(ref IntPtr destination, nuint pointerLength)
        {
            fixed (IntPtr* destinationPointer = &destination)
            {
                byte* bytePointer = (byte*)destinationPointer;
                nuint byteLength = pointerLength * (nuint)sizeof(IntPtr);
                for (nuint i = 0; i < byteLength; i++)
                {
                    bytePointer[i] = 0;
                }
            }
        }

        internal static void ZeroMemory(ref byte destination, nuint byteLength)
        {
            ClearWithoutReferences(ref destination, byteLength);
        }

        internal static void Memmove(ref byte destination, ref byte source, nuint byteLength)
        {
            Buffer.Memmove(ref destination, ref source, byteLength);
        }

        internal static void Fill(ref byte destination, byte value, nuint byteLength)
        {
            fixed (byte* destinationPointer = &destination)
            {
                for (nuint i = 0; i < byteLength; i++)
                {
                    destinationPointer[i] = value;
                }
            }
        }

        internal static void Fill<T>(ref T destination, T value, nuint elementCount)
        {
            FillSingleOrHalt(ref destination, value, elementCount);
        }

        internal static void Fill<T>(ref T destination, nuint elementCount, T value)
        {
            FillSingleOrHalt(ref destination, value, elementCount);
        }

        private static void FillSingleOrHalt<T>(ref T destination, T value, nuint elementCount)
        {
            if (elementCount == 0)
            {
                return;
            }

            destination = value;
            if (elementCount != 1)
            {
                RuntimeFailFast.Halt();
            }
        }
    }

    public enum AttributeTargets { }

    public sealed class AttributeUsageAttribute : Attribute
    {
        public AttributeUsageAttribute(AttributeTargets validOn)
        {
        }

        public bool AllowMultiple { get; set; }

        public bool Inherited { get; set; }
    }

    public struct RuntimeTypeHandle { }
    public struct RuntimeMethodHandle { }
    public struct RuntimeFieldHandle { }

    namespace Runtime
    {
        internal sealed class RuntimeExportAttribute : Attribute
        {
            public RuntimeExportAttribute(string entry)
            {
            }
        }

        internal static unsafe class X64StackProbeRuntime
        {
            private static ulong s_probeEntry;

            public static bool TryInstall(ulong probeEntry)
            {
                if (probeEntry == 0
                    || (probeEntry & 15) != 0
                    || s_probeEntry != 0)
                {
                    return false;
                }

                s_probeEntry = probeEntry;
                return true;
            }

            public static bool TryClear(ulong expectedProbeEntry)
            {
                if (expectedProbeEntry == 0
                    || (s_probeEntry != 0
                        && s_probeEntry != expectedProbeEntry))
                {
                    return false;
                }

                s_probeEntry = 0;
                return true;
            }

            [RuntimeExport("RhpStackProbe")]
            private static void RhpStackProbe()
            {
                ulong probeEntry = s_probeEntry;
                if (probeEntry == 0)
                {
                    while (true)
                    {
                    }
                }

                ((delegate* managed<void>)(void*)probeEntry)();
            }
        }

        internal static unsafe class TypeCast
        {
            [RuntimeExport("RhTypeCast_IsInstanceOfClass")]
            public static object IsInstanceOfClass(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastClass")]
            public static object CheckCastClass(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastClassSpecial")]
            public static object CheckCastClassSpecial(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_IsInstanceOfArray")]
            public static object IsInstanceOfArray(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastArray")]
            public static object CheckCastArray(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_IsInstanceOfInterface")]
            public static object IsInstanceOfInterface(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastInterface")]
            public static object CheckCastInterface(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_IsInstanceOfAny")]
            public static object IsInstanceOfAny(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastAny")]
            public static object CheckCastAny(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_CheckCastAny_NoCacheLookup")]
            public static object CheckCastAny_NoCacheLookup(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return value;
            }

            [RuntimeExport("RhTypeCast_IsInstanceOfException")]
            public static bool IsInstanceOfException(Internal.Runtime.MethodTable* targetType, object value)
            {
                RuntimeFailFast.Halt();
                return false;
            }
        }
    }

    namespace Runtime.CompilerServices
    {
        public static class RuntimeFeature
        {
            public const string UnmanagedSignatureCallingConvention = nameof(UnmanagedSignatureCallingConvention);
        }

        public static class RuntimeHelpers
        {
            public static unsafe int OffsetToStringData
            {
                get { return sizeof(IntPtr) + sizeof(int); }
            }
        }
    }
}

namespace System.Runtime.InteropServices
{
    public sealed class UnmanagedCallersOnlyAttribute : System.Attribute
    {
        public UnmanagedCallersOnlyAttribute()
        {
        }
    }

    public interface IDynamicInterfaceCastable
    {
        bool IsInterfaceImplemented(System.RuntimeTypeHandle interfaceType, bool throwIfNotImplemented);

        System.RuntimeTypeHandle GetInterfaceImplementation(System.RuntimeTypeHandle interfaceType);

        System.RuntimeTypeHandle GetDynamicInterfaceImplementation(System.RuntimeTypeHandle interfaceType);
    }
}

namespace Internal.Runtime.CompilerHelpers
{
    using System;
    using System.Runtime;

    internal static class ThrowHelpers
    {
        private static void ThrowOverflowException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowIndexOutOfRangeException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowNullReferenceException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowDivideByZeroException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowArrayTypeMismatchException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowArrayMismatchException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowPlatformNotSupportedException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowTypeLoadException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowInvalidProgramException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowInvalidCastException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowBadImageFormatException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowMissingMethodException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowMissingFieldException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowMarshalDirectiveException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowNotSupportedException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowOutOfMemoryException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowRankException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowFeatureBodyRemoved()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowArgumentException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowArgumentNullException()
        {
            RuntimeFailFast.Halt();
        }

        private static void ThrowArgumentOutOfRangeException()
        {
            RuntimeFailFast.Halt();
        }
    }

    internal static class StartupCodeHelpers
    {
        [RuntimeExport("RhpReversePInvoke")]
        private static void RhpReversePInvoke(IntPtr frame)
        {
        }

        [RuntimeExport("RhpReversePInvokeReturn")]
        private static void RhpReversePInvokeReturn(IntPtr frame)
        {
        }

        [RuntimeExport("RhpPInvoke")]
        private static void RhpPInvoke(IntPtr frame)
        {
        }

        [RuntimeExport("RhpPInvokeReturn")]
        private static void RhpPInvokeReturn(IntPtr frame)
        {
        }

        [RuntimeExport("RhpFallbackFailFast")]
        private static void RhpFallbackFailFast()
        {
            RuntimeFailFast.Halt();
        }
    }
}
