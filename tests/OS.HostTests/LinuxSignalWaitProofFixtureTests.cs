using System.Buffers.Binary;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxSignalProofFixture;

internal static unsafe class LinuxSignalWaitProofFixtureTests
{
    [HostTest("m16-009 actual handler output writes preserve emitted pipe descriptors TIDs and futex controls")]
    private static void OutputOwnership()
    {
        foreach(string variant in new[]{"SignalPipeEintr","SignalPipeRestart","SignalPipePartial"})
        {
            byte[] elf=F.CreateElf(variant);var calls=F.ExpectedWaitInvocations(variant);
            int Start(F.WaitInvocation call)=>checked((int)(call.ReturnRip-0x400000))-40;
            int pipe=Start(calls.Single(c=>c.Number==293));
            HostCheck.Equal((byte)0xbf,elf[pipe+5]);
            int pair=checked((int)(U32(elf,pipe+6)-0x400000));
            BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(pair),3);
            BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(pair+4),4);
            BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(0x11b8),17);
            BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(0x11bc),19);
            BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(0x11d0),23);
            foreach(var call in calls.Where(c=>c.Number is 14 or 131))
            {
                int at=Start(call);int output;
                if(call.Number==131)
                {
                    HostCheck.Equal((byte)0xbe,elf[at+10]);uint address=U32(elf,at+11);if(address==0)continue;
                    output=checked((int)(address-0x400000));
                    BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(output),0x406000);
                    BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(output+8),call.Handler?1UL:0);
                    BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(output+16),4096);
                }
                else
                {
                    HostCheck.Equal((byte)0xba,elf[at+15]);uint address=U32(elf,at+16);if(address==0)continue;
                    output=checked((int)(address-0x400000));
                    BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(output),call.Handler?512UL:0);
                }
                HostCheck.Equal(3U,U32(elf,pair));HostCheck.Equal(4U,U32(elf,pair+4));
                HostCheck.Equal(17U,U32(elf,0x11b8));HostCheck.Equal(19U,U32(elf,0x11bc));HostCheck.Equal(23U,U32(elf,0x11d0));
            }
            foreach(var call in calls)
            {
                int end=checked((int)(call.ReturnRip-0x400000));
                if(call.Number is 0 or 1 && elf.AsSpan(end-8,2).SequenceEqual(new byte[]{0x8b,0x3d}))
                    HostCheck.Equal(pair+(call.Number==0?0:4),end-2+I32(elf,end-6));
            }
        }
    }

    [HostTest("m16-009 interrupted wait packages map disjoint payload read TLS and alternate stack pages")]
    private static void Packages()
    {
        foreach (string variant in F.WaitVariants)
        {
            byte[] elf = F.CreateElf(variant), package = F.CreatePackage(variant);
            HostCheck.True(elf.Length == 40960 && U64(elf, 152) == 36864 && U64(elf, 160) == 36864);
            HostCheck.True(elf.AsSpan(0x7000, 4100).IndexOfAnyExcept((byte)0x5a) < 0 && elf.AsSpan(0x9000, 4096).IndexOfAnyExcept((byte)0) < 0);
            HostCheck.True(U64(elf, 0x2000) == 0x13579bdf && U64(elf, 0x3000) == 0x2468ace0 && U64(elf, 0x1120) == 0x406000 && U64(elf, 0x1130) == 4096);
            var view = LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded && view.PayloadBytes == 40960 && view.Manifest.Threads == 2 && view.Manifest.Descriptors == 16);
            fixed (byte* p = package)
            {
                HostCheck.Equal(LinuxElfLoadStatus.Ready, LinuxElfLoader.Validate(p + view.PayloadOffset, view.PayloadBytes,
                    view.Manifest.MemoryBytes, view.Manifest.StackBytes, out var plan));
                HostCheck.True(plan.ImageBytes == 40960 && plan.LoadCount == 2 && plan.Entry == 0x400200);
                HostCheck.True(LinuxUserInterruptPolicy.TryRead(p + view.ManifestOffset, view.ManifestBytes, view.Manifest, out bool irq) && !irq);
            }
            using MemoryStream input = new(package); F.RequirePackage(input); HostCheck.Equal(0L, input.Position);
            HostCheck.Equal(variant, F.PackageVariant(package));
            HostCheck.Equal(42U, F.ExpectedExitCode(variant));
            HostCheck.True(package.AsSpan().SequenceEqual(F.CreatePackage(variant)));
        }
    }

    [HostTest("m16-009 wait byte inventories bind syscall numbers descriptor loads child order and actual replay sites")]
    private static void Instructions()
    {
        foreach (string variant in F.WaitVariants)
        {
            bool futex = variant.StartsWith("SignalFutex", StringComparison.Ordinal), partial = variant == "SignalPipePartial";
            bool restart = variant.EndsWith("Restart", StringComparison.Ordinal);
            byte[] elf = F.CreateElf(variant); var calls = F.ExpectedWaitInvocations(variant);
            uint[] pre = futex ? [39,186,158,13,131,56,202] : [39,186,158,13,131,293,56,partial ? 1U : 0U];
            uint[] child = futex ? [39,186,234,202,60] : partial ? [39,186,234,60] : [39,186,234,1,60];
            uint[] handler = [186,14,131,1,15];
            List<uint> post = restart ? [futex ? 202U : 0U] : !futex && !partial ? [0] : [];
            if (partial) post.AddRange([0,1,0]);
            post.AddRange([14,131]); if (!futex) post.AddRange([3,3]); post.AddRange([1,231]);
            HostCheck.True(calls.Select(c => c.Number).SequenceEqual(pre.Concat(child).Concat(handler).Concat(post)));
            HostCheck.True(F.ExpectedInvocations(variant).Select(c => (c.Number,c.ReturnRip,c.Handler))
                .SequenceEqual(calls.Select(c => (c.Number,c.ReturnRip,c.Handler))));
            for (int i = 0; i < calls.Count; i++)
            {
                var call = calls[i]; int end = checked((int)(call.ReturnRip - 0x400000));
                bool fd = elf.AsSpan(end - 8, 2).SequenceEqual(new byte[] {0x8b,0x3d});
                int start = end - (call.Number == 15 ? 7 : fd ? 46 : 40);
                HostCheck.True(elf[start] == 0xb8 && U32(elf,start + 1) == call.Number && elf.AsSpan(end - 2,2).SequenceEqual(new byte[] {15,5}));
                if (fd)
                {
                    HostCheck.True(call.Number is 0 or 1);
                    HostCheck.Equal(call.Number == 0 ? 0x11c0 : 0x11c4, end - 2 + I32(elf,end - 6));
                }
                HostCheck.Equal(i >= pre.Length && i < pre.Length + child.Length, call.Child);
                HostCheck.Equal(i >= pre.Length + child.Length && i < pre.Length + child.Length + handler.Length, call.Handler);
            }
            var parked = calls[pre.Length - 1];
            HostCheck.Equal(restart ? 2 : 1, calls.Count(c => c.ReturnRip == parked.ReturnRip));
            if (restart) HostCheck.Equal(parked.ReturnRip, calls[pre.Length + child.Length + handler.Length].ReturnRip);
        }
    }

    [HostTest("m16-009 handlers inspect real interrupted or rewound context and preserve positive partial progress")]
    private static void SavedContext()
    {
        foreach (string variant in F.WaitVariants)
        {
            bool futex = variant.StartsWith("SignalFutex", StringComparison.Ordinal), partial = variant == "SignalPipePartial";
            bool restart = variant.EndsWith("Restart", StringComparison.Ordinal);
            byte[] elf = F.CreateElf(variant); var calls = F.ExpectedWaitInvocations(variant);
            var parked = calls.First(c => !c.Child && !c.Handler && (c.Number == 202 || c.Number == 0 || c.Number == 1));
            HostCheck.True(U64(elf,0x1100) == 0x400a00 && U64(elf,0x1110) == 0x400e00);
            HostCheck.Equal(restart || partial ? 0x1c000004UL : 0x0c000004UL, U64(elf,0x1108));
            ReadOnlySpan<byte> body = elf.AsSpan(0xa00,0x400);
            (uint Field,long Value)[] saved = [(144,partial ? 4096 : restart ? futex ? 202 : 0 : -4),
                (168,(long)(restart ? parked.ReturnRip - 2 : parked.ReturnRip)),(160,0x405000),
                (104,futex ? 0x4011d0 : partial ? 4 : 3),(112,futex ? 128 : partial ? 0x407000 : 0x409000),(136,futex ? 0 : partial ? 4100 : 1)];
            foreach (var field in saved)
            {
                byte[] check = [0x48,0x8b,0x82,0,0,0,0,0x48,0x3d,0,0,0,0];
                BinaryPrimitives.WriteUInt32LittleEndian(check.AsSpan(3), field.Field);
                BinaryPrimitives.WriteInt32LittleEndian(check.AsSpan(9), checked((int)field.Value));
                HostCheck.True(body.IndexOf(check) >= 0);
            }
            HostCheck.True(body.IndexOf(new byte[] {0xbb,0x10,0x32,0x54,0x76}) >= 0 &&
                elf.AsSpan(0x200,0x600).IndexOf(new byte[] {0x89,0xd8,0x48,0x3d,0xdf,0x9b,0x57,0x13}) >= 0);
            HostCheck.Equal(partial, elf.AsSpan(0x200,0x600).IndexOf(new byte[] {0xf3,0xa6,15,0x85}) >= 0);
        }
    }

    private static uint U32(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static int I32(byte[] bytes, int at) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
    private static ulong U64(byte[] bytes, int at) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(at));
}
