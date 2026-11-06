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
        public sealed class RuntimeCandidate
        {
            public const int StateSize = 480;
            public const int GateOffset = 0x300;
            public const int GateSize = 160;
            public const int TotalSpan = GateOffset + GateSize;
            private RuntimeCandidate(long physicalAddress, byte[] stateBytes, byte[] gateBytes, ulong transitionCanaryValue)
            {
                PhysicalAddress = physicalAddress;
                StateBytes = stateBytes;
                GateBytes = gateBytes;
                OriginalRflags = ReadUInt64(stateBytes, 200);
                OriginalCr0 = ReadUInt64(stateBytes, 208);
                OriginalCr3 = ReadUInt64(stateBytes, 216);
                OriginalCr4 = ReadUInt64(stateBytes, 224);
                OriginalEfer = ReadUInt64(stateBytes, 232);
                PhysicalAddressMask = ReadUInt64(stateBytes, 240);
                OwnedCr3 = ReadUInt64(stateBytes, 8);
                TransitionCanaryValue = transitionCanaryValue;
                FirmwareCr3Readback = ReadUInt64(stateBytes, 408);
                RestoredCr4Readback = ReadUInt64(stateBytes, 424);
                RestoredRflagsIntent = ReadUInt64(stateBytes, 432);
                PostReturnRflags = ReadUInt64(gateBytes, 104);
                PostReturnCr3 = ReadUInt64(gateBytes, 112);
                PostReturnCr4 = ReadUInt64(gateBytes, 120);
            }

            public long PhysicalAddress
            {
                get;
            }
            public long GatePhysicalAddress => PhysicalAddress + GateOffset;
            public byte[] StateBytes
            {
                get;
            }
            public byte[] GateBytes
            {
                get;
            }
            public ulong OriginalRflags
            {
                get;
            }
            public ulong OriginalCr0
            {
                get;
            }
            public ulong OriginalCr3
            {
                get;
            }
            public ulong OriginalCr4
            {
                get;
            }
            public ulong OriginalEfer
            {
                get;
            }
            public ulong PhysicalAddressMask
            {
                get;
            }
            public ulong OwnedCr3
            {
                get;
            }
            public ulong TransitionCanaryValue
            {
                get;
            }
            public ulong FirmwareCr3Readback
            {
                get;
            }
            public ulong RestoredCr4Readback
            {
                get;
            }
            public ulong RestoredRflagsIntent
            {
                get;
            }
            public ulong PostReturnRflags
            {
                get;
            }
            public ulong PostReturnCr3
            {
                get;
            }
            public ulong PostReturnCr4
            {
                get;
            }

            public static RuntimeCandidate? TryRead(byte[] bytes, int offset, long physicalAddress)
            {
                if (ReadUInt32(bytes, offset) != StateSize || ReadUInt16(bytes, offset + 4) != 1 || bytes[offset + 6] != 1 || bytes[offset + 7] != 0 || ReadUInt64(bytes, offset + 184) != (ulong)physicalAddress)
                {
                    return null;
                }

                byte[] state = Copy(bytes, offset, StateSize);
                byte[] gate = Copy(bytes, offset + GateOffset, GateSize);
                ulong originalRflags = ReadUInt64(state, 200);
                ulong originalCr3 = ReadUInt64(state, 216);
                ulong originalCr4 = ReadUInt64(state, 224);
                ulong ownedCr3 = ReadUInt64(state, 8);
                ulong cliRflags = ReadUInt64(state, 272);
                ulong physicalPageMask = ReadUInt64(state, 240) & ~0xfffUL;
                ulong allowedOwnedCr3Mask = physicalPageMask | (1UL << 3) | (1UL << 4);
                const ulong MutableBeforeCliReadback = (1UL << 0) | (1UL << 2) | (1UL << 4) | (1UL << 6) | (1UL << 7) | (1UL << 9) | (1UL << 11);
                bool originalPge = (originalCr4 & (1UL << 7)) != 0;
                if (ReadUInt32(gate, 0) != GateSize || ReadUInt16(gate, 4) != 1 || gate[6] != 1 || gate[7] != 3 || ReadUInt64(gate, 8) != (ulong)(physicalAddress + GateOffset) || ReadUInt64(gate, 48) != originalRflags || ReadUInt64(gate, 56) != originalCr3 || ReadUInt64(gate, 64) != originalCr4 || ReadUInt64(gate, 72) == 0 || ReadUInt32(gate, 80) != 1 || gate[84] != (((originalRflags & (1UL << 9)) != 0) ? (byte)1 : (byte)0) || gate[85] != (originalPge ? (byte)1 : (byte)0) || gate[86] != 1 || gate[87] != 1 || ReadUInt64(gate, 88) != ReadUInt64(gate, 72) || ReadUInt64(gate, 96) != ReadUInt64(gate, 72) || ReadUInt64(gate, 104) != originalRflags || ReadUInt64(gate, 112) != originalCr3 || ReadUInt64(gate, 120) != originalCr4 || ReadUInt64(gate, 128) != ReadUInt64(gate, 72) || ReadUInt32(gate, 136) != 1 || gate[140] != 1 || gate[141] != 1 || ReadUInt16(gate, 142) != 0 || ReadUInt32(gate, 144) != 0x4b47534fU || ReadUInt32(gate, 148) != 0 || ReadUInt64(gate, 152) != 0 || ReadUInt32(state, 180) != 0 || physicalPageMask == 0 || (ownedCr3 & ~allowedOwnedCr3Mask) != 0 || (ownedCr3 & physicalPageMask) == 0 || (ownedCr3 & physicalPageMask) == (originalCr3 & physicalPageMask) || (cliRflags & ~MutableBeforeCliReadback) != (originalRflags & ~MutableBeforeCliReadback) || (cliRflags & (1UL << 9)) != 0 || ReadUInt64(state, 280) != (originalCr4 & ~(1UL << 7)) || ReadUInt64(state, 288) != ownedCr3 || ReadUInt64(state, 408) != originalCr3 || ReadUInt64(state, 424) != originalCr4 || ReadUInt64(state, 432) != originalRflags || ReadUInt64(state, 248) != ReadUInt64(state, 440) || ReadUInt64(state, 448) != 1 || ReadUInt64(state, 456) != 1 || state[464] != 1 || state[465] != 17 || state[466] < 1 || state[466] > 4 || state[467] != 1 || state[468] != (originalPge ? (byte)1 : (byte)0) || state[469] != 1 || state[470] != 1 || state[471] != 12 || ReadUInt64(state, 472) != 0 || ReadUInt64(state, 296) != ReadUInt64(state, 160) || ReadUInt64(state, 304) != ReadUInt64(state, 168) || ReadUInt64(state, 416) != ReadUInt64(state, 168) || ReadUInt64(bytes, offset + StateSize) != ReadUInt64(state, 168))
                {
                    return null;
                }

                return new RuntimeCandidate(physicalAddress, state, gate, ReadUInt64(bytes, offset + StateSize));
            }
        }
    }
}
