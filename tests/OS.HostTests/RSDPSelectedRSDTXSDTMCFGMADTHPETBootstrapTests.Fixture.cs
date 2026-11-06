using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Std.Binary;
using Bootstrap = OS.Boot.RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap;

internal static partial class RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrapTests
{
    private sealed class Fixture
    {
        internal ulong RsdpAddress = 0x1000;
        internal ulong RootAddress = 0x2000;
        internal byte[] Rsdp;
        internal byte[] Root;
        internal byte[] Mcfg = Sdt(AcpiTableDirectory.McfgSignature, 61, 0x31);
        internal byte[] Madt = Sdt(Bootstrap.MadtSignature, 53, 0x71);
        internal byte[] Hpet = Sdt(Bootstrap.HpetSignature, 57, 0xA1);
        internal AcpiTableSource[] Sources;
        internal ulong[] Scratch = new ulong[8];
        internal byte[] Destination = new byte[1024];
        internal Fixture(bool extended = true, bool fallback = false)
        {
            bool xsdt = extended && !fallback;
            Root = RootTable(xsdt, 0x3000, 0x4000, 0x5000);
            Rsdp = RsdpTable(extended, xsdt ? 0x9000u : 0x2000u, xsdt ? 0x2000UL : 0);
            Sources = [new(0x3000, Mcfg), new(0x4000, Madt), new(0x5000, Hpet)];
            Destination.AsSpan().Fill(0xAD);
            Scratch.AsSpan().Fill(0xF0F0F0F0F0F0F0F0);
        }

        internal AcpiBootstrapCopyResult Copy() => Bootstrap.Copy(Rsdp, RsdpAddress, Root, RootAddress, Scratch, Sources, Destination);
        internal int Total => Rsdp.Length + Root.Length + Mcfg.Length + Madt.Length + Hpet.Length;
    }
}
