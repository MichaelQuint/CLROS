using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OS.X64TextSerializerGenerator;

/// <summary>Exact original ILC contributions, never inferred from the next symbol or section tail.</summary>
public static class EhContributionCatalogGenerator
{
    public const uint Magic = 0x43454843; // CHEC, little endian
    public const int MaximumMethods = 32;
    public const string StartSymbol = "CLROS_Eh_CatalogStart";
    public const string EndSymbol = "CLROS_Eh_CatalogEnd";
    public const string DataSymbol = "CLROS_Eh_Contributions";
    public const string DataEndSymbol = "CLROS_Eh_ContributionsEnd";
    private const int MaximumInputBytes = 16 * 1024 * 1024;
    private readonly record struct Symbol(string Name, uint Value, short Section, ushort Type, byte Class);
    private readonly record struct Node(string Name, uint Length, string Hash);
    private readonly record struct Contribution(Node Code, Node Eh, Symbol Root, Symbol Table);

    public static int Run(string[] args)
    {
        const string test = "m04.021.eh-catalog.generate";
        Console.WriteLine("TEST_START " + test);
        try
        {
            if (args.Length != 7 || args[1] != "--object" || args[3] != "--map" || args[5] != "--output")
                throw new InvalidDataException("arguments");
            foreach (string input in new[]{args[2], args[4]})
                if (new FileInfo(input).Length is <= 0 or > MaximumInputBytes) throw new InvalidDataException("input_capacity");
            string output = Path.GetFullPath(args[6]);
            if (output.Equals(Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase) ||
                output.Equals(Path.GetFullPath(args[4]), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("output_alias");
            byte[] obj = File.ReadAllBytes(args[2]), mapRaw = File.ReadAllBytes(args[4]); string map = File.ReadAllText(args[4]);
            byte[] generated = Generate(obj, map);
            // The producer never changes its inputs. The integration/native verifier retain these hashes.
            if (!SHA256.HashData(obj).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(args[2]))) ||
                !SHA256.HashData(mapRaw).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(args[4])))) throw new InvalidDataException("input_changed");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            string temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, generated); File.Move(temporary, output, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            Console.WriteLine($"M04_EH_CATALOG_READY bytes={generated.Length} sha256={Convert.ToHexString(SHA256.HashData(generated))} inputSha256={Convert.ToHexString(SHA256.HashData(obj))} mapSha256={Convert.ToHexString(SHA256.HashData(mapRaw))}");
            Console.WriteLine("TEST_PASS " + test); return 0;
        }
        catch (Exception e) when (e is InvalidDataException or XmlException or IOException or UnauthorizedAccessException or OverflowException or FormatException)
        { Console.WriteLine("TEST_FAIL " + test + " " + (e is InvalidDataException ? e.Message : e.GetType().Name)); return 1; }
    }

    public static byte[] Generate(byte[] obj, string map)
    {
        if (obj.Length is < 20 or > MaximumInputBytes || map.Length is <= 0 or > MaximumInputBytes) Fail("input_capacity");
        if (U16(obj, 0) != 0x8664 || U16(obj, 16) != 0 || U16(obj, 18) != 0 || U32(obj, 4) != 0) Fail("coff_header");
        int sections = U16(obj, 2); if (sections == 0) Fail("coff_sections");
        uint symbolsAt = U32(obj, 8), symbolCount = U32(obj, 12);
        SpanRange(obj, 20, checked((uint)sections * 40));
        if (symbolsAt < 20U + (uint)sections * 40 || symbolCount == 0 || symbolCount > 100000) Fail("coff_symbols");
        SpanRange(obj, symbolsAt, checked(symbolCount * 18 + 4));
        uint stringsAt = checked(symbolsAt + symbolCount * 18), stringsBytes = U32(obj, stringsAt);
        if (stringsBytes < 4) Fail("coff_strings"); SpanRange(obj, stringsAt, stringsBytes);
        Dictionary<string, Symbol> symbols = new(StringComparer.Ordinal);
        bool[] symbolRecords = new bool[symbolCount];
        for (uint i = 0; i < symbolCount;)
        {
            uint at = symbolsAt + i * 18; string name;
            symbolRecords[i] = true;
            if (U32(obj, at) == 0)
            {
                uint offset = U32(obj, at + 4); if (offset < 4 || offset >= stringsBytes) Fail("symbol_name");
                name = Name(obj, stringsAt + offset, stringsBytes - offset, true);
            }
            else name = Name(obj, at, 8, false);
            short section = unchecked((short)U16(obj, at + 12)); byte aux = obj[at + 17];
            if (i + 1U + aux > symbolCount || section > sections) Fail("symbol_record");
            Symbol symbol = new(name, U32(obj, at + 8), section, U16(obj, at + 14), obj[at + 16]);
            // Section/debug symbols may repeat. External contribution definitions may not.
            if (symbol.Class == 2 && symbol.Section > 0 && !symbols.TryAdd(name, symbol)) Fail("duplicate_symbol");
            i += 1U + aux;
        }
        using var reader = XmlReader.Create(new StringReader(map), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumInputBytes });
        XElement root = XElement.Load(reader); if (root.Name != "ObjectNodes") Fail("map_root");
        Dictionary<string, Node> codes = new(StringComparer.Ordinal); List<Node> eh = [];
        foreach (XElement e in root.Elements())
        {
            if (e.Name != "MethodCode" && e.Name != "MethodExceptionHandlingInfo") continue;
            string name = (string?)e.Attribute("Name") ?? "", hash = (string?)e.Attribute("Hash") ?? "";
            uint length = 0;
            if (name.Length == 0 || hash.Length != 64 || !hash.All(Uri.IsHexDigit) ||
                !uint.TryParse((string?)e.Attribute("Length"), NumberStyles.None, CultureInfo.InvariantCulture, out length) || length == 0) Fail("map_node");
            Node node = new(name, length, hash);
            if (e.Name == "MethodCode") { if (!codes.TryAdd(name, node)) Fail("duplicate_method"); }
            else { if (eh.Any(n => n.Name == name)) Fail("duplicate_eh"); eh.Add(node); }
        }
        if (eh.Count is < 1 or > MaximumMethods) Fail("catalog_count");
        List<Contribution> contributions = [];
        foreach (Node table in eh.OrderBy(n => n.Name, StringComparer.Ordinal))
        {
            Node code = default; Symbol method = default, data = default;
            if (!table.Name.StartsWith("__ehinfo_", StringComparison.Ordinal) ||
                !codes.TryGetValue(table.Name[9..], out code) || !symbols.TryGetValue(code.Name, out method) ||
                !symbols.TryGetValue(table.Name, out data)) Fail("contribution_missing");
            // Pinned ILC emits type-zero external code symbols; section flags and the
            // exact MethodCode hash establish the contribution shape.
            if (method.Type is not (0 or 0x20) || data.Type != 0 || code.Length > MaximumInputBytes || table.Length > 4096) Fail("contribution_shape");
            Verify(obj, method, code, true, symbolRecords); VerifyCodeExtent(obj, method, code.Length, sections, symbolRecords);
            Verify(obj, data, table, false, symbolRecords);
            foreach (Contribution earlier in contributions)
                if (Overlaps(method, code.Length, earlier.Root, earlier.Code.Length) ||
                    Overlaps(data, table.Length, earlier.Table, earlier.Eh.Length)) Fail("contribution_alias");
            // Another map contribution cannot be hidden inside the declared code extent.
            foreach (Node other in codes.Values)
                if (other.Name != code.Name && symbols.TryGetValue(other.Name, out Symbol otherSymbol) &&
                    Overlaps(method, code.Length, otherSymbol, other.Length)) Fail("code_overlap");
            contributions.Add(new(code, table, method, data));
        }
        byte[] payload = new byte[16 + contributions.Count * 16]; W32(payload, 0, Magic); W32(payload, 4, 1);
        W32(payload, 8, (uint)contributions.Count); W32(payload, 12, (uint)payload.Length);
        List<CoffSymbolDefinition> outputSymbols = [new(StartSymbol,0,1,0x20,2), new(EndSymbol,8,1,0x20,2),
            new(DataSymbol,0,2,0,2), new(DataEndSymbol,(uint)payload.Length,2,0,2)];
        List<CoffRelocationDefinition> fixups = [];
        for (int i = 0; i < contributions.Count; i++)
        {
            Contribution c = contributions[i]; uint at = (uint)(16 + i * 16), target = (uint)outputSymbols.Count;
            outputSymbols.Add(new(c.Code.Name,0,0,0x20,2)); outputSymbols.Add(new(c.Eh.Name,0,0,0,2));
            W32(payload, at + 4, c.Code.Length); W32(payload, at + 12, c.Eh.Length);
            fixups.Add(new(at,target,3)); fixups.Add(new(at+8,target+1,3));
        }
        Amd64CoffObjectDefinition definition = new(0x8664,0,
            [new(".text",[0x48,0x8d,0x05,0,0,0,0,0xc3,0x48,0x8d,0x05,0,0,0,0,0xc3],0x60500020,[new(3,2,4),new(11,3,4)]),
             new(".rdata",payload,0x40300040,[..fixups])], [..outputSymbols]);
        byte[] output = new byte[65536];
        if (!Amd64CoffWriter.TryWriteContributionCatalog(in definition, output, out int written, out CoffWriteError error)) Fail("catalog_write_" + error);
        return output.AsSpan(0,written).ToArray();
    }

    private static void Verify(byte[] obj, Symbol symbol, Node node, bool code, bool[] symbolRecords)
    {
        uint section = 20U + (uint)(symbol.Section - 1) * 40;
        uint size = U32(obj, section + 16), raw = U32(obj, section + 20), flags = U32(obj, section + 36);
        if (raw == 0 || symbol.Value > size || node.Length > size - symbol.Value || (flags & 0x80000000) != 0 ||
            (flags & 0x40000000) == 0 || (code ? (flags & 0x20000020) != 0x20000020 : (flags & 0x20000040) != 0x40)) Fail("contribution_section");
        SpanRange(obj, raw, size);
        // ILC's XML hashes precede local code relocation resolution. Do not treat
        // that digest as a COFF code-byte digest. Original pdata joins establish
        // the exact whole-method extent; both complete input digests are retained.
        if (!code && !Convert.ToHexString(SHA256.HashData(obj.AsSpan(checked((int)(raw+symbol.Value)),checked((int)node.Length)))).Equals(node.Hash,StringComparison.OrdinalIgnoreCase)) Fail("contribution_hash");
        uint relocation = U32(obj, section + 24); int count = U16(obj, section + 32);
        if ((flags & 0x01000000) != 0) Fail("relocation_overflow");
        if (count != 0) SpanRange(obj, relocation, (uint)count * 10);
        HashSet<uint> occupied = [];
        uint symbolsAt = U32(obj,8), symbolsCount = U32(obj,12);
        for (int i = 0; i < count; i++)
        {
            uint at = relocation + (uint)i * 10, offset = U32(obj,at), target = U32(obj,at+4); ushort kind = U16(obj,at+8);
            uint width=kind switch {1=>8,2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 11=>4,10=>2,12=>1,_=>0};
            if (target >= symbolsCount || !symbolRecords[target] || width==0 || offset>=size || width>size-offset) Fail("relocation_record");
            if ((ulong)offset+width<=symbol.Value || (ulong)offset>=(ulong)symbol.Value+node.Length) continue;
            if (offset<symbol.Value || width>node.Length-(offset-symbol.Value) || (code ? kind is < 3 or > 9 : kind != 3) ||
                obj[symbolsAt + target * 18 + 16] != 2) Fail("contribution_relocation");
            for (uint b = 0; b < 4; b++) if (!occupied.Add(offset+b)) Fail("relocation_overlap");
        }
    }
    private static void VerifyCodeExtent(byte[] obj, Symbol root, uint length, int sections, bool[] symbolRecords)
    {
        uint symbolsAt=U32(obj,8), symbolsCount=U32(obj,12), first=uint.MaxValue, last=0; int frames=0;
        for(int sectionIndex=0;sectionIndex<sections;sectionIndex++)
        {
            uint section=20U+(uint)sectionIndex*40;
            if (Name(obj,section,8,false)!=".pdata") continue;
            uint size=U32(obj,section+16), raw=U32(obj,section+20), reloc=U32(obj,section+24), flags=U32(obj,section+36);
            int count=U16(obj,section+32);
            if (size==0 || size%12!=0 || (flags&0xE0000040)!=0x40000040 || (flags&0x01000000)!=0 || (uint)count!=size/4) Fail("pdata_shape");
            SpanRange(obj,raw,size); SpanRange(obj,reloc,(uint)count*10);
            for(uint row=0;row<size;row+=12)
            {
                uint fixup=reloc+(row/4)*10;
                for(uint f=0;f<3;f++)
                {
                    uint target=U32(obj,fixup+f*10+4);
                    if(U32(obj,fixup+f*10)!=row+f*4 || U16(obj,fixup+f*10+8)!=3 || target>=symbolsCount || !symbolRecords[target]) Fail("pdata_relocation");
                }
                uint beginSymbol=U32(obj,fixup+4), endSymbol=U32(obj,fixup+14);
                uint beginAt=symbolsAt+beginSymbol*18;
                // Identity is the original section/value pair. Folded aliases are
                // rejected by the contribution overlap check, rather than guessed.
                if(U16(obj,beginAt+12)!=(ushort)root.Section || U32(obj,beginAt+8)!=root.Value) continue;
                if(beginSymbol!=endSymbol || obj[beginAt+16]!=2) Fail("pdata_root_identity");
                uint unwindAt=symbolsAt+U32(obj,fixup+24)*18;
                short unwindSection=unchecked((short)U16(obj,unwindAt+12));
                if(unwindSection<=0 || unwindSection>sections || obj[unwindAt+16] is not (2 or 3)) Fail("pdata_unwind_identity");
                uint unwindHeader=20U+(uint)(unwindSection-1)*40, unwindFlags=U32(obj,unwindHeader+36);
                ulong unwindOffset=(ulong)U32(obj,unwindAt+8)+U32(obj,raw+row+8);
                uint unwindBytes=U32(obj,unwindHeader+16);
                if((unwindFlags&0xE0000040)!=0x40000040 || unwindOffset>unwindBytes || unwindBytes-unwindOffset<4) Fail("pdata_unwind_bounds");
                SpanRange(obj,U32(obj,unwindHeader+20),unwindBytes);
                uint begin=U32(obj,raw+row), end=U32(obj,raw+row+4);
                if(begin>=end || end>length || ++frames>65) Fail("pdata_bounds");
                first=Math.Min(first,begin); last=Math.Max(last,end);
            }
        }
        if(frames==0 || first!=0 || last!=length) Fail("code_extent");
    }
    private static bool Overlaps(Symbol a, uint length, Symbol b, uint other) => a.Section == b.Section && (ulong)a.Value < (ulong)b.Value+other && (ulong)b.Value < (ulong)a.Value+length;
    private static string Name(byte[] obj,uint at,uint capacity,bool requireTerminator)
    {
        SpanRange(obj,at,capacity); uint length=0;
        while(length<capacity && obj[at+length]!=0) { if (obj[at+length] is < 32 or > 126 || length >= 4096) Fail("symbol_name"); length++; }
        if (length==0 || (requireTerminator && length==capacity)) Fail("symbol_name");
        return Encoding.ASCII.GetString(obj,(int)at,(int)length);
    }
    private static void SpanRange(byte[] obj,uint at,uint bytes) { if (at>(uint)obj.Length || bytes>(uint)obj.Length-at) Fail("coff_bounds"); }
    private static ushort U16(byte[] obj,uint at) { SpanRange(obj,at,2); return BinaryPrimitives.ReadUInt16LittleEndian(obj.AsSpan((int)at)); }
    private static uint U32(byte[] obj,uint at) { SpanRange(obj,at,4); return BinaryPrimitives.ReadUInt32LittleEndian(obj.AsSpan((int)at)); }
    private static void W32(byte[] obj,uint at,uint value) => BinaryPrimitives.WriteUInt32LittleEndian(obj.AsSpan((int)at),value);
    [System.Diagnostics.CodeAnalysis.DoesNotReturn] private static void Fail(string why) => throw new InvalidDataException(why);
}
