using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal sealed unsafe class M01015PageZeroFixture : IDisposable
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

    internal M01015PageZeroFixture()
    {
        Context = M01015TestData.Context();
        Root = M01015TestData.Physical(0x0010_0000);
        Pages = new X64PageTableImagePage[4];
        Entries = new ulong[2048];
        for (int index = 0; index < Pages.Length; index++)
        {
            X64PageMapStatus status = X64PageTableImagePage.TryCreate(M01015TestData.Physical(0x0010_0000 + (ulong)index * 0x1000), (ulong)index * 512, out Pages[index]);
            if (status != X64PageMapStatus.Success)
            {
                throw new InvalidOperationException("Page fixture invalid: " + status);
            }
        }
    }

    internal void LinkThrough(int pointerCount)
    {
        Array.Clear(Entries);
        if (pointerCount >= 1)
        {
            Entries[0] = M01015TestData.EncodeTablePointer(X64PageTableLevel.Pml4e, Pages[1].PhysicalStart, Context);
        }

        if (pointerCount >= 2)
        {
            Entries[512] = M01015TestData.EncodeTablePointer(X64PageTableLevel.Pdpte, Pages[2].PhysicalStart, Context);
        }

        if (pointerCount >= 3)
        {
            Entries[1024] = M01015TestData.EncodeTablePointer(X64PageTableLevel.Pde, Pages[3].PhysicalStart, Context);
        }
    }

    internal ulong EncodeTablePointer(X64PageTableLevel level, ulong physical)
    {
        return M01015TestData.EncodeTablePointer(level, M01015TestData.Physical(physical), Context);
    }

    internal ulong EncodeLeaf(X64PageTableLevel level, ulong physical)
    {
        X64PageTableEntryResult result = X64PageTableEntry.TryEncode(new X64PageTableEntryRequest(level, X64PageTableEntryKind.Leaf, M01015TestData.Physical(physical), PageProtection.Read, PageCachePolicy.WriteBack), Context);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Leaf fixture invalid: " + result.Status);
        }

        return result.Entry.RawValue;
    }

    internal PageZeroMappingPolicyResult Audit(PhysicalAddress root = default)
    {
        if (!root.IsValid)
            root = Root;
        fixed (X64PageTableImagePage* pages = Pages)
        fixed (ulong* entries = Entries)
        {
            return PageZeroMappingPolicy.AuditTableImage(root, Context, pages, (ulong)Pages.Length, (ulong)Pages.Length, entries, (ulong)Entries.Length, (ulong)Entries.Length);
        }
    }

    internal X64PageMapResult Apply(X64PageMapRequest request)
    {
        fixed (X64PageTableImagePage* pages = Pages)
        fixed (ulong* entries = Entries)
        {
            return X64PageMap.Apply(request, pages, (ulong)Pages.Length, (ulong)Pages.Length, entries, (ulong)Entries.Length, (ulong)Entries.Length, null, 0, 0);
        }
    }

    public void Dispose()
    {
    }
}
