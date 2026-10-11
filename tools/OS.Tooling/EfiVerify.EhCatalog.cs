using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using OS.X64TextSerializerGenerator;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

internal sealed partial class EfiVerification
{
    private void VerifyEhCatalog()
    {
        const string objectName = "CLROS.X64.EhCatalog.obj";
        if (!profile.IsEnabled("M04EhCatalog"))
        {
            Require(!response.Contains(objectName, StringComparison.OrdinalIgnoreCase) &&
                !Regex.IsMatch(map, @"(?m)^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+CLROS_Eh_(?:CatalogStart|CatalogEnd|Contributions|ContributionsEnd)\s+"),
                "eh_catalog_unrequested_native_input");
            return;
        }
        AuthorityFile Pair(string prefix, string name, string suffix)
        {
            string preserved = EvidencePath(prefix + "ObjectPath", "eh_catalog_preserved_path"),
                linked = EvidencePath(prefix + "LinkedObjectPath", "eh_catalog_linked_path");
            Require(SamePath(preserved, artifactPath + suffix) &&
                SamePath(linked, Path.Combine(Path.GetDirectoryName(linkedSerializer)!, name)) &&
                !SamePath(preserved, linked), "eh_catalog_input_path_contract");
            AuthorityFile p = Open(preserved, prefix, MaximumTextBytes, output: true),
                l = Open(linked, prefix + "_linked", MaximumTextBytes, output: true, root: Root);
            Require(Number(prefix + "ObjectBytes") == p.Length && p.Length == l.Length &&
                Text(prefix + "ObjectSha256") == p.Sha256 && p.Sha256 == l.Sha256, "eh_catalog_input_identity");
            AuthorityFiles.RequireDistinct(p, l);
            if (name.EndsWith(".obj", StringComparison.Ordinal))
            {
                // LinkNative executes in the EFI project directory; its original object is relative.
                int count = name == "OS.obj" ? responseTokens.Count(t => t.Length != 0 && t[0] is not ('/' or '-') &&
                    SamePath(FullPath(Path.Combine(Root,"src/OS"),t,"eh_catalog_link_object_path"),linked)) : ObjectCount(linked);
                Require(count == 1,"eh_catalog_link_input_count",name);
            }
            return p;
        }
        var catalog = Pair("ehCatalog", objectName, ".eh-catalog.obj");
        var original = Pair("ehCatalogIlc", "OS.obj", ".ilc.obj");
        var originalMap = Pair("ehCatalogMap", "OS.map.xml", ".ilc.map.xml");
        AuthorityFiles.RequireDistinct(catalog, original, originalMap);
        byte[] originalBytes = original.ReadBytes(MaximumTextBytes, "eh_catalog_ilc_read");
        byte[] expected;
        try { expected = EhContributionCatalogGenerator.Generate(originalBytes,
            originalMap.ReadUtf8(MaximumTextBytes, "eh_catalog_map_read")); }
        catch (Exception error) when (error is InvalidDataException or System.Xml.XmlException or OverflowException or FormatException)
        { throw new ToolFailure("eh_catalog_original_inputs_rejected", error.GetType().Name); }
        Require(catalog.ReadBytes(MaximumTextBytes, "eh_catalog_read").AsSpan().SequenceEqual(expected), "eh_catalog_recomputed_object_mismatch");
        // Offsets below describe only the producer's recomputed, byte-identical two-section object.
        uint U32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(at,4));
        int dataOffset = checked((int)U32(80)), dataBytes = checked((int)U32(76)),
            symbolOffset = checked((int)U32(8)), symbolCount = checked((int)U32(12));
        byte[] payload = expected.AsSpan(dataOffset, dataBytes).ToArray();
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(8,4));
        Require(count == 2 && symbolCount == 4 + count * 2 && dataBytes == 16 + count * 16, "eh_catalog_frame_contribution_count");
        string Name(byte[] bytes,int symbols,int total,int index)
        {
            int at = symbols + index * 18;
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at,4)) != 0)
                return Encoding.ASCII.GetString(bytes.AsSpan(at,8)).TrimEnd('\0');
            int start = checked(symbols + total * 18 + (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at+4,4))), end = start;
            while (bytes[end] != 0) end++;
            return Encoding.ASCII.GetString(bytes.AsSpan(start,end-start));
        }
        string SymbolName(int index) => Name(expected,symbolOffset,symbolCount,index);
        // Generate has already bounded and authenticated this COFF layout and its primary relocation targets.
        uint OriginalWord(int at) => BinaryPrimitives.ReadUInt32LittleEndian(originalBytes.AsSpan(at,4));
        int originalSymbols=checked((int)OriginalWord(8)), originalCount=checked((int)OriginalWord(12));
        void JoinOriginalContribution(string name,ulong address,uint length,bool eh)
        {
            int found=-1;
            for (int i=0;i<originalCount;)
            {
                int at=originalSymbols+i*18;
                if (Name(originalBytes,originalSymbols,originalCount,i)==name && originalBytes[at+16]==2 &&
                    BinaryPrimitives.ReadInt16LittleEndian(originalBytes.AsSpan(at+12,2))>0)
                { Require(found<0,"eh_catalog_original_symbol_alias"); found=at; }
                i+=1+originalBytes[at+17];
            }
            Require(found>=0,"eh_catalog_original_symbol_missing",name);
            int section=20+(BinaryPrimitives.ReadInt16LittleEndian(originalBytes.AsSpan(found+12,2))-1)*40;
            uint value=OriginalWord(found+8);
            byte[] linkedBytes=originalBytes.AsSpan(checked((int)(OriginalWord(section+20)+value)),checked((int)length)).ToArray();
            int relocations=checked((int)OriginalWord(section+24)), relocationCount=BinaryPrimitives.ReadUInt16LittleEndian(originalBytes.AsSpan(section+32,2));
            for (int i=0;i<relocationCount;i++)
            {
                int at=relocations+i*10; uint fixup=OriginalWord(at);
                if (fixup<value || (ulong)fixup>=(ulong)value+length) continue;
                int within=checked((int)(fixup-value)), target=checked((int)OriginalWord(at+4));
                ushort kind=BinaryPrimitives.ReadUInt16LittleEndian(originalBytes.AsSpan(at+8,2));
                string targetName=Name(originalBytes,originalSymbols,originalCount,target);
                ulong targetAddress=Address(targetName,"OS.obj","eh_catalog_original");
                if (kind==3)
                    BinaryPrimitives.WriteUInt32LittleEndian(linkedBytes.AsSpan(within,4),
                        unchecked((uint)(targetAddress-image.ImageBase)+BinaryPrimitives.ReadUInt32LittleEndian(linkedBytes.AsSpan(within,4))));
                else
                {
                    Require(!eh && kind is >=4 and <=9,"eh_catalog_original_relocation_kind");
                    decimal delta=(decimal)targetAddress-address-within-4-(kind-4)+BinaryPrimitives.ReadInt32LittleEndian(linkedBytes.AsSpan(within,4));
                    Require(delta>=int.MinValue && delta<=int.MaxValue,"eh_catalog_original_rel32_range");
                    BinaryPrimitives.WriteInt32LittleEndian(linkedBytes.AsSpan(within,4),(int)delta);
                }
            }
            var backing=image.Resolve(address-image.ImageBase,length,"eh_catalog_original_not_backed","eh_catalog_original_ambiguous");
            Require(image.Copy(backing.Offset,checked((int)length),"eh_catalog_original_truncated").AsSpan().SequenceEqual(linkedBytes),
                eh ? "eh_catalog_original_eh_join" : "eh_catalog_original_code_join",name);
        }
        ulong start = Address(EhContributionCatalogGenerator.DataSymbol, objectName, "eh_catalog"),
            end = Address(EhContributionCatalogGenerator.DataEndSymbol, objectName, "eh_catalog"),
            getter = Address(EhContributionCatalogGenerator.StartSymbol, objectName, "eh_catalog"),
            endGetter = Address(EhContributionCatalogGenerator.EndSymbol, objectName, "eh_catalog");
        Require(start % 4 == 0 && end - start == (ulong)dataBytes && endGetter == getter + 8 && getter % 16 == 0,
            "eh_catalog_linked_extent");
        var code = image.Resolve(getter-image.ImageBase,16,"eh_catalog_getters_not_backed","eh_catalog_getters_ambiguous");
        code.Section.RequireCode("eh_catalog_getters_permissions");
        byte[] text = image.Copy(code.Offset,16,"eh_catalog_getters_truncated");
        Rel32(text,3,getter,start,"eh_catalog_start_rel32"); Rel32(text,11,getter,end,"eh_catalog_end_rel32");
        text.AsSpan(3,4).Clear(); text.AsSpan(11,4).Clear();
        Require(text.AsSpan().SequenceEqual(expected.AsSpan(checked((int)U32(40)),16)),"eh_catalog_getter_code_mismatch");
        HashSet<string> methods = [];
        for (int i=0; i<count; i++)
        {
            string method = SymbolName(4+i*2), eh = SymbolName(5+i*2);
            const string methodPrefix = "OS_OS_Runtime_RhpNewFastAllocation__";
            Require(eh == "__ehinfo_" + method && methods.Add(method) &&
                (method == methodPrefix + "FrameMiddle" || method == methodPrefix + "FrameCatcher"), "eh_catalog_frame_method_set");
            ulong root = Address(method,"OS.obj","eh_catalog"), table = Address(eh,"OS.obj","eh_catalog");
            int at=16+i*16;
            uint codeBytes=BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(at+4,4)),
                ehBytes=BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(at+12,4));
            Require(codeBytes == (method.EndsWith("FrameMiddle",StringComparison.Ordinal) ? 72U : 138U) &&
                ehBytes == (method.EndsWith("FrameMiddle",StringComparison.Ordinal) ? 4U : 15U), "eh_catalog_frame_geometry");
            image.Resolve(root-image.ImageBase,codeBytes,"eh_catalog_code_not_backed","eh_catalog_code_ambiguous").Section.RequireCode("eh_catalog_code_permissions");
            image.Resolve(table-image.ImageBase,ehBytes,"eh_catalog_eh_not_backed","eh_catalog_eh_ambiguous").Section.RequireReadOnlyData("eh_catalog_eh_permissions");
            JoinOriginalContribution(method,root,codeBytes,false);
            JoinOriginalContribution(eh,table,ehBytes,true);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at,4),checked((uint)(root-image.ImageBase)));
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at+8,4),checked((uint)(table-image.ImageBase)));
        }
        var data=image.Resolve(start-image.ImageBase,(ulong)dataBytes,"eh_catalog_data_not_backed","eh_catalog_data_ambiguous");
        data.Section.RequireReadOnlyData("eh_catalog_data_permissions");
        Require(image.Copy(data.Offset,dataBytes,"eh_catalog_data_truncated").AsSpan().SequenceEqual(payload),"eh_catalog_linked_payload_mismatch");
        emit($"EFI_ARTIFACT_EH_CATALOG methods={count} bytes={dataBytes} originalObjectSha256={original.Sha256} originalMapSha256={originalMap.Sha256} recomputed=true transferAccepted=false");
    }
}
