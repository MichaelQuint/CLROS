using System.Buffers.Binary;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxSignalProofFixture;

internal static unsafe class LinuxSignalNestedProofFixtureTests
{
    [HostTest("m16-009 nested alternate stack geometry admits three complete frames and rejects the fourth below its base")]
    private static void Geometry()
    {
        ulong[] frames = [0x406c38, 0x4067b8, 0x406338, 0x405eb8];
        ulong[] fp = [0x406e00, 0x406980, 0x406500, 0x406080];
        for (uint depth = 1; depth <= 4; depth++)
        {
            ulong top = depth == 1 ? 0x407000UL : frames[depth - 2] - 128;
            HostCheck.Equal(fp[depth - 1], (top - 512) & ~63UL);
            HostCheck.Equal(frames[depth - 1], F.NestedHandlerRsp(depth));
            HostCheck.True(fp[depth - 1] + 512 <= top && frames[depth - 1] + 440 <= fp[depth - 1]);
        }
        HostCheck.True(frames.Take(3).All(rsp => rsp >= 0x406000) && frames[3] < 0x406000 && LinuxSignalServices.MaximumNesting == 4);
        foreach (string variant in F.NestedVariants)
        {
            byte[] elf = F.CreateElf(variant);
            HostCheck.True(U64(elf, 0x1100) == 0x400600 && U64(elf, 0x1108) == 0x4c000004 &&
                U64(elf, 0x1110) == 0x400e00 && U64(elf, 0x1118) == 0x1000);
            HostCheck.True(U64(elf, 0x1120) == 0x406000 && U64(elf, 0x1130) == 4096);
            for (int i = 0; i < 3; i++)
                HostCheck.Equal(frames[i], U64(elf, 0x1280 + i * 8));
        }
    }

    [HostTest("m16-009 nested inventories flatten repeated handler and restorer sites in actual inner to outer order")]
    private static void InvocationOrder()
    {
        uint[] normal = [39, 186, 158, 13, 131, 14, 234, 186, 14, 131, 14, 234, 186, 14, 131, 14, 14, 131, 1, 15, 14, 131, 1, 15, 14, 131, 1, 231];
        uint[] negative = [39, 186, 158, 13, 131, 14, 234, 186, 14, 131, 14, 234, 186, 14, 131, 14, 234, 186, 14, 131, 14, 234];
        uint[] depths = [0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 0, 0, 0, 0];
        foreach (string variant in F.NestedVariants)
        {
            bool exhaustion = variant == "AlternateStackExhaustion";
            byte[] elf = F.CreateElf(variant);
            var calls = F.ExpectedNestedInvocations(variant);
            HostCheck.True(calls.Select(c => c.Number).SequenceEqual(exhaustion ? negative : normal));
            HostCheck.True(F.ExpectedInvocations(variant).Select(c => (c.Number, c.ReturnRip, c.Handler))
                .SequenceEqual(calls.Select(c => (c.Number, c.ReturnRip, c.Depth != 0))));
            for (int i = 0; i < calls.Count; i++)
            {
                var call = calls[i];
                uint depth = exhaustion ? i < 7 ? 0U : (uint)((i - 7) / 5 + 1) : depths[i];
                HostCheck.Equal(depth, call.Depth);
                HostCheck.Equal(call.Number == 15, call.Restorer);
                int end = checked((int)(call.ReturnRip - 0x400000));
                HostCheck.True(elf.AsSpan(end - 2, 2).SequenceEqual(new byte[] { 15, 5 }));
                bool dynamicMask = elf.AsSpan(end - 7, 7).SequenceEqual(new byte[] { 0x4a, 0x8d, 0x74, 0xe6, 0xf8, 15, 5 });
                int length = call.Number == 15 ? 7 : call.Number == 234 || dynamicMask ? 52 : 40;
                HostCheck.True(elf[end - length] == 0xb8 && U32(elf, end - length + 1) == call.Number);
                if (dynamicMask)
                {
                    HostCheck.True(call.Number == 14 && depth != 0 && elf.AsSpan(end - 14, 3).SequenceEqual(new byte[] { 0x48, 0x8d, 0x35 }));
                    HostCheck.Equal(0x1260, end - 7 + I32(elf, end - 11));
                }
                if (call.Number == 234)
                {
                    HostCheck.True(elf.AsSpan(end - 14, 2).SequenceEqual(new byte[] { 0x8b, 0x3d }) && elf.AsSpan(end - 8, 2).SequenceEqual(new byte[] { 0x8b, 0x35 }));
                    HostCheck.Equal(0x1160, end - 8 + I32(elf, end - 12));
                    HostCheck.Equal(0x1160, end - 2 + I32(elf, end - 6));
                }
            }
            HostCheck.Equal(exhaustion ? 139U : 42U, F.ExpectedExitCode(variant));
            HostCheck.Equal(exhaustion ? 0 : 2, calls.Count(c => c.Restorer));
            if (!exhaustion)
                HostCheck.Equal(calls[19].ReturnRip, calls[23].ReturnRip);
        }
    }

    [HostTest("m16-009 nested instructions bind actual context pointers saved registers masks and bounded recursive branches")]
    private static void ContextAndRestoration()
    {
        foreach (string variant in F.NestedVariants)
        {
            bool exhaustion = variant == "AlternateStackExhaustion";
            byte[] elf = F.CreateElf(variant);
            var calls = F.ExpectedNestedInvocations(variant);
            HostCheck.True(U64(elf, 0x1140) == 0x800 && U64(elf, 0x1240) == 0x1800 &&
                U64(elf, 0x1248) == 0x3800 && U64(elf, 0x1250) == 0x7800);
            HostCheck.True(U64(elf, 0x1260) == 0x3800 && U64(elf, 0x1268) == 0x7800 && U64(elf, 0x1270) == 0xf800);
            HostCheck.True(U64(elf, 0x12a0) == 0x405000 && U64(elf, 0x12a8) == 0x406c38 && U64(elf, 0x12b0) == 0x4067b8);
            HostCheck.Equal(calls[6].ReturnRip, U64(elf, 0x12c0));
            HostCheck.Equal(calls[11].ReturnRip, U64(elf, 0x12c8));
            HostCheck.Equal(calls[11].ReturnRip, U64(elf, 0x12d0));
            HostCheck.True(U64(elf, 0x12e0) == 0x12345678 && U64(elf, 0x12e8) == 0x23456789);
            ReadOnlySpan<byte> handler = elf.AsSpan(0x600, 0x600);
            HostCheck.True(handler.IndexOf(new byte[] { 0x49, 0x89, 0xd5, 0x49, 0x89, 0xe6 }) >= 0 &&
                handler.IndexOf(new byte[] { 0x49, 0x8d, 0x46, 8, 0x48, 0x39, 0xd0 }) >= 0 &&
                handler.IndexOf(new byte[] { 0x49, 0x8d, 0x86, 0x38, 1, 0, 0, 0x48, 0x39, 0xf0 }) >= 0);
            foreach (uint offset in new uint[] { 0, 16, 24, 32, 72, 80, 88, 144, 160, 168, 296 })
            {
                byte[] load = [0x49, 0x8b, 0x85, 0, 0, 0, 0];
                BinaryPrimitives.WriteUInt32LittleEndian(load.AsSpan(3), offset);
                HostCheck.True(handler.IndexOf(load) >= 0);
            }
            HostCheck.True(handler.IndexOf(new byte[] { 0x4d, 0x89, 0x9d, 0x90, 0, 0, 0 }) >= 0);
            int choose = handler.IndexOf(new byte[] { 0x41, 0x83, 0xfc, 2, 15, 0x84 });
            HostCheck.Equal(!exhaustion, choose >= 0);
            if (choose >= 0)
                HostCheck.Equal(0xc00, 0x600 + choose + 10 + I32(elf, 0x600 + choose + 6));
            else
            {
                int afterSend = checked((int)(calls[11].ReturnRip - 0x400000));
                HostCheck.True(elf[afterSend] == 0xe9 && afterSend + 5 + I32(elf, afterSend + 1) == 0xf00);
            }
            HostCheck.True(elf.AsSpan(0xe00, 9).SequenceEqual(new byte[] { 0xb8, 15, 0, 0, 0, 15, 5, 15, 11 }));
        }
    }

    [HostTest("m16-009 nested packages retain seven page loader geometry and canonical immutable selection")]
    private static void Packages()
    {
        foreach (string variant in F.NestedVariants)
        {
            byte[] package = F.CreatePackage(variant);
            var view = LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded && view.PayloadBytes == 0x7000 && view.Manifest.Threads == 2);
            fixed (byte* bytes = package)
                HostCheck.Equal(LinuxElfLoadStatus.Ready, LinuxElfLoader.Validate(bytes + view.PayloadOffset, view.PayloadBytes,
                    view.Manifest.MemoryBytes, view.Manifest.StackBytes, out _));
            HostCheck.Equal(variant, F.PackageVariant(package));
            HostCheck.True(package.AsSpan().SequenceEqual(F.CreatePackage(variant)));
            byte[] elf = F.CreateElf(variant);
            elf[0x1200] = 255;
            HostCheck.True(F.CreateElf(variant)[0x1200] == 0);
        }
        HostCheck.True(!F.IsNestedVariant("nestedalternatestack") && !F.IsNestedVariant("Activation"));
    }

    private static uint U32(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static int I32(byte[] bytes, int at) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
    private static ulong U64(byte[] bytes, int at) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(at));
}
