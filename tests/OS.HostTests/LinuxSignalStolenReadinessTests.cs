using System.Buffers.Binary;
using OS.Tooling;
using F = OS.Tooling.LinuxSignalProofFixture;
using T = LinuxSignalTranscriptTests;

internal static class LinuxSignalStolenReadinessTests
{
    [HostTest("m16-020 pipe race emits actual child write signal competing read and later parent progress")]
    private static void Instructions()
    {
        byte[] elf=F.CreateElf(F.StolenWaitVariant), package=F.CreatePackage(F.StolenWaitVariant);
        HostCheck.Equal(F.StolenWaitVariant,F.PackageVariant(package));
        var calls=F.ExpectedWaitInvocations(F.StolenWaitVariant);
        var child=calls.Where(c=>c.Child).ToArray();
        HostCheck.True(child.Select(c=>c.Number).SequenceEqual(new uint[]{39,186,1,234,0,60}));
        foreach(var call in child.Where(c=>c.Number is 0 or 1))
        {
            int end=checked((int)(call.ReturnRip-F.ImageBase));
            HostCheck.True(elf.AsSpan(end-8,2).SequenceEqual(new byte[]{0x8b,0x3d}));
            int displacement=BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(end-6));
            HostCheck.Equal(F.WaitPair+(call.Number==1?4:0),end-2+displacement);
            HostCheck.Equal(call.Number,BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(end-45)));
        }
        var parentIo=calls.Where(c=>!c.Child&&!c.Handler&&c.Number is 0 or 1).ToArray();
        HostCheck.True(parentIo.Select(c=>c.Number).SequenceEqual(new uint[]{0,1,0,1}));
        HostCheck.True(calls.Count(c=>c.Handler&&c.Number==15)==1);
        byte[] altered=(byte[])package.Clone();altered[^1]^=1;
        bool rejected=false;try{F.PackageVariant(altered);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);
    }

    [HostTest("m16-020 pipe race transcript rejects reordered wake signal drain missing handler and fabricated progress")]
    private static void Transcript()
    {
        string[] rows=LinuxSignalWaitTranscriptTests.Synthetic(F.StolenWaitVariant);byte[] package=F.CreatePackage(F.StolenWaitVariant);
        QemuTranscripts.ValidateLinuxSignalTail(rows,package,0x200000,0x100000);
        HostCheck.Equal(46,rows.Length);
        int Child(ulong nr)=>Array.FindIndex(rows,r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal)&&T.Field(r,"identity")==4294967298UL&&T.Field(r,"nr")==nr);
        int write=Child(1),signal=Child(234),read=Child(0);HostCheck.True(write<signal&&signal<read);
        var reordered=(string[])rows.Clone();(reordered[write],reordered[signal])=(reordered[signal],reordered[write]);T.Reject(reordered,package);
        T.Reject(T.Change(rows,read,"valueBits",0),package);
        T.Reject(rows.Where((_,i)=>i!=read).ToArray(),package);
        int resume=Array.FindIndex(rows,r=>r.StartsWith("M16_008_RESUME ",StringComparison.Ordinal));T.Reject(T.Change(rows,resume,"valueBits",0),package);
        int handler=Array.FindIndex(rows,r=>r=="M16_005_WRITE fd=1 bytes=15 data=M16_HANDLER_OK!");T.Reject(rows.Where((_,i)=>i!=handler).ToArray(),package);
        T.Reject(rows,F.CreatePackage("SignalPipeEintr"));
    }
}
