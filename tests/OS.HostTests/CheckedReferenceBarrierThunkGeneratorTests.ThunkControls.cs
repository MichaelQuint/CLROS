#if CHECKED_REFERENCE_THUNK_CHILD
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.X64TextSerializerGenerator;

internal static partial class ThunkControls
{
    internal static void Need(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    static byte[] Hex(string value) => Convert.FromHexString(string.Concat(value.Where(c => !char.IsWhiteSpace(c))));
    // Independent, hand-derived scalar instruction oracle, including all sixteen explicit XMM slots.
    static readonly byte[] Scalar = Hex("""
        9C55 4889E5 505152535657 41504151415241534154415541564157
        4881EC30010000 4883E4F0 48F7450800040000 0F858E010000
        F30F7F842420000000 F30F7F8C2430000000 F30F7F942440000000 F30F7F9C2450000000
        F30F7FA42460000000 F30F7FAC2470000000 F30F7FB42480000000 F30F7FBC2490000000
        F3440F7F8424A0000000 F3440F7F8C24B0000000 F3440F7F9424C0000000 F3440F7F9C24D0000000
        F3440F7FA424E0000000 F3440F7FAC24F0000000 F3440F7FB42400010000 F3440F7FBC2410010000
        0FAE9C2420010000 B901000000 488B55F0 4C8B45E8 41B908000000
        E800000000 83F801 0F85CD000000 9C415B49F7C300040000 0F85BD000000
        0FAE942420010000
        F30F6F842420000000 F30F6F8C2430000000 F30F6F942440000000 F30F6F9C2450000000
        F30F6FA42460000000 F30F6FAC2470000000 F30F6FB42480000000 F30F6FBC2490000000
        F3440F6F8424A0000000 F3440F6F8C24B0000000 F3440F6F9424C0000000 F3440F6F9C24D0000000
        F3440F6FA424E0000000 F3440F6FAC24F0000000 F3440F6FB42400010000 F3440F6FBC2410010000
        488D6590 415F415E415D415C415B415A41594158 5F5E5B5A59585D9DC3 EBFE
        """);
    static byte[] ExpectedText()
    {
        Need(Scalar.Length == 452, "literal-scalar-size");
        byte[] second = (byte[])Scalar.Clone(); second[213] = 2;
        byte[] third = [.. Scalar[..421], .. Hex("488345D808488345D008"), .. Scalar[421..]];
        third[213] = 3; third[220] = 0xD0; third[224] = 0xD8;
        BinaryPrimitives.WriteInt32LittleEndian(third.AsSpan(48),408);
        BinaryPrimitives.WriteInt32LittleEndian(third.AsSpan(241),215);
        BinaryPrimitives.WriteInt32LittleEndian(third.AsSpan(257),199);
        return [.. Scalar, .. Enumerable.Repeat((byte)0x90,12), .. second, .. Enumerable.Repeat((byte)0x90,12), .. third];
    }
    static byte[] ExpectedObject()
    {
        byte[] text = ExpectedText(); Need(text.Length == 1390,"literal-text-size");
        // Fixed complete COFF header, section, relocation and symbol rows. No serializer helpers used.
        byte[] header = Hex("6486 0100 00000000 CA050000 04000000 0000 0000");
        byte[] section = Hex("2E74657874000000 00000000 00000000 6E050000 3C000000 AC050000 00000000 0300 0000 20005060");
        byte[] relocations = Hex("E8000000 03000000 0400 B8020000 03000000 0400 88040000 03000000 0400");
        byte[] symbols = Hex("""
          00000000 04000000 00000000 0100 2000 02 00
          00000000 11000000 D0010000 0100 2000 02 00
          00000000 25000000 A0030000 0100 2000 02 00
          00000000 37000000 00000000 0000 2000 02 00
          """);
        return [.. header,.. section,.. text,0,0,.. relocations,.. symbols,0x5B,0,0,0,
            .. Encoding.ASCII.GetBytes("RhpAssignRef\0RhpCheckedAssignRef\0RhpByRefAssignRef\0CLROS_CheckedReferenceBarrier_Apply\0")];
    }
    static ushort U16(byte[] b,int p)=>BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(p));
    static uint U32(byte[] b,int p)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));
    internal static byte[] CheckObject(byte[] b)
    {
        Need(b.Length==1645 && b.AsSpan().SequenceEqual(ExpectedObject()),"complete-independent-object-literal");
        Need(U16(b,0)==0x8664 && U16(b,2)==1 && U32(b,4)==0 && U32(b,8)==1482 && U32(b,12)==4 && U32(b,16)==0,"file-header");
        Need(Encoding.ASCII.GetString(b,20,8)==".text\0\0\0" && U32(b,28)==0 && U32(b,32)==0 && U32(b,36)==1390 && U32(b,40)==60 && U32(b,44)==1452 && U32(b,48)==0 && U16(b,52)==3 && U16(b,54)==0 && U32(b,56)==0x60500020,"section-header-no-unwind");
        string[] names=["RhpAssignRef","RhpCheckedAssignRef","RhpByRefAssignRef","CLROS_CheckedReferenceBarrier_Apply"];
        uint[] values=[0,464,928,0]; int[] nameOffsets=[4,17,37,55];
        Need(U32(b,1554)==91,"string-length");
        for(int i=0;i<4;i++)
        {
            int p=1482+i*18; int start=1554+nameOffsets[i]; int end=Array.IndexOf(b,(byte)0,start);
            Need(U32(b,p)==0 && U32(b,p+4)==nameOffsets[i] && U32(b,p+8)==values[i] && U16(b,p+12)==(i==3?0:1) && U16(b,p+14)==0x20 && b[p+16]==2 && b[p+17]==0,"symbol-row-"+i);
            Need(Encoding.ASCII.GetString(b,start,end-start)==names[i],"symbol-name-"+i);
        }
        uint[] calls=[232,696,1160];
        for(int i=0;i<3;i++)
        {
            int p=1452+i*10; Need(U32(b,p)==calls[i] && U32(b,p+4)==3 && U16(b,p+8)==4,"REL32-row-"+i);
            Need(b[60+(int)calls[i]-1]==0xE8 && U32(b,60+(int)calls[i])==0,"call-addend");
            int entry=(int)values[i], terminal=entry+(i==2?460:450);
            foreach(int d in new[]{48,241,257}) Need(entry+d+4+BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(60+entry+d))==terminal,"local-reject-branch");
            Need(b[60+terminal]==0xEB && b[61+terminal]==0xFE,"terminal-loop");
        }
        return b[60..1450];
    }
    internal static object Run(string output, string expectedSerializerHash, int expectedSerializerSize)
    {
        Directory.CreateDirectory(output);
        var definition=CheckedReferenceBarrierThunkGenerator.Define();
        Need(Amd64CoffWriter.TryGetRequiredSize(in definition,true,out int size,out _) && size==1645,"size");
        byte[] guarded=Enumerable.Range(0,1709).Select(i=>(byte)(i*37+11)).ToArray(); byte[] before=(byte[])guarded.Clone();
        Need(CheckedReferenceBarrierThunkGenerator.TryGenerate(guarded.AsSpan(32,1645),out int written,out var error) && written==1645 && error==CoffWriteError.None,"generate");
        Need(guarded.AsSpan(0,32).SequenceEqual(before.AsSpan(0,32)) && guarded.AsSpan(1677).SequenceEqual(before.AsSpan(1677)),"generation-canaries");
        byte[] obj=guarded[32..1677]; byte[] text=CheckObject(obj);
        File.WriteAllBytes(Path.Combine(output,"checked-reference-thunks.obj"),obj);
        foreach(int capacity in new[]{0,1,60,1644})
        {
            byte[] destination=Enumerable.Range(0,1709).Select(i=>(byte)(i*29+71)).ToArray(); byte[] old=(byte[])destination.Clone();
            Need(!CheckedReferenceBarrierThunkGenerator.TryGenerate(destination.AsSpan(32,capacity),out written,out error) && written==0 && error==CoffWriteError.CapacityExceeded && destination.AsSpan().SequenceEqual(old),"capacity-atomic-"+capacity);
        }
        byte[] rejected=Enumerable.Repeat((byte)0xA7,1645).ToArray();
        Need(!Amd64CoffWriter.TryWrite(in definition,rejected,out written,out _) && written==0 && rejected.All(x=>x==0xA7),"default-reject-atomic");
        byte[] serializer=new byte[expectedSerializerSize]; Need(InstructionPublicationSerializerGenerator.TryGenerate(serializer,out written,out error) && written==expectedSerializerSize && Convert.ToHexString(SHA256.HashData(serializer))==expectedSerializerHash,"serializer-unchanged");
        File.WriteAllBytes(Path.Combine(output,"serializer.obj"),serializer);
        List<object> states=[];
        uint[] statuses=[0,2,0x101,0x80000001,uint.MaxValue];
        for(int operation=1;operation<=3;operation++) foreach(int residue in new[]{0,8})
        {
            states.Add(new Model(text,operation,residue,1,false,false).Verify());
            states.Add(new Model(text,operation,residue,1,true,false).Verify());
            states.Add(new Model(text,operation,residue,1,false,true).Verify());
            foreach(uint status in statuses) states.Add(new Model(text,operation,residue,status,false,false).Verify());
        }
        // Each single-byte mutation must fail complete-object validation. This checks every field,
        // displacement, padding byte and string byte; semantic state tests separately use valid bytes.
        int mutations=0;
        for(int p=0;p<obj.Length;p++)
        {
            byte[] bad=(byte[])obj.Clone(); bad[p]^=1; bool failed=false;
            try { CheckObject(bad); } catch(InvalidOperationException) { failed=true; }
            Need(failed,"object-mutation-"+p); mutations++;
        }
        return new {Passed=true,ObjectBytes=obj.Length,ObjectSha256=Convert.ToHexString(SHA256.HashData(obj)),TextBytes=text.Length,
            Functions=3,ExternalCallbacks=1,Rel32=3,LocalRejectBranches=9,StateCases=states,SingleByteObjectRejects=mutations,
            NativeCodeExecuted=false,RealCallbackPresent=false,UnwindAccepted=false,WiderStateAccepted=false,GuestAccepted=false};
    }
















































































































}

















#else
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
#endif
