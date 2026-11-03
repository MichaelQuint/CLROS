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
        public sealed class ObservationEvidence
        {
            public ObservationEvidence(string memorySha256, List<PreflightCandidate> preflightCandidates, PreflightCandidate preflight, RuntimeCandidate runtime)
            {
                MemorySha256 = memorySha256;
                PreflightCandidateCount = preflightCandidates.Count;
                StringBuilder addresses = new StringBuilder();
                for (int index = 0; index < preflightCandidates.Count; index++)
                {
                    if (index != 0)
                    {
                        addresses.Append(';');
                    }

                    addresses.Append(Hex(checked((ulong)preflightCandidates[index].PhysicalAddress)));
                }

                PreflightPhysicalAddresses = addresses.ToString();
                PreflightPhysicalAddress = checked((ulong)preflight.PhysicalAddress);
                PreflightPodSha256 = HashBytes(preflight.Bytes);
                PreflightPodBase64 = Convert.ToBase64String(preflight.Bytes);
                Cr0 = preflight.Cr0;
                RawCr3 = preflight.RawCr3;
                RawCr4 = preflight.RawCr4;
                Ia32Efer = preflight.Ia32Efer;
                CpuidMaximumExtendedLeaf = preflight.CpuidMaximumExtendedLeaf;
                CpuidAddressSizesEax = preflight.CpuidAddressSizesEax;
                PhysicalAddressWidth = preflight.PhysicalAddressWidth;
                PhysicalAddressMask = runtime.PhysicalAddressMask;
                OwnedCr3 = runtime.OwnedCr3;
                Rflags = preflight.Rflags;
                RuntimeStatePhysicalAddress = checked((ulong)runtime.PhysicalAddress);
                RuntimeStatePodSha256 = HashBytes(runtime.StateBytes);
                RuntimeStatePodBase64 = Convert.ToBase64String(runtime.StateBytes);
                TransitionCanaryPhysicalAddress = checked((ulong)(runtime.PhysicalAddress + RuntimeCandidate.StateSize));
                TransitionCanaryValue = runtime.TransitionCanaryValue;
                CallGatePhysicalAddress = checked((ulong)runtime.GatePhysicalAddress);
                CallGatePodSha256 = HashBytes(runtime.GateBytes);
                CallGatePodBase64 = Convert.ToBase64String(runtime.GateBytes);
                PostReturnRflags = runtime.PostReturnRflags;
                PostReturnCr3 = runtime.PostReturnCr3;
                PostReturnCr4 = runtime.PostReturnCr4;
                FirmwareCr3Readback = runtime.FirmwareCr3Readback;
                RestoredCr4Readback = runtime.RestoredCr4Readback;
                RestoredRflagsIntent = runtime.RestoredRflagsIntent;
            }

            public string MemorySha256
            {
                get;
            }
            public int PreflightCandidateCount
            {
                get;
            }
            public string PreflightPhysicalAddresses
            {
                get;
            }
            public ulong PreflightPhysicalAddress
            {
                get;
            }
            public string PreflightPodSha256
            {
                get;
            }
            public string PreflightPodBase64
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
            public byte PhysicalAddressWidth
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
            public ulong Rflags
            {
                get;
            }
            public ulong RuntimeStatePhysicalAddress
            {
                get;
            }
            public string RuntimeStatePodSha256
            {
                get;
            }
            public string RuntimeStatePodBase64
            {
                get;
            }
            public ulong TransitionCanaryPhysicalAddress
            {
                get;
            }
            public ulong TransitionCanaryValue
            {
                get;
            }
            public ulong CallGatePhysicalAddress
            {
                get;
            }
            public string CallGatePodSha256
            {
                get;
            }
            public string CallGatePodBase64
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

            private static string HashBytes(byte[] bytes)
            {
                return Convert.ToHexString(SHA256.HashData(bytes));
            }
        }
    }
}
