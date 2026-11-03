using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;

internal static class FilteredCatchThunkGeneratorTests
{
    private static void Need(bool value, string why)
    {
        if (!value)
            throw new InvalidOperationException(why);
    }
    private static T Read<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        HostCheck.Run("runtime.filtered-catch-thunks.return-and-rejections", () =>
        {
            string root = HostToolProcess.FindRepositoryRoot();
            string folder = Path.Combine(root, "artifacts", "filtered-catch-thunks", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            _ = ExceptionHandlingThunkGeneratorTests.Run(root, folder, "build",
                ["build", "tools/OS.X64TextSerializerGenerator", "--nologo", "-m:1"]);
            var assembly = Assembly.LoadFile(Path.Combine(root, "tools/OS.X64TextSerializerGenerator/bin/Debug/net11.0/OS.X64TextSerializerGenerator.dll"));
            var generator = assembly.GetType("OS.X64TextSerializerGenerator.ExceptionHandlingThunkGenerator", true)!;
            object definition = generator.GetMethod("DefineFilteredCatch")!.Invoke(null, null)!;
            object section = Read<Array>(definition, "Sections").GetValue(0)!;
            byte[] transfer = Read<byte[]>(section, "RawData");
            object legacy = generator.GetMethod("Define", Type.EmptyTypes)!.Invoke(null, null)!;
            Need(transfer.SequenceEqual(Read<byte[]>(Read<Array>(legacy, "Sections").GetValue(0)!, "RawData")), "original transfer bytes retained");
            string[] names = ["RhpThrowEx", "CLROS_Filter_Select", "CLROS_Filter_ValidateContinuation", "CLROS_Filter_Clauses", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FilterCatcher"];
            Need(Read<Array>(definition, "Symbols").Cast<object>().Select(s => Read<string>(s, "Name")).SequenceEqual(names), "separate filtered-catch symbols");
            uint[] offsets = [330, 811, 1027], targets = [1, 2, 4];
            object[] relocs = Read<Array>(section, "Relocations").Cast<object>().ToArray();
            Need(relocs.Length == 3, "exact callback/table relocations");
            for (int i = 0; i < relocs.Length; i++)
                Need(Read<uint>(relocs[i], "Offset") == offsets[i] && Read<uint>(relocs[i], "SymbolIndex") == targets[i] && Read<ushort>(relocs[i], "Type") == 4, "REL32 " + i);
            for (int failure = 0; failure <= 6; failure++)
                ExceptionHandlingThunkGeneratorTests.VerifyTransfer(transfer, failure);
            Type wrapper = assembly.GetType("OS.X64TextSerializerGenerator.ExceptionFilterThunkGenerator", true)!;
            object returning = wrapper.GetMethod("Define")!.Invoke(null, null)!;
            object repeated = wrapper.GetMethod("Define")!.Invoke(null, null)!;
            object returningSection = Read<Array>(returning, "Sections").GetValue(0)!;
            byte[] code = Read<byte[]>(returningSection, "RawData");
            Need(Read<ushort>(returning, "Machine") == 0x8664 && Read<uint>(returning, "TimeDateStamp") == 0 && Read<Array>(returning, "Sections").Length == 1 &&
                Read<string>(returningSection, "Name") == ".text" && Read<uint>(returningSection, "Characteristics") == 0x60500020 && Read<Array>(returningSection, "Relocations").Length == 0, "closed returning wrapper");
            object[] symbols = Read<Array>(returning, "Symbols").Cast<object>().ToArray();
            Need(symbols.Length == 1 && Read<string>(symbols[0], "Name") == "CLROS_Filter_Invoke" && Read<uint>(symbols[0], "Value") == 0 && Read<short>(symbols[0], "SectionNumber") == 1 && Read<ushort>(symbols[0], "Type") == 0x20 && Read<byte>(symbols[0], "StorageClass") == 2, "ordinary returning entry");
            Need(code.SequenceEqual(Read<byte[]>(Read<Array>(repeated, "Sections").GetValue(0)!, "RawData")), "deterministic wrapper");
            foreach (int result in new[] { 0, 1, -1, int.MinValue, int.MaxValue })
                for (int failure = 0; failure <= 4; failure++)
                    VerifyReturn(code, result, failure);
            // Independent execution expectations reject saving from the parent's
            // context in place of the caller and returning a substituted result.
            byte[] changed = [.. code];
            int resultLoad = Find(changed, [0x48, 0x8B, 0x84, 0x24, 0x40, 0x01, 0, 0]);
            changed[resultLoad + 4] = 0x38;
            bool rejected = false;
            try
            {
                VerifyReturn(changed, -1, 0);
            }
            catch (InvalidOperationException) { rejected = true; }
            Need(rejected, "substituted return slot rejected");
            string tool = assembly.Location;
            foreach (var pair in new[] { ("--filtered-catch", OS.Tooling.EfiContracts.FilteredCatchObjectBytes, OS.Tooling.EfiContracts.FilteredCatchObjectSha256),
                ("--filter-invoke", OS.Tooling.EfiContracts.FilterInvokeObjectBytes, OS.Tooling.EfiContracts.FilterInvokeObjectSha256) })
            {
                string file = pair.Item1[2..] + ".obj";
                string[] command = [tool, pair.Item1, "--output-root", folder, "--output-file", file];
                _ = ExceptionHandlingThunkGeneratorTests.Run(root, folder, file + "-emit", command);
                byte[] obj = File.ReadAllBytes(Path.Combine(folder, file));
                Need(obj.Length == pair.Item2 && Convert.ToHexString(SHA256.HashData(obj)).Equals(pair.Item3, StringComparison.OrdinalIgnoreCase), "complete serialized object identity");
                byte[] modeled = pair.Item1 == "--filter-invoke" ? code : transfer;
                Need(obj.AsSpan(60, modeled.Length).SequenceEqual(modeled), "serialized text is modeled text");
                _ = ExceptionHandlingThunkGeneratorTests.Run(root, folder, file + "-repeat", command);
                Need(File.ReadAllBytes(Path.Combine(folder, file)).SequenceEqual(obj), "CLI deterministic repeat");
                int attempt = 0;
                foreach (string competing in new[] { "--filtered-catch", "--filter-invoke", "--constructor-failure", "--frame-dispatch", "--nested-finally-catch", "--rethrow-inner", "--rethrow-outer", "--single-finally-catch", "--simple-throw-catch", "--interface-dispatch", "--checked-reference-barriers" })
                    foreach (bool reverse in new[] { false, true })
                    {
                        string output = ExceptionHandlingThunkGeneratorTests.Run(root, folder, file + "-conflict-" + attempt++,
                            [tool, reverse ? competing : pair.Item1, reverse ? pair.Item1 : competing, "--output-root", folder, "--output-file", file], 2);
                        Need(output.Contains(pair.Item1 == competing ? "argument_duplicate" : "argument_mode_conflict", StringComparison.Ordinal) &&
                            File.ReadAllBytes(Path.Combine(folder, file)).SequenceEqual(obj), "mode rejection preserves output");
                    }
            }
            File.WriteAllBytes(Path.Combine(folder, "filter-return.text.bin"), code);
            File.WriteAllLines(Path.Combine(folder, "definition.txt"), [$"ReturnTextBytes={code.Length}", $"ReturnTextSha256={Convert.ToHexString(SHA256.HashData(code))}", "ReturnFrameBytes=328 TransferFrameBytes=840"]);
        });
    }

    private static int Find(byte[] code, byte[] pattern)
    {
        for (int i = 0; i <= code.Length - pattern.Length; i++)
            if (code.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                return i;
        throw new InvalidOperationException("independent instruction pattern absent");
    }

    // Finite instruction interpreter. The selector and original method have
    // distinct GP/full-XMM values. The filter clobbers both, its home area and
    // FP controls; only the actual EAX result may survive ordinary return.
    private static void VerifyReturn(byte[] code, int result, int failure)
    {
        const ulong entry = 6008, frame = entry - 328, parent = 1000, parentSp = 7600, exception = 0x700000, filter = 0x180004000, returnRip = 0x180009000;
        int[] nv = [3, 5, 6, 7, 12, 13, 14, 15];
        ulong[] g = Enumerable.Range(0, 16).Select(i => 0xCA110000UL + (ulong)i).ToArray();
        byte[][] xmm = Enumerable.Range(0, 16).Select(r => Enumerable.Range(0, 16).Select(i => (byte)(r * 11 + i)).ToArray()).ToArray();
        ulong[] caller = [.. g];
        byte[][] callerXmm = xmm.Select(x => x.ToArray()).ToArray();
        byte[] memory = new byte[8192];
        void Put(ulong at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(memory.AsSpan((int)at, 8), value);
        ulong Get(ulong at) => BinaryPrimitives.ReadUInt64LittleEndian(memory.AsSpan((int)at, 8));
        Put(entry, returnRip);
        Put(parent, 0x180005001);
        Put(parent + 8, parentSp);
        for (int i = 0; i < nv.Length; i++)
            Put(parent + 16 + (ulong)i * 8, 0xFACE0000UL + (ulong)nv[i]);
        for (int r = 6; r < 16; r++)
            for (int i = 0; i < 16; i++)
                memory[(int)parent + 80 + (r - 6) * 16 + i] = (byte)(0x80 + r + i);
        byte[] parentBefore = memory.AsSpan((int)parent, 240).ToArray();
        g[4] = entry;
        g[1] = filter;
        g[2] = exception;
        g[8] = parent;
        ulong flags = failure == 1 ? 0x400UL : 0, cr4 = failure == 2 ? 1UL << 23 : 0;
        uint mxcsr = 0x1F80;
        ushort x87 = 0x37F;
        bool zero = false;
        int ip = 0, calls = 0;
        int I32(int at) => BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(at, 4));
        bool At(params byte[] bytes) => code.AsSpan(ip, bytes.Length).SequenceEqual(bytes);
        for (int step = 0; step < 300; step++)
        {
            if (At(0xEB, 0xFE))
            {
                Need(failure != 0 && calls == (failure >= 3 ? 1 : 0), "reject at correct control boundary");
                return;
            }
            if (code[ip] == 0xC3)
            {
                Need(failure == 0 && calls == 1 && g[4] == entry && Get(g[4]) == returnRip && (uint)g[0] == (uint)result, "genuine result and ordinary return stack");
                foreach (int r in nv)
                    Need(g[r] == caller[r], "caller GP restored " + r);
                for (int r = 6; r < 16; r++)
                    Need(xmm[r].SequenceEqual(callerXmm[r]), "caller full XMM restored " + r);
                Need(mxcsr == 0x1F80 && x87 == 0x37F && memory.AsSpan((int)parent, 240).SequenceEqual(parentBefore), "FP and immutable parent context");
                return;
            }
            if (code[ip] == 0x9C)
            {
                g[4] -= 8;
                Put(g[4], flags);
                ip++;
                continue;
            }
            if (code[ip] == 0x58)
            {
                g[0] = Get(g[4]);
                g[4] += 8;
                ip++;
                continue;
            }
            if (At(0x48, 0x81, 0xEC) || At(0x48, 0x81, 0xC4))
            {
                g[4] = code[ip + 2] == 0xEC ? g[4] - (uint)I32(ip + 3) : g[4] + (uint)I32(ip + 3);
                ip += 7;
                continue;
            }
            if (At(0x48, 0xA9))
            {
                zero = (g[0] & (uint)I32(ip + 2)) == 0;
                ip += 6;
                continue;
            }
            if (At(0x0F, 0x85))
            {
                int next = ip + 6;
                ip = zero ? next : next + I32(ip + 2);
                continue;
            }
            if (At(0x0F, 0x20, 0xE0))
            {
                g[0] = cr4;
                ip += 3;
                continue;
            }
            if (At(0x0F, 0xAE, 0x9C, 0x24) || At(0x0F, 0xAE, 0x94, 0x24))
            {
                int at = (int)g[4] + I32(ip + 4);
                if (code[ip + 2] == 0x9C)
                    BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(at), mxcsr);
                else
                    mxcsr = BinaryPrimitives.ReadUInt32LittleEndian(memory.AsSpan(at));
                ip += 8;
                continue;
            }
            if (At(0xD9, 0xBC, 0x24) || At(0xD9, 0xAC, 0x24))
            {
                int at = (int)g[4] + I32(ip + 3);
                if (code[ip + 1] == 0xBC)
                    BinaryPrimitives.WriteUInt16LittleEndian(memory.AsSpan(at), x87);
                else
                    x87 = BinaryPrimitives.ReadUInt16LittleEndian(memory.AsSpan(at));
                ip += 7;
                continue;
            }
            if (code[ip] == 0xF3)
            {
                int rex = code[ip + 1] is >= 0x40 and <= 0x4F ? code[ip + 1] : 0, p = ip + 1 + (rex == 0 ? 0 : 1);
                Need(code[p] == 0x0F && code[p + 1] is 0x6F or 0x7F, "finite vector vocabulary");
                int r = ((code[p + 2] >> 3) & 7) + ((rex & 4) != 0 ? 8 : 0), extra = (code[p + 2] & 7) == 4 ? 1 : 0;
                ulong at = ((rex & 1) != 0 ? g[11] : g[4]) + (uint)I32(p + 3 + extra);
                if (code[p + 1] == 0x7F)
                    xmm[r].CopyTo(memory, (int)at);
                else
                    xmm[r] = memory.AsSpan((int)at, 16).ToArray();
                ip = p + 7 + extra;
                continue;
            }
            if (code[ip] is 0x48 or 0x49 or 0x4C or 0x4D && code[ip + 1] is 0x89 or 0x8B)
            {
                int rex = code[ip], r = ((code[ip + 2] >> 3) & 7) + ((rex & 4) != 0 ? 8 : 0), extra = (code[ip + 2] & 7) == 4 ? 1 : 0;
                ulong at = ((rex & 1) != 0 ? g[11] : g[4]) + (uint)I32(ip + 3 + extra);
                if (code[ip + 1] == 0x89)
                    Put(at, g[r]);
                else
                    g[r] = Get(at);
                ip += 7 + extra;
                continue;
            }
            if (At(0xFF, 0x94, 0x24))
            {
                Need(calls == 0 && g[4] == frame && (g[4] & 15) == 0 && I32(ip + 3) == 272 && Get(frame + 272) == filter && g[1] == exception && g[2] == parentSp, "filter args, target and shadow alignment");
                foreach (int r in nv)
                    Need(g[r] == 0xFACE0000UL + (ulong)r, "original parent GP installed " + r);
                for (int r = 6; r < 16; r++)
                    Need(xmm[r].SequenceEqual(parentBefore.AsSpan(80 + (r - 6) * 16, 16).ToArray()), "original parent XMM installed " + r);
                memory.AsSpan((int)frame, 32).Fill(0xCC);
                foreach (int r in nv)
                    g[r] = 0xBADF0000UL + (ulong)r;
                for (int r = 6; r < 16; r++)
                    xmm[r] = Enumerable.Repeat((byte)(0xE0 + r), 16).ToArray();
                g[0] = (uint)result;
                mxcsr = 0x5F80;
                x87 = 0x77F;
                flags = failure == 3 ? 0x400UL : 0;
                cr4 = failure == 4 ? 1UL << 23 : 0;
                calls++;
                ip += 7;
                continue;
            }
            throw new InvalidOperationException("unexpected returning opcode " + ip);
        }
        throw new InvalidOperationException("returning wrapper exceeded finite budget");
    }
}
