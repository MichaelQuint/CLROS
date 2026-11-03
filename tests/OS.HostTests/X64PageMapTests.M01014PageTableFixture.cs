using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Memory;

internal sealed unsafe class M01014PageTableFixture : IDisposable
{
    internal X64PageTableImagePage[] Pages
    {
        get;
    }
    internal ulong[] Entries
    {
        get;
    }
    internal X64PageTableEncodingPreconditions Context
    {
        get;
    }
    internal PhysicalAddress Root
    {
        get;
    }
    internal VirtualAddress Target
    {
        get;
    }

    internal M01014PageTableFixture(ulong target = 0x0040_0000, ulong tablePhysicalStart = 0x0010_0000)
    {
        Context = M01014TestData.Context();
        Root = M01014TestData.Physical(tablePhysicalStart);
        Target = M01014TestData.Virtual(target);
        PhysicalAddress pdpt = M01014TestData.Physical(tablePhysicalStart + 0x1000);
        PhysicalAddress pd = M01014TestData.Physical(tablePhysicalStart + 0x2000);
        PhysicalAddress pt = M01014TestData.Physical(tablePhysicalStart + 0x3000);
        Pages = new X64PageTableImagePage[4];
        X64PageTableImagePage.TryCreate(Root, 0, out Pages[0]);
        X64PageTableImagePage.TryCreate(pdpt, 512, out Pages[1]);
        X64PageTableImagePage.TryCreate(pd, 1024, out Pages[2]);
        X64PageTableImagePage.TryCreate(pt, 1536, out Pages[3]);
        Entries = new ulong[2048];
        ulong rawTarget = M01014TestData.Raw(Target);
        uint pml4Index = (uint)((rawTarget >> 39) & 0x1ff);
        uint pdptIndex = (uint)((rawTarget >> 30) & 0x1ff);
        uint pdIndex = (uint)((rawTarget >> 21) & 0x1ff);
        Entries[pml4Index] = M01014TestData.EncodeTablePointer(X64PageTableLevel.Pml4e, pdpt, Context);
        Entries[512 + pdptIndex] = M01014TestData.EncodeTablePointer(X64PageTableLevel.Pdpte, pd, Context);
        Entries[1024 + pdIndex] = M01014TestData.EncodeTablePointer(X64PageTableLevel.Pde, pt, Context);
    }

    internal X64PageMapResult Apply(X64PageMapRequest request, X64EssentialMappingRange[]? essential = null)
    {
        essential ??= [];
        fixed (X64PageTableImagePage* pages = Pages)
        fixed (ulong* entries = Entries)
        fixed (X64EssentialMappingRange* ranges = essential)
        {
            return X64PageMap.Apply(request, pages, (ulong)Pages.Length, (ulong)Pages.Length, entries, (ulong)Entries.Length, (ulong)Entries.Length, ranges, (ulong)essential.Length, (ulong)essential.Length);
        }
    }

    public void Dispose()
    {
    }
}
