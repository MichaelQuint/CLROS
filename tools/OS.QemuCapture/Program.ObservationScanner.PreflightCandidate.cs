using OS.Tools.QemuRunner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal static partial class Program
{
    private sealed partial class ObservationScanner
    {
        public sealed class PreflightCandidate
        {
            public const int Size = 56;
            private PreflightCandidate(long physicalAddress, byte[] bytes, ulong cr0, ulong rawCr3, ulong rawCr4, ulong ia32Efer, uint cpuidMaximumExtendedLeaf, uint cpuidAddressSizesEax, ulong rflags)
            {
                PhysicalAddress = physicalAddress;
                Bytes = bytes;
                Cr0 = cr0;
                RawCr3 = rawCr3;
                RawCr4 = rawCr4;
                Ia32Efer = ia32Efer;
                CpuidMaximumExtendedLeaf = cpuidMaximumExtendedLeaf;
                CpuidAddressSizesEax = cpuidAddressSizesEax;
                Rflags = rflags;
            }

            public long PhysicalAddress
            {
                get;
            }
            public byte[] Bytes
            {
                get;
            }
            public ulong Cr0
            {
                get;
            }
            public ulong RawCr3
            {
                get;
            }
            public ulong RawCr4
            {
                get;
            }
            public ulong Ia32Efer
            {
                get;
            }
            public uint CpuidMaximumExtendedLeaf
            {
                get;
            }
            public uint CpuidAddressSizesEax
            {
                get;
            }
            public byte PhysicalAddressWidth => (byte)CpuidAddressSizesEax;
            public ulong Rflags
            {
                get;
            }

            public static PreflightCandidate Read(byte[] bytes, int offset, long physicalAddress)
            {
                return new PreflightCandidate(physicalAddress, Copy(bytes, offset, Size), ReadUInt64(bytes, offset), ReadUInt64(bytes, offset + 8), ReadUInt64(bytes, offset + 16), ReadUInt64(bytes, offset + 24), ReadUInt32(bytes, offset + 32), ReadUInt32(bytes, offset + 36), ReadUInt64(bytes, offset + 40));
            }

            public bool Matches(RuntimeCandidate runtime)
            {
                return Cr0 == runtime.OriginalCr0 && RawCr3 == runtime.OriginalCr3 && RawCr4 == runtime.OriginalCr4 && Ia32Efer == runtime.OriginalEfer && Rflags == runtime.OriginalRflags;
            }

            public void ValidateModeAndAddress(RuntimeCandidate runtime)
            {
                const ulong RequiredCr0 = 0x80010001UL;
                const ulong RequiredEfer = 0x0d00UL;
                const ulong Cr4Pae = 1UL << 5;
                const ulong Cr4La57 = 1UL << 12;
                const ulong Cr4Pcide = 1UL << 17;
                const ulong Cr4Cet = 1UL << 23;
                const ulong RflagsOne = 1UL << 1;
                const ulong RflagsTrap = 1UL << 8;
                const ulong RflagsDirection = 1UL << 10;
                const ulong Cr3AllowedLow = (1UL << 3) | (1UL << 4);
                byte width = PhysicalAddressWidth;
                if (CpuidMaximumExtendedLeaf < 0x80000008U || width < 36 || width > 52 || (Cr0 & RequiredCr0) != RequiredCr0 || (RawCr4 & Cr4Pae) == 0 || (RawCr4 & (Cr4La57 | Cr4Pcide | Cr4Cet)) != 0 || (Ia32Efer & RequiredEfer) != RequiredEfer || (Rflags & RflagsOne) == 0 || (Rflags & (RflagsTrap | RflagsDirection)) != 0)
                {
                    throw new InvalidDataException("preflight_mode_invalid");
                }

                ulong physicalMask = (1UL << width) - 1;
                ulong allowedCr3 = (physicalMask & ~0xfffUL) | Cr3AllowedLow;
                ulong firmwareRoot = RawCr3 & (physicalMask & ~0xfffUL);
                if (runtime.PhysicalAddressMask != physicalMask || (RawCr3 & ~allowedCr3) != 0 || firmwareRoot == 0)
                {
                    throw new InvalidDataException("preflight_cr3_or_width_invalid");
                }
            }
        }
    }
}
