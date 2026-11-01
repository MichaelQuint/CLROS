using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.WritableBlockSession;

// Independent fake MMIO/DMA and volatile/persistent device bytes test only
// host protocol behavior; no boot activation or real persistence is established.
internal static unsafe partial class WritableBlockSessionTests
{
    private const ulong Common = 0x400000, Notify = 0x403000, Config = 0x402000,
        Physical = 0x220000, DeviceBase = 0x123456780000, Capacity = 0x100000010;
    private static byte Payload(int i) => unchecked((byte)(i * 73 + 19));
    private static void Fill(Span<byte> bytes) { for (int i = 0; i < bytes.Length; i++) bytes[i] = Payload(i); }
    private static Block.Status Write(Fixture f, ulong sector, ReadOnlySpan<byte> bytes, out Block.RequestToken token)
    { Block.RequestToken value = default; var status = Block.SubmitWrite(f.State, sector, bytes, &value); token = value; return status; }
    private static Block.Status Flush(Fixture f, out Block.RequestToken token)
    { Block.RequestToken value = default; var status = Block.SubmitFlush(f.State, &value); token = value; return status; }
    private static Block.Status Read(Fixture f, ulong sector, out Block.RequestToken token)
    { Block.RequestToken value = default; var status = Block.SubmitRead(f.State, sector, &value); token = value; return status; }
    private static Block.Status Poll(Fixture f, out Block.Completion completion)
    { Block.Completion value = default; var status = Block.Poll(f.State, &value); completion = value; return status; }
    internal static int ManagedOffset<T>(T* value,string field) where T:unmanaged
    {
        var method=new DynamicMethod("WritableManagedOffset",typeof(int),new[]{typeof(nint)},typeof(WritableBlockSessionTests).Module,true);
        var il=method.GetILGenerator();il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda,typeof(T).GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!);
        il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Sub);il.Emit(OpCodes.Conv_I4);il.Emit(OpCodes.Ret);
        return ((Func<nint,int>)method.CreateDelegate(typeof(Func<nint,int>)))((nint)value);
    }

    [HostTest("writable block actual OUT and no-data FLUSH distinguish submitted completed and durable bytes")]
    private static void Frontiers()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start());
        HostCheck.True(f.Negotiation.Succeeded && f.Hardware->Chosen == Block.Features);
        Span<byte> bytes = stackalloc byte[512]; Fill(bytes); byte[] expected = bytes.ToArray();
        HostCheck.Equal(Block.Status.Success, Write(f, 7, bytes, out var token)); bytes.Clear();
        HostCheck.True(f.State->SubmittedBytes == 512 && f.State->CompletedBytes == 0 && f.State->DurableBytes == 0 && f.State->Dirty);
        HostCheck.Equal(Block.Status.Busy, Flush(f, out _)); HostCheck.Equal(Block.Status.Pending, Poll(f, out _));
        f.Complete(); HostCheck.Equal(Block.Status.Success, Poll(f, out var done));
        HostCheck.True(done.Token.Generation == token.Generation && done.Kind == Block.Operation.Write && done.Bytes == 512 &&
            !done.Cancelled && done.DeviceResult == Block.Status.Success && f.State->CompletedBytes == 512 && f.State->DurableBytes == 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.Hardware->Volatile, 512).SequenceEqual(expected) &&
            new ReadOnlySpan<byte>(f.Hardware->Durable, 512).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.Equal(Block.Status.Success, Flush(f, out token));
        HostCheck.True(f.Hardware->Type == 4 && f.State->DurableBytes == 0); f.Complete();
        HostCheck.Equal(Block.Status.Success, Poll(f, out done));
        HostCheck.True(done.Kind == Block.Operation.Flush && done.Bytes == 0 && f.State->DurableBytes == 512 && !f.State->Dirty &&
            new ReadOnlySpan<byte>(f.Hardware->Durable, 512).SequenceEqual(expected));
        HostCheck.Equal(Block.Status.Success, Flush(f, out _)); f.Complete(); HostCheck.Equal(Block.Status.Success, Poll(f, out _));
        HostCheck.Equal(3U, f.Hardware->Notifies); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); f.CheckGuards();
    }

    [HostTest("writable block unsupported flush RO and rejected features never admit writes")]
    private static void Features()
    {
        foreach (ulong offered in new[] { 0UL, 1UL << 32, Block.Features | (1UL << 5), Block.FlushFeature })
        {
            using var f = new Fixture(); f.Hardware->Offered = offered;
            var negotiated = f.Negotiate(); HostCheck.True(!negotiated.Succeeded && negotiated.FailureStatusAttempted && negotiated.FailureStatusWritten);
            var result = Block.Start(f.State, f.Input); HostCheck.True(result != Block.Status.Success && f.Pool->AllocatedMask == 0 && f.Hardware->Notifies == 0 && !f.Hardware->BusEnabled);
        }
        using var rejected = new Fixture(); rejected.Hardware->RejectFeatures = true;
        HostCheck.Equal(VirtioNegotiationStatus.FeaturesRejected, rejected.Negotiate().Status);
        HostCheck.Equal(Block.Features, rejected.Negotiation.Selected);
        HostCheck.Equal(Block.Status.UnsupportedFeatures, Block.Start(rejected.State, rejected.Input));
    }

    [HostTest("writable block write and flush completion failures retain dirty retry state without durability")]
    private static void CompletionFailures()
    {
        Span<byte> bytes = stackalloc byte[512]; Fill(bytes);
        foreach (bool flush in new[] { false, true })
        foreach (var item in new (uint Id,uint Length,byte Status,ushort Delta,Block.Status Expected)[]
        { (1,1,0,1,Block.Status.InvalidCompletion),(0,0,0,1,Block.Status.InvalidCompletion),(0,2,0,1,Block.Status.InvalidCompletion),
          (0,513,0,1,Block.Status.InvalidCompletion),(0,1,0,2,Block.Status.InvalidCompletion),(0,1,255,1,Block.Status.InvalidCompletion),
          (0,1,1,1,Block.Status.DeviceIoError),(0,1,2,1,Block.Status.UnsupportedRequest) })
        {
            using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); HostCheck.Equal(Block.Status.Success, Write(f, 7, bytes, out _));
            if (flush) { f.Complete(); HostCheck.Equal(Block.Status.Success, Poll(f, out _)); HostCheck.Equal(Block.Status.Success, Flush(f, out _)); }
            f.Complete(item.Id,item.Length,item.Status,item.Delta); HostCheck.Equal(item.Expected, Poll(f, out _));
            HostCheck.True(f.State->LastFailure == item.Expected && f.State->DurableBytes == 0 && f.State->Dirty && f.State->RetryRequired &&
                f.State->RequiresCleanup && f.Pool->AllocatedMask == 1 && f.Owner == DmaBufferOwnership.Device);
            HostCheck.Equal(Block.Status.InvalidState, Flush(f, out _)); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            HostCheck.True(f.State->Dirty && f.State->RetryRequired && f.State->LastFailure == item.Expected && f.State->DurableBytes == 0); f.CheckGuards();
        }
    }

    [HostTest("writable block cancellation cannot undo a completed OUT or acknowledged FLUSH")]
    private static void Cancellation()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); Span<byte> bytes = stackalloc byte[512]; Fill(bytes);
        HostCheck.Equal(Block.Status.Success, Write(f, 8, bytes, out var token));
        HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token)); HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
        HostCheck.Equal(Block.Status.Busy, Flush(f, out _)); f.Complete(); HostCheck.Equal(Block.Status.Cancelled, Poll(f, out var done));
        HostCheck.True(done.Cancelled && done.DeviceResult == Block.Status.Success && done.Bytes == 512 && f.State->CompletedBytes == 512 && f.State->DurableBytes == 0);
        HostCheck.Equal(Block.Status.StaleToken, Block.Cancel(f.State, token));
        HostCheck.Equal(Block.Status.Success, Flush(f, out token)); HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
        f.Complete(); HostCheck.Equal(Block.Status.Cancelled, Poll(f, out done));
        HostCheck.True(done.Cancelled && done.DeviceResult == Block.Status.Success && f.State->DurableBytes == 512 && !f.State->Dirty);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); f.CheckGuards();
    }

    [HostTest("writable block later failed writes or flushes preserve the earlier durable frontier and retry facts")]
    private static void PriorDurableFrontier()
    {
        Span<byte> bytes=stackalloc byte[512];
        foreach(bool flushFailure in new[]{false,true})
        {
            using var f=new Fixture(); Fill(bytes); byte[] first=bytes.ToArray();
            HostCheck.Equal(Block.Status.Success,f.Start()); HostCheck.Equal(Block.Status.Success,Write(f,2,bytes,out _));
            f.Complete(); HostCheck.Equal(Block.Status.Success,Poll(f,out _));
            HostCheck.Equal(Block.Status.Success,Flush(f,out _)); f.Complete(); HostCheck.Equal(Block.Status.Success,Poll(f,out _));
            for(int i=0;i<bytes.Length;i++)bytes[i]^=255;
            HostCheck.Equal(Block.Status.Success,Write(f,3,bytes,out _));
            if(flushFailure)
            { f.Complete(); HostCheck.Equal(Block.Status.Success,Poll(f,out _)); HostCheck.Equal(Block.Status.Success,Flush(f,out _)); }
            f.Complete(status:1); HostCheck.Equal(Block.Status.DeviceIoError,Poll(f,out var completion));
            HostCheck.True(completion.DeviceResult==Block.Status.DeviceIoError && f.State->SubmittedBytes==1024 &&
                f.State->CompletedBytes==(flushFailure?1024UL:512UL) && f.State->DurableBytes==512 && f.State->Dirty && f.State->RetryRequired &&
                new ReadOnlySpan<byte>(f.Hardware->Durable,512).SequenceEqual(first));
            HostCheck.Equal(Block.Status.Success,Block.Stop(f.State));
            HostCheck.True(f.State->DurableBytes==512 && f.State->Dirty && f.State->RetryRequired && f.State->LastFailure==Block.Status.DeviceIoError &&
                new ReadOnlySpan<byte>(f.Hardware->Durable,512).SequenceEqual(first)); f.CheckGuards();
        }
    }

    [HostTest("writable block timeout quarantines DMA and reset never manufactures durable writes")]
    private static void TimeoutAndReset()
    {
        Span<byte> bytes = stackalloc byte[512]; Fill(bytes);
        foreach (bool flush in new[] { false, true })
        {
            using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); HostCheck.Equal(Block.Status.Success, Write(f, 8, bytes, out _));
            if (flush) { f.Complete(); HostCheck.Equal(Block.Status.Success, Poll(f, out _)); HostCheck.Equal(Block.Status.Success, Flush(f, out _)); }
            f.Hardware->Now = 1000; HostCheck.Equal(Block.Status.Timeout, Poll(f, out _));
            byte[] dma = new ReadOnlySpan<byte>(f.Data, 4096).ToArray(); f.Hardware->ResetStuck = true;
            HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State));
            HostCheck.True(f.State->LastFailure == Block.Status.Timeout && f.Pool->AllocatedMask == 1 && f.Owner == DmaBufferOwnership.Device &&
                new ReadOnlySpan<byte>(f.Data, 4096).SequenceEqual(dma));
            f.Hardware->ResetStuck = false; f.Hardware->DisableFailure = true; HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State));
            f.Hardware->DisableFailure = false; f.Hardware->ResetFenceFailure = true; HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State));
            HostCheck.True(f.Pool->AllocatedMask == 1 && f.State->DurableBytes == 0);
            f.Hardware->ResetFenceFailure = false; f.Hardware->ResetWriteFailure = true;
            HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State)); HostCheck.Equal(1UL, f.Pool->AllocatedMask);
            f.Hardware->ResetWriteFailure = false; HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            HostCheck.True(f.State->Retired && f.State->Dirty && f.State->RetryRequired && f.State->DurableBytes == 0 &&
                f.State->LastFailure == Block.Status.Timeout && f.Pool->AllocatedMask == 0 && new ReadOnlySpan<byte>(f.Data,4096).IndexOfAnyExcept((byte)0)<0);
            uint calls = f.Hardware->Calls; HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); HostCheck.Equal(calls,f.Hardware->Calls); f.CheckGuards();
        }
    }

    [HostTest("writable block original deadlines configuration generation and late used entries fail closed")]
    private static void Generations()
    {
        Span<byte> bytes = stackalloc byte[512]; Fill(bytes);
        foreach (int mode in new[] { 0,1,2,3,4,5 })
        {
            using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); HostCheck.Equal(Block.Status.Success, Write(f,8,bytes,out var token));
            if (mode == 0) { f.Hardware->Now = 1000; f.Complete(); HostCheck.Equal(Block.Status.Success,Poll(f,out _)); }
            else if (mode == 1) { f.Hardware->Generation++; HostCheck.Equal(Block.Status.ConfigurationChanged,Poll(f,out _)); }
            else if (mode == 2) { f.Hardware->ClockFailure = true; HostCheck.Equal(Block.Status.ClockFailed,Poll(f,out _)); }
            else if (mode == 3) { f.Complete(); HostCheck.Equal(Block.Status.Success,Poll(f,out _)); *(ushort*)(f.Data+150)=2; HostCheck.Equal(Block.Status.InvalidCompletion,Poll(f,out _)); }
            else if (mode == 4) { f.Hardware->Now=100; HostCheck.Equal(Block.Status.Pending,Poll(f,out _)); f.Hardware->Now=99; HostCheck.Equal(Block.Status.ClockRegressed,Poll(f,out _)); }
            else { f.Hardware->Now=ulong.MaxValue; HostCheck.Equal(Block.Status.Timeout,Poll(f,out _)); }
            Block.RequestToken wrong = token; ((ulong*)&wrong)[2]++;
            HostCheck.True(Block.Cancel(f.State,wrong) is Block.Status.StaleToken or Block.Status.InvalidState);
            Block.State copied = *f.State; uint before = f.Hardware->Calls;
            HostCheck.Equal(Block.Status.InvalidState,Block.Stop(&copied)); HostCheck.Equal(before,f.Hardware->Calls);
            HostCheck.Equal(Block.Status.Success,Block.Stop(f.State)); HostCheck.True(f.State->DurableBytes==0); f.CheckGuards();
        }
    }

    [HostTest("writable block stale DMA generation never reuses or frees another lease")]
    private static void DmaGeneration()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success,f.Start()); Span<byte> bytes=stackalloc byte[512]; Fill(bytes);
        HostCheck.Equal(Block.Status.Success,Write(f,1,bytes,out _));
        ulong* generation=(ulong*)((byte*)f.Slot+(int)Marshal.OffsetOf<DmaBufferSlot>("Generation")); ulong original=*generation; (*generation)++;
        uint before=f.Hardware->Calls; HostCheck.Equal(Block.Status.DmaRejected,Poll(f,out _)); HostCheck.Equal(before,f.Hardware->Calls);
        HostCheck.Equal(Block.Status.DmaRejected,Block.Stop(f.State)); HostCheck.True(f.Pool->AllocatedMask==1 && f.Owner==DmaBufferOwnership.Device && f.State->Receipt.CleanupStatus==VirtioBlkQueueStatus.DmaRejected);
        *generation=original; HostCheck.Equal(Block.Status.Success,Block.Stop(f.State)); HostCheck.Equal(0UL,f.State->DurableBytes); f.CheckGuards();
    }

    [HostTest("writable block changed DMA window cannot redirect cleanup scrub or release authority")]
    private static void DmaWindow()
    {
        using var f=new Fixture(); HostCheck.Equal(Block.Status.Success,f.Start()); Span<byte> bytes=stackalloc byte[512];Fill(bytes);
        HostCheck.Equal(Block.Status.Success,Write(f,7,bytes,out _));
        var held=(DmaAddressWindow*)((byte*)f.Pool+ManagedOffset(f.Pool,"Window")); var original=*held;
        HostCheck.Equal(DmaAddressStatus.Success,DmaAddressWindow.TryCreate((ulong)f.Data+4096,Physical,DeviceBase,4096,64,out var changed));
        *held=changed;byte[] dma=new ReadOnlySpan<byte>(f.Data,4096).ToArray();uint before=f.Hardware->Calls;
        HostCheck.Equal(Block.Status.DmaRejected,Poll(f,out _));HostCheck.Equal(before,f.Hardware->Calls);
        HostCheck.Equal(Block.Status.DmaRejected,Block.Stop(f.State));
        HostCheck.True(f.Pool->AllocatedMask==1 && f.Owner==DmaBufferOwnership.Device && new ReadOnlySpan<byte>(f.Data,4096).SequenceEqual(dma) &&
            new ReadOnlySpan<byte>(f.Data+4096,4096).IndexOfAnyExcept((byte)0xa5)<0);
        *held=original;HostCheck.Equal(Block.Status.Success,Block.Stop(f.State));f.CheckGuards();
    }

    [HostTest("writable block metadata and caller aliases reject before clearing active owner storage")]
    private static void Aliases()
    {
        using var f=new Fixture(); HostCheck.Equal(Block.Status.Success,f.Start()); Span<byte> bytes=stackalloc byte[512]; Fill(bytes);
        HostCheck.Equal(Block.Status.Success,Write(f,1,bytes,out _));
        byte[] state=new ReadOnlySpan<byte>(f.State,sizeof(Block.State)).ToArray(), dma=new ReadOnlySpan<byte>(f.Data,4096).ToArray(); uint before=f.Hardware->Calls;
        foreach(ulong address in new[]{0UL,8UL,0x0000800000000000UL,0x00007ffffffffff8UL,(ulong)f.State,(ulong)f.Pool,(ulong)f.Slot,(ulong)f.Data,Common,Notify,Config,(ulong)f.State+1})
        {
            HostCheck.Equal(Block.Status.InvalidStorage,Block.SubmitFlush(f.State,(Block.RequestToken*)address));
            HostCheck.Equal(Block.Status.InvalidStorage,Block.Poll(f.State,(Block.Completion*)address));
        }
        HostCheck.True(before==f.Hardware->Calls && new ReadOnlySpan<byte>(f.State,sizeof(Block.State)).SequenceEqual(state) &&
            new ReadOnlySpan<byte>(f.Data,4096).SequenceEqual(dma) && f.Pool->AllocatedMask==1);
        f.Complete(); HostCheck.Equal(Block.Status.Success,Poll(f,out _));
        Block.RequestToken output=default; output=*(Block.RequestToken*)f.State;
        foreach(ulong address in new[]{(ulong)f.State,(ulong)f.Pool,(ulong)f.Slot,(ulong)f.Data,Common})
            HostCheck.Equal(Block.Status.InvalidStorage,Block.SubmitWrite(f.State,1,new ReadOnlySpan<byte>((void*)address,512),&output));
        byte[] saved=bytes.ToArray(); before=f.Hardware->Calls;
        fixed(byte* same=bytes) HostCheck.Equal(Block.Status.InvalidStorage,Block.SubmitWrite(f.State,1,bytes,(Block.RequestToken*)same));
        HostCheck.True(before==f.Hardware->Calls && bytes.SequenceEqual(saved));
        HostCheck.Equal(Block.Status.Success,Block.Stop(f.State)); f.CheckGuards();
    }

    [HostTest("writable block retained OUT and FLUSH chains cross ushort wrap without allocation")]
    private static void Wrap()
    {
        using var f=new Fixture(); HostCheck.Equal(Block.Status.Success,f.Start()); Span<byte> bytes=stackalloc byte[512]; Fill(bytes); bool good=true;
        long allocated=0;
        for(uint i=0;i<65538;i++)
        {
            if(i==1) allocated=GC.GetAllocatedBytesForCurrentThread();
            good &= Write(f,i,bytes,out var token)==Block.Status.Success; f.Complete();
            good &= Poll(f,out var result)==Block.Status.Success && result.Token.Generation==token.Generation;
        }
        long delta=GC.GetAllocatedBytesForCurrentThread()-allocated;
        HostCheck.True(good && delta==0 && f.State->CompletedBytes==65538UL*512 && f.State->DurableBytes==0);
        HostCheck.Equal(Block.Status.Success,Flush(f,out _)); f.Complete(); HostCheck.Equal(Block.Status.Success,Poll(f,out _));
        HostCheck.Equal(f.State->CompletedBytes,f.State->DurableBytes); HostCheck.Equal(Block.Status.Success,Block.Stop(f.State)); f.CheckGuards();
    }

    [HostTest("writable block every negotiation and setup callback failure rejects without unowned DMA release")]
    private static void SetupFailures()
    {
        uint count;
        using (var baseline=new Fixture()) { HostCheck.Equal(Block.Status.Success,baseline.Start()); count=baseline.Hardware->Calls; HostCheck.Equal(Block.Status.Success,Block.Stop(baseline.State)); }
        for(uint at=1;at<=count;at++)
        foreach(bool after in new[]{false,true})
        {
            using var f=new Fixture(); f.Hardware->FailCall=at; f.Hardware->FailAfterWrite=after;
            HostCheck.True(f.Start()!=Block.Status.Success && f.Hardware->FailureFired,"setup callback "+at);
            if(f.State->RequiresCleanup) HostCheck.Equal(Block.Status.Success,Block.Stop(f.State));
            HostCheck.True(f.Pool->AllocatedMask==0 && !f.Hardware->BusEnabled && f.State->DurableBytes==0);
        }
    }

    [HostTest("writable block every OUT and FLUSH publication or completion callback failure preserves exact failure and DMA")]
    private static void RequestFailures()
    {
        Span<byte> bytes=stackalloc byte[512]; Fill(bytes);
        for(int phase=0;phase<4;phase++)
        {
            void Prepare(Fixture f)
            {
                HostCheck.Equal(Block.Status.Success,f.Start());
                if(phase>=2)
                { HostCheck.Equal(Block.Status.Success,Write(f,2,new byte[512],out _)); f.Complete(); HostCheck.Equal(Block.Status.Success,Poll(f,out _)); }
                if((phase&1)!=0)
                { HostCheck.Equal(Block.Status.Success,phase==1?Write(f,3,new byte[512],out _):Flush(f,out _)); f.Complete(); }
            }
            uint count;
            using(var baseline=new Fixture())
            {
                Prepare(baseline); uint begin=baseline.Hardware->Calls;
                HostCheck.Equal(Block.Status.Success,phase==0?Write(baseline,3,bytes,out _):phase==2?Flush(baseline,out _):Poll(baseline,out _));
                count=baseline.Hardware->Calls-begin; HostCheck.Equal(Block.Status.Success,Block.Stop(baseline.State));
            }
            for(uint at=1;at<=count;at++)
            foreach(bool after in new[]{false,true})
            {
                using var f=new Fixture(); Prepare(f); f.Hardware->FailCall=f.Hardware->Calls+at; f.Hardware->FailAfterWrite=after;
                var result=phase==0?Write(f,3,bytes,out _):phase==2?Flush(f,out _):Poll(f,out _);
                HostCheck.True(result!=Block.Status.Success && f.Hardware->FailureFired && !f.State->Running &&
                    f.State->LastFailure==result && f.Pool->AllocatedMask==1 && f.Owner==DmaBufferOwnership.Device && f.State->DurableBytes==0,
                    "request phase="+phase+" callback="+at);
                HostCheck.Equal(Block.Status.Success,Block.Stop(f.State)); HostCheck.True(f.State->LastFailure==result && f.State->DurableBytes==0);
            }
        }
    }

    [HostTest("writable block invalid admission overflow and foreign tokens publish nothing")]
    private static void AdmissionBounds()
    {
        using (var churn=new Fixture())
        { churn.Hardware->GenerationChurn=true; HostCheck.Equal(Block.Status.ConfigurationChanged,churn.Start()); HostCheck.True(churn.State->Retired && churn.Pool->AllocatedMask==0); }
        using var f=new Fixture(); f.Negotiate(); uint before=f.Hardware->Calls;
        var admission=f.Input; admission.Epoch=0; HostCheck.Equal(Block.Status.InvalidAccess,Block.Start(f.State,admission));
        admission=f.Input; admission.Access.BusMasterOn=null; HostCheck.Equal(Block.Status.InvalidAccess,Block.Start(f.State,admission));
        HostCheck.Equal(before,f.Hardware->Calls); HostCheck.Equal(Block.Status.Success,Block.Start(f.State,f.Input));
        Span<byte> bytes=stackalloc byte[512]; Fill(bytes);
        before=f.Hardware->Calls; HostCheck.Equal(Block.Status.SectorOutOfRange,Write(f,Capacity,bytes,out _));
        Block.RequestToken token=default; HostCheck.Equal(Block.Status.InvalidStorage,Block.SubmitWrite(f.State,1,bytes[..511],&token));
        HostCheck.Equal(before,f.Hardware->Calls);
        HostCheck.Equal(Block.Status.Success,Write(f,2,bytes,out token));
        before=f.Hardware->Calls;
        for(int field=0;field<3;field++)
        { Block.RequestToken changed=token; ((ulong*)&changed)[field]^=1; HostCheck.Equal(Block.Status.StaleToken,Block.Cancel(f.State,changed)); }
        HostCheck.Equal(before,f.Hardware->Calls); f.Complete(); HostCheck.Equal(Block.Status.Success,Poll(f,out _));
        int generationOffset=ManagedOffset(f.State,"Generation");
        *(ulong*)((byte*)f.State+generationOffset)=ulong.MaxValue;
        before=f.Hardware->Calls; HostCheck.Equal(Block.Status.GenerationExhausted,Write(f,3,bytes,out _)); HostCheck.Equal(before,f.Hardware->Calls);
        HostCheck.Equal(Block.Status.Success,Block.Stop(f.State)); f.CheckGuards();
    }

    [HostTest("writable queue actual IN retains ReadReady until copy and does not advance write durable frontiers")]
    private static void ReadFrontiers()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); byte[] payload = new byte[512]; Fill(payload);
        HostCheck.Equal(Block.Status.Success, Write(f, 4, payload, out _)); f.Complete(); HostCheck.Equal(Block.Status.Success, Poll(f, out _));
        HostCheck.Equal(Block.Status.Success, Read(f, 4, out var token)); HostCheck.Equal(0U, f.Hardware->Type);
        HostCheck.True(f.State->ReadPending && f.State->RequestPending && !f.State->ReadReady);
        byte[] result = new byte[512]; HostCheck.Equal(Block.Status.Pending, Block.CopyRead(f.State, token, result));
        f.Hardware->Now = 1000; f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f, out var completion));
        HostCheck.True(completion.Kind == Block.Operation.Read && completion.Bytes == 512 && !completion.Cancelled && f.State->ReadReady && f.State->RequestPending);
        HostCheck.Equal(Block.Status.Busy, Flush(f, out _)); HostCheck.Equal(Block.Status.Busy, Write(f, 5, payload, out _));
        HostCheck.Equal(Block.Status.Ready, Poll(f, out _)); HostCheck.Equal(2UL, f.State->Completed);
        f.Hardware->ClockFailure = true; // Already validated Ready data has no new clock/expiry dependency.
        HostCheck.Equal(Block.Status.Success, Block.CopyRead(f.State, token, result)); f.Hardware->ClockFailure = false;
        HostCheck.True(result.AsSpan().SequenceEqual(payload) && !f.State->RequestPending && f.State->CompletedBytes == 512 && f.State->SubmittedBytes == 512 && f.State->DurableBytes == 0);
        HostCheck.Equal(Block.Status.StaleToken, Block.CopyRead(f.State, token, result));
        HostCheck.Equal(Block.Status.Success, Flush(f, out _)); f.Complete(); HostCheck.Equal(Block.Status.Success, Poll(f, out _));
        HostCheck.Equal(512UL, f.State->DurableBytes); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); f.CheckGuards();
    }

    [HostTest("writable queue IN status-only errors malformed lengths and timeout never expose stale DMA bytes")]
    private static void ReadFailures()
    {
        foreach (var item in new (uint Id, uint Length, byte Status, ushort Delta, Block.Status Expected)[]
        { (0,1,0,1,Block.Status.InvalidCompletion), (0,512,0,1,Block.Status.InvalidCompletion), (0,514,0,1,Block.Status.InvalidCompletion),
          (0,0,1,1,Block.Status.InvalidCompletion), (1,513,0,1,Block.Status.InvalidCompletion), (0,513,0,2,Block.Status.InvalidCompletion),
          (0,1,1,1,Block.Status.DeviceIoError), (0,513,1,1,Block.Status.DeviceIoError), (0,1,2,1,Block.Status.UnsupportedRequest),
          (0,513,255,1,Block.Status.InvalidCompletion) })
        {
            using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); HostCheck.Equal(Block.Status.Success, Read(f, 3, out var token));
            byte[] output = Enumerable.Repeat((byte)0x71, 512).ToArray(); f.Complete(item.Id, item.Length, item.Status, item.Delta);
            HostCheck.Equal(item.Expected, Poll(f, out _)); HostCheck.Equal(Block.Status.InvalidState, Block.CopyRead(f.State, token, output));
            HostCheck.True(output.All(value => value == 0x71) && f.Pool->AllocatedMask == 1 && f.State->SubmittedBytes == 0 && f.State->DurableBytes == 0);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); f.CheckGuards();
        }
        using var timeout = new Fixture(); HostCheck.Equal(Block.Status.Success, timeout.Start()); Read(timeout, 3, out _);
        timeout.Hardware->Now = 1000; HostCheck.Equal(Block.Status.Timeout, Poll(timeout, out _));
        timeout.Hardware->ResetStuck = true; HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(timeout.State));
        HostCheck.True(timeout.Pool->AllocatedMask == 1 && timeout.Owner == DmaBufferOwnership.Device);
        timeout.Hardware->ResetStuck = false; HostCheck.Equal(Block.Status.Success, Block.Stop(timeout.State)); timeout.CheckGuards();
    }

    [HostTest("writable queue IN copy aliases preserve Ready and changed config used index or lease reject before output")]
    private static void ReadCopyGuards()
    {
        for (int mode = 0; mode < 4; mode++)
        {
            using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); Fill(new Span<byte>(f.Hardware->Volatile, 512));
            Read(f, 3, out var token); f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f, out _)); uint calls = f.Hardware->Calls;
            byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(Block.State)).ToArray();
            foreach (ulong address in new[] { 0UL, 8UL, 0x0000800000000000UL, (ulong)f.State, (ulong)f.Pool, (ulong)f.Slot, (ulong)f.Data, Common, Notify, Config })
                HostCheck.Equal(Block.Status.InvalidStorage, Block.CopyRead(f.State, token, new Span<byte>((void*)address, 512)));
            HostCheck.Equal(Block.Status.InvalidStorage, Block.CopyRead(f.State, token, new byte[511]));
            HostCheck.True(calls == f.Hardware->Calls && f.State->ReadReady && new ReadOnlySpan<byte>(f.State, sizeof(Block.State)).SequenceEqual(state));
            byte[] result = Enumerable.Repeat((byte)0x81, 512).ToArray();
            if (mode == 0) { f.Hardware->Generation++; HostCheck.Equal(Block.Status.ConfigurationChanged, Block.CopyRead(f.State, token, result)); }
            else if (mode == 1) { *(ushort*)(f.Data + 150) = 2; HostCheck.Equal(Block.Status.InvalidCompletion, Block.CopyRead(f.State, token, result)); }
            else if (mode == 2)
            {
                ulong* generation = (ulong*)((byte*)f.Slot + ManagedOffset(f.Slot, "Generation")); (*generation)++;
                HostCheck.Equal(Block.Status.DmaRejected, Block.CopyRead(f.State, token, result)); (*generation)--;
            }
            else { f.Hardware->FailCall = calls + 4; HostCheck.Equal(Block.Status.OrderingFailed, Block.CopyRead(f.State, token, result)); }
            HostCheck.True(result.All(value => value == 0x81)); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); f.CheckGuards();
        }
    }

    [HostTest("writable queue IN cancellation revokes pending and held Ready copies without altering write frontiers")]
    private static void ReadCancellation()
    {
        foreach (bool ready in new[] { false, true })
        {
            using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); Read(f, 4, out var token);
            if (ready) { f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f, out _)); }
            HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
            if (!ready) { HostCheck.True(f.State->RequestPending); f.Complete(); HostCheck.Equal(Block.Status.Cancelled, Poll(f, out var cancelled)); HostCheck.True(cancelled.Cancelled); }
            byte[] result = Enumerable.Repeat((byte)0x91, 512).ToArray(); HostCheck.Equal(Block.Status.StaleToken, Block.CopyRead(f.State, token, result));
            HostCheck.True(result.All(value => value == 0x91) && !f.State->RequestPending && f.State->SubmittedBytes == 0 && f.State->CompletedBytes == 0 && f.State->DurableBytes == 0);
            HostCheck.Equal(Block.Status.Success, Read(f, 5, out var next)); HostCheck.True(next.Generation != token.Generation);
            f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f, out _)); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            HostCheck.True(f.State->Retired && f.Pool->AllocatedMask == 0); f.CheckGuards();
        }
    }

    [HostTest("writable queue mixed retained IN OUT FLUSH wraps ushort rings with no per-request allocation")]
    private static void MixedWrap()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); Span<byte> bytes = stackalloc byte[512]; Fill(bytes); bool good = true;
        long allocated = 0; ulong writes = 0;
        for (uint i = 0; i < 65538; i++)
        {
            if (i == 1) allocated = GC.GetAllocatedBytesForCurrentThread();
            bool read = i % 2 == 0;
            good &= (read ? Read(f, i, out _) : Write(f, i, bytes, out _)) == Block.Status.Success;
            // Token is copied from validated completion, never synthesized from counters.
            f.Complete(); good &= Poll(f, out var done) == (read ? Block.Status.Ready : Block.Status.Success);
            if (read) good &= Block.CopyRead(f.State, done.Token, bytes) == Block.Status.Success; else writes += 512;
        }
        HostCheck.True(good && allocated == GC.GetAllocatedBytesForCurrentThread() && f.State->CompletedBytes == writes && f.State->DurableBytes == 0);
        Flush(f, out _); f.Complete(); HostCheck.Equal(Block.Status.Success, Poll(f, out _)); HostCheck.Equal(writes, f.State->DurableBytes);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); f.CheckGuards();
    }

    [HostTest("writable queue every IN publication completion and copy callback error retains exact DMA ownership and atomic output")]
    private static void ReadCallbackFailures()
    {
        for (int phase = 0; phase < 3; phase++)
        {
            Block.RequestToken Prepare(Fixture f)
            {
                HostCheck.Equal(Block.Status.Success, f.Start()); Block.RequestToken token = default;
                if (phase != 0) { Read(f, 2, out token); f.Complete(); }
                if (phase == 2) HostCheck.Equal(Block.Status.Ready, Poll(f, out _));
                return token;
            }
            Block.Status Act(Fixture f, Block.RequestToken token, Span<byte> target) =>
                phase == 0 ? Read(f, 2, out _) : phase == 1 ? Poll(f, out _) : Block.CopyRead(f.State, token, target);
            uint count; byte[] output = new byte[512];
            using (var baseline = new Fixture())
            {
                var token = Prepare(baseline); uint start = baseline.Hardware->Calls;
                HostCheck.Equal(phase == 1 ? Block.Status.Ready : Block.Status.Success, Act(baseline, token, output));
                count = baseline.Hardware->Calls - start; HostCheck.Equal(Block.Status.Success, Block.Stop(baseline.State));
            }
            for (uint at = 1; at <= count; at++) foreach (bool after in new[] { false, true })
            {
                using var f = new Fixture(); var token = Prepare(f); output.AsSpan().Fill(0x93);
                f.Hardware->FailCall = f.Hardware->Calls + at; f.Hardware->FailAfterWrite = after;
                var result = Act(f, token, output);
                HostCheck.True(result is not (Block.Status.Success or Block.Status.Ready) && result == f.State->LastFailure && f.Hardware->FailureFired &&
                    f.Pool->AllocatedMask == 1 && f.Owner == DmaBufferOwnership.Device && !f.State->Running &&
                    f.State->SubmittedBytes == 0 && f.State->CompletedBytes == 0 && f.State->DurableBytes == 0);
                HostCheck.True(output.All(value => value == 0x93)); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); f.CheckGuards();
            }
        }
    }

    [HostTest("writable observation accumulates exact published OUT validated FLUSH and committed IN without callback authority")]
    private static void ObservationChain()
    {
        using var f = new Fixture(observe: true); HostCheck.Equal(Block.Status.Success, f.Start());
        byte[] input = new byte[512]; Fill(input); byte[] expected = input.ToArray();
        HostCheck.Equal(Block.Status.Success, Write(f, 3, input, out var write)); input.AsSpan().Clear();
        var o = f.Observation;
        HostCheck.True(o->Request.Owner == (ulong)f.State && o->Request.Epoch == f.Input.Epoch && o->Request.Generation == write.Generation &&
            o->Kind == Block.Operation.Write && o->Sector == 3 && o->Flags == Block.ObservationFlags.Published && o->PayloadBytes == 512 && o->CompletionBytes == 0 &&
            o->SubmittedCount == 1 && o->CompletedCount == 0 && o->AvailableIndex == 1 && o->ConsumedIndex == 0 && o->SubmittedBytes == 512 && o->CompletedBytes == 0 &&
            o->FlushAcknowledgedBytes == 0 && o->DeviceResult == Block.Status.Uninitialized && new ReadOnlySpan<byte>(o->Data, 512).SequenceEqual(expected));
        f.Complete(); HostCheck.Equal(Block.Status.Success, Poll(f, out _));
        HostCheck.True(o->Flags == (Block.ObservationFlags.Published | Block.ObservationFlags.CompletionValidated) && o->CompletionBytes == 512 && o->CompletedBytes == 512 &&
            o->CompletedCount == 1 && o->ConsumedIndex == 1 && o->DeviceResult == Block.Status.Success && o->FlushAcknowledgedBytes == 0);
        HostCheck.Equal(Block.Status.Success, Flush(f, out var flush));
        HostCheck.True(o->Kind == Block.Operation.Flush && o->Request.Generation == flush.Generation && o->PayloadBytes == 0 && o->CompletionBytes == 0 &&
            o->FlushTargetBytes == 512 && o->FlushAcknowledgedBytes == 0 && new ReadOnlySpan<byte>(o->Data, 512).IndexOfAnyExcept((byte)0) < 0);
        f.Complete(); HostCheck.Equal(Block.Status.Success, Poll(f, out _));
        HostCheck.True(o->FlushAcknowledgedBytes == 512 && o->DeviceResult == Block.Status.Success && o->CompletionBytes == 0);
        HostCheck.Equal(Block.Status.Success, Read(f, 3, out var read));
        f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f, out _));
        HostCheck.True(o->Request.Generation == read.Generation && o->Kind == Block.Operation.Read && o->CompletionBytes == 512 && o->PayloadBytes == 0 &&
            (o->Flags & Block.ObservationFlags.ReadCopied) == 0 && new ReadOnlySpan<byte>(o->Data, 512).IndexOfAnyExcept((byte)0) < 0);
        byte[] output = new byte[512]; HostCheck.Equal(Block.Status.Success, Block.CopyRead(f.State, read, output));
        HostCheck.True(output.AsSpan().SequenceEqual(expected) && new ReadOnlySpan<byte>(o->Data, 512).SequenceEqual(output) && o->PayloadBytes == 512 &&
            o->Flags == (Block.ObservationFlags.Published | Block.ObservationFlags.CompletionValidated | Block.ObservationFlags.ReadCopied));
        byte[] retained = new ReadOnlySpan<byte>(o, sizeof(Block.Observation)).ToArray();
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); HostCheck.True(new ReadOnlySpan<byte>(o, sizeof(Block.Observation)).SequenceEqual(retained));
    }

    [HostTest("writable observation admission and active source output aliases reject before any borrowed bytes change")]
    private static void ObservationAliases()
    {
        using var f = new Fixture(observe: true); f.Negotiate();
        foreach (ulong address in new[] { 8UL, 0x0000800000000000UL, 0x00007ffffffffff8UL, (ulong)f.Observation + 1,
            (ulong)f.State, (ulong)f.Pool, (ulong)f.Slot, (ulong)f.Data, Common, Notify, Config })
        {
            var a = f.Input; a.Observation = (Block.Observation*)address;
            uint calls = f.Hardware->Calls; HostCheck.Equal(Block.Status.InvalidAccess, Block.Start(f.State, a));
            HostCheck.True(calls == f.Hardware->Calls && new ReadOnlySpan<byte>(f.State, sizeof(Block.State)).IndexOfAnyExcept((byte)0) < 0 &&
                new ReadOnlySpan<byte>(f.Observation, sizeof(Block.Observation)).IndexOfAnyExcept((byte)0) < 0);
        }
        HostCheck.Equal(Block.Status.Success, Block.Start(f.State, f.Input));
        byte[] bytes = new byte[512]; Fill(bytes); Write(f, 3, bytes, out _);
        byte[] slot = new ReadOnlySpan<byte>(f.Observation, sizeof(Block.Observation)).ToArray(), state = new ReadOnlySpan<byte>(f.State, sizeof(Block.State)).ToArray(), dma = new ReadOnlySpan<byte>(f.Data, 4096).ToArray();
        uint prior = f.Hardware->Calls;
        HostCheck.Equal(Block.Status.InvalidStorage, Block.SubmitFlush(f.State, (Block.RequestToken*)f.Observation));
        Block.RequestToken token = default; HostCheck.Equal(Block.Status.InvalidStorage, Block.SubmitWrite(f.State, 3, new ReadOnlySpan<byte>(f.Observation->Data, 512), &token));
        HostCheck.Equal(Block.Status.InvalidStorage, Block.Poll(f.State, (Block.Completion*)f.Observation));
        HostCheck.True(prior == f.Hardware->Calls && new ReadOnlySpan<byte>(f.Observation, sizeof(Block.Observation)).SequenceEqual(slot) &&
            new ReadOnlySpan<byte>(f.State, sizeof(Block.State)).SequenceEqual(state) && new ReadOnlySpan<byte>(f.Data, 4096).SequenceEqual(dma));
        f.Complete(); Poll(f, out _); Read(f, 3, out token); f.Complete(); Poll(f, out _);
        HostCheck.Equal(Block.Status.InvalidStorage, Block.CopyRead(f.State, token, new Span<byte>(f.Observation->Data, 512)));
        HostCheck.True(f.State->ReadReady && f.Observation->PayloadBytes == 0); Block.Cancel(f.State, token); Block.Stop(f.State);
    }

    [HostTest("writable observation cancelled and failed IN never reports copied bytes or hides wire completion")]
    private static void ObservationReadFailures()
    {
        for (int mode = 0; mode < 5; mode++)
        {
            using var f = new Fixture(observe: true); f.Start(); Read(f, 3, out var token);
            if (mode == 0) Block.Cancel(f.State, token);
            f.Complete(length: mode == 3 ? 512U : mode == 4 ? 1U : 513U, status: mode == 4 ? (byte)1 : (byte)0);
            var status = Poll(f, out _);
            if (mode < 3 && mode != 0) HostCheck.Equal(Block.Status.Ready, status);
            if (mode == 1) Block.Cancel(f.State, token);
            if (mode == 2)
            {
                ulong* generation = (ulong*)((byte*)f.Slot + ManagedOffset(f.Slot, "Generation")); ulong prior = *generation; (*generation)++;
                byte[] output = Enumerable.Repeat((byte)0x71, 512).ToArray();
                HostCheck.Equal(Block.Status.DmaRejected, Block.CopyRead(f.State, token, output)); *generation = prior;
                HostCheck.True(output.All(value => value == 0x71));
            }
            HostCheck.True(f.Observation->PayloadBytes == 0 && (f.Observation->Flags & Block.ObservationFlags.ReadCopied) == 0 &&
                new ReadOnlySpan<byte>(f.Observation->Data, 512).IndexOfAnyExcept((byte)0) < 0);
            if (mode == 3) HostCheck.True(status == Block.Status.InvalidCompletion && f.Observation->Flags == Block.ObservationFlags.Published && f.Observation->CompletedCount == 0);
            else HostCheck.True((f.Observation->Flags & Block.ObservationFlags.CompletionValidated) != 0 && f.Observation->CompletedCount == 1 &&
                f.Observation->DeviceResult == (mode == 4 ? Block.Status.DeviceIoError : Block.Status.Success));
            if (mode < 2) HostCheck.True(f.Observation->Cancelled);
            Block.Stop(f.State);
        }
    }

    [HostTest("writable observation captures partial failed publication without fabricated completion or flush acknowledgment")]
    private static void ObservationPublicationFailure()
    {
        byte[] bytes = new byte[512]; Fill(bytes); uint count;
        using (var good = new Fixture(observe: true))
        { good.Start(); uint before = good.Hardware->Calls; Write(good, 3, bytes, out _); count = good.Hardware->Calls - before; Block.Stop(good.State); }
        bool sawUnpublished = false, sawPublished = false;
        for (uint at = 1; at <= count; at++) foreach (bool after in new[] { false, true })
        {
            using var f = new Fixture(observe: true); f.Start(); f.Hardware->FailCall = f.Hardware->Calls + at; f.Hardware->FailAfterWrite = after;
            HostCheck.True(Write(f, 3, bytes, out _) != Block.Status.Success && f.State->LastFailure != Block.Status.Success);
            if (f.State->Submitted == 0)
            { sawUnpublished = true; HostCheck.True(f.Observation->Flags == Block.ObservationFlags.None && f.Observation->Request.Generation == 0); }
            else
            {
                sawPublished = true; HostCheck.True(f.Observation->Flags == Block.ObservationFlags.Published && f.Observation->SubmittedCount == 1 &&
                    f.Observation->PayloadBytes == 512 && new ReadOnlySpan<byte>(f.Observation->Data, 512).SequenceEqual(bytes) && f.State->Dirty);
            }
            HostCheck.True(f.Observation->CompletedCount == 0 && f.Observation->FlushAcknowledgedBytes == 0 && f.State->Completed == 0 && f.State->DurableBytes == 0);
            byte[] receipt = new ReadOnlySpan<byte>(f.Observation, sizeof(Block.Observation)).ToArray(); Block.Stop(f.State);
            HostCheck.True(new ReadOnlySpan<byte>(f.Observation, sizeof(Block.Observation)).SequenceEqual(receipt));
        }
        HostCheck.True(sawUnpublished && sawPublished);
    }

    [HostTest("writable observation FLUSH wire failure and cancellation preserve actual acknowledged frontier only")]
    private static void ObservationFlushFailure()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var f = new Fixture(observe: true); f.Start(); byte[] bytes = new byte[512]; Write(f, 3, bytes, out _); f.Complete(); Poll(f, out _);
            Flush(f, out var token); Block.Cancel(f.State, token); f.Complete(status: fail ? (byte)1 : (byte)0);
            HostCheck.Equal(fail ? Block.Status.DeviceIoError : Block.Status.Cancelled, Poll(f, out _));
            HostCheck.True(f.Observation->Kind == Block.Operation.Flush && f.Observation->FlushTargetBytes == 512 && f.Observation->Cancelled &&
                f.Observation->FlushAcknowledgedBytes == (fail ? 0UL : 512UL) && f.Observation->DeviceResult == (fail ? Block.Status.DeviceIoError : Block.Status.Success) &&
                f.Observation->PayloadBytes == 0 && f.Observation->CompletionBytes == 0 && new ReadOnlySpan<byte>(f.Observation->Data, 512).IndexOfAnyExcept((byte)0) < 0);
            Block.Stop(f.State);
        }
    }

    [HostTest("writable optional observation preserves null behavior callback counts allocation and ushort ring reuse")]
    private static void ObservationDefaultAndWrap()
    {
        using var plain = new Fixture(); using var observed = new Fixture(observe: true); plain.Start(); observed.Start();
        byte[] bytes = new byte[512]; Fill(bytes); ulong previous = 0; uint calls0 = plain.Hardware->Calls, calls1 = observed.Hardware->Calls;
        long allocated = GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (int i = 0; i < 65538; i++)
        {
            good &= Write(plain, 3, bytes, out _) == Block.Status.Success && Write(observed, 3, bytes, out _) == Block.Status.Success;
            plain.Complete(); observed.Complete(); good &= Poll(plain, out _) == Block.Status.Success && Poll(observed, out _) == Block.Status.Success;
            good &= observed.Observation->Request.Generation == ++previous && observed.Observation->AvailableIndex == unchecked((ushort)previous) &&
                observed.Observation->ConsumedIndex == unchecked((ushort)previous) && observed.Observation->CompletedCount == previous;
        }
        HostCheck.True(good && allocated == GC.GetAllocatedBytesForCurrentThread() && plain.Hardware->Calls - calls0 == observed.Hardware->Calls - calls1 &&
            plain.State->SubmittedBytes == observed.State->SubmittedBytes && plain.State->CompletedBytes == observed.State->CompletedBytes && plain.State->DurableBytes == observed.State->DurableBytes);
        Block.Stop(plain.State); Block.Stop(observed.State);
    }

    [HostTest("writable observation engine Pump preserves every generation and accumulates same-turn IN completion plus copy")]
    private static void ObservationEnginePump()
    {
        using var f = new ContinuingFat32MutationTests.Fixture(observe: true); f.Record = false;
        var o = f.Device.Observation; ulong generation = 0; Block.ObservationFlags lastFlags = 0; Block.Operation lastKind = default; bool sawReadCopy = false, sawWrite = false, sawFlush = false;
        CStatusDrive();
        void Sample()
        {
            if (o->Flags == Block.ObservationFlags.None) return;
            if (o->Request.Generation != generation)
            {
                HostCheck.True(o->Request.Generation == generation + 1 && (generation == 0 || (lastFlags & Block.ObservationFlags.CompletionValidated) != 0 &&
                    (lastKind != Block.Operation.Read || (lastFlags & Block.ObservationFlags.ReadCopied) != 0)));
                generation = o->Request.Generation;
            }
            else HostCheck.True((o->Flags & lastFlags) == lastFlags);
            if ((o->Flags & Block.ObservationFlags.ReadCopied) != 0)
            {
                sawReadCopy = true; HostCheck.True(o->PayloadBytes == 512 && o->CompletionBytes == 512 &&
                    new ReadOnlySpan<byte>(o->Data, 512).SequenceEqual(f.Volatile.Slice(checked((int)o->Sector * 512), 512)));
            }
            if (o->Kind == Block.Operation.Write) HostCheck.True(o->PayloadBytes == 512 && new ReadOnlySpan<byte>(o->Data, 512).SequenceEqual(new ReadOnlySpan<byte>(f.Device.Data + 512, 512)));
            sawWrite |= o->Kind == Block.Operation.Write; sawFlush |= o->Kind == Block.Operation.Flush;
            lastFlags = o->Flags; lastKind = o->Kind;
        }
        OS.Storage.ContinuingFat32Mutation.Status Drive()
        {
            for (int i = 0; i < 100000; i++) { var result = f.Turn(); Sample(); if (result != OS.Storage.ContinuingFat32Mutation.Status.Pending) return result; }
            throw new InvalidOperationException("observation engine did not terminate");
        }
        void CStatusDrive()
        {
            HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.Pending, OS.Storage.ContinuingFat32Mutation.Start(f.State, f.Input));
            HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.Ready, Drive());
            OS.Storage.ContinuingFat32Mutation.RequestToken request = default; OS.Storage.ContinuingFat32Mutation.FileToken file = default;
            HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.Pending, OS.Storage.ContinuingFat32Mutation.BeginOpenRoot(f.State, "DATA.BIN", &request));
            HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.Ready, Drive()); OS.Storage.ContinuingFat32Mutation.CopyOpen(f.State, request, &file);
            byte[] bytes = Enumerable.Range(0, 37).Select(i => (byte)(i + 81)).ToArray();
            HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.Pending, OS.Storage.ContinuingFat32Mutation.BeginOverwrite(f.State, file, 500, bytes, &request));
            HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.Ready, Drive()); OS.Storage.ContinuingFat32Mutation.Result result = default;
            HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.Success, OS.Storage.ContinuingFat32Mutation.CopyResult(f.State, request, &result));
            HostCheck.True(result.Bytes == 37 && sawReadCopy && sawWrite && sawFlush && generation == f.Device.State->Submitted && generation == f.Device.State->Completed);
            HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.Success, OS.Storage.ContinuingFat32Mutation.Stop(f.State));
        }
    }

    private delegate bool BorrowObservation(OS.Storage.ContinuingFat32Mutation.State* state, ulong epoch, ulong address, ulong bytes);
    [HostTest("writable observation POD sizes keep state receipt pool and slot within one supervisor metadata page")]
    private static void ObservationLayout()
    {
        HostCheck.True(!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<Block.State>() &&
            !System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<Block.Admission>() &&
            !System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<Block.Observation>() && sizeof(Block.Observation) <= 1024 &&
            sizeof(Block.State) + sizeof(OS.Storage.ContinuingFat32Mutation.State) + sizeof(DmaBufferPool) + sizeof(DmaBufferSlot) + sizeof(Block.Observation) <= 4096);
        Console.WriteLine($"WRITABLE_OBSERVATION_LAYOUT state={sizeof(Block.State)} admission={sizeof(Block.Admission)} observation={sizeof(Block.Observation)} engine={sizeof(OS.Storage.ContinuingFat32Mutation.State)} pool={sizeof(DmaBufferPool)} slot={sizeof(DmaBufferSlot)}");
    }

    [HostTest("writable observation extent cannot alias continuing engine admission output or provider borrow")]
    private static void ObservationEngineAliases()
    {
        using var f = new ContinuingFat32MutationTests.Fixture(observe: true);
        var a = f.Input; a.Sector = (byte*)f.Device.Observation; byte[] before = new ReadOnlySpan<byte>(f.Device.Observation, sizeof(Block.Observation)).ToArray();
        HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.InvalidStorage, OS.Storage.ContinuingFat32Mutation.Start(f.State, a));
        HostCheck.True(new ReadOnlySpan<byte>(f.Device.Observation, sizeof(Block.Observation)).SequenceEqual(before)); f.Mount();
        var borrow = typeof(OS.Storage.ContinuingFat32Mutation).GetMethod("CanBorrowStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<BorrowObservation>();
        HostCheck.True(!borrow(f.State, f.Input.Epoch, (ulong)f.Device.Observation, 8));
        HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.InvalidStorage, OS.Storage.ContinuingFat32Mutation.BeginOpenRoot(f.State, "DATA.BIN", (OS.Storage.ContinuingFat32Mutation.RequestToken*)f.Device.Observation));
        HostCheck.Equal(OS.Storage.ContinuingFat32Mutation.Status.Success, OS.Storage.ContinuingFat32Mutation.Stop(f.State));
    }


    private static bool Enter(Hardware* h, bool write = false)
    { h->Calls++; if (h->Calls != h->FailCall) return true; h->FailureFired = true; return write && h->FailAfterWrite; }
    private static byte Exit(Hardware* h) => h->Calls == h->FailCall ? (byte)0 : (byte)1;
    [UnmanagedCallersOnly] private static byte Interrupt(void* c, VirtioMappedRegion common) => 0;
    [UnmanagedCallersOnly] private static byte Read8(void* c, ulong address, byte* value)
    {
        var h = (Hardware*)c; if (!Enter(h)) return 0;
        if (address == Common + 20) { *value = h->Status; if (h->ResetRequested && *value == 0) h->ResetObserved = true; }
        else if (address == Common + 21) { h->GenerationReads++; *value = h->GenerationChurn ? (byte)h->GenerationReads : h->Generation; }
        else if (address == (ulong)h->Data + 272) { if (!h->Acquired) h->BadOrder = true; *value = h->Data[272]; }
        else return 0;
        return 1;
    }
    [UnmanagedCallersOnly] private static byte Write8(void* c, ulong address, byte value)
    {
        var h = (Hardware*)c; if (!Enter(h, true) || address != Common + 20) return 0;
        if (value == 15)
        {
            if (!h->BusEnabled || h->Enabled != 1 || h->ConfigVector != ushort.MaxValue || h->QueueVector != ushort.MaxValue) h->BadOrder = true;
            h->Status = 15; h->DriverOkWrites++;
        }
        else if (value == 0)
        {
            h->ResetRequested = true; h->ResetObserved = false;
            if (h->ResetWriteFailure) return 0;
            h->Resets++; if (!h->ResetStuck) { h->Status = 0; h->Enabled = 0; h->Pending = false; }
        }
        else if (value is 1 or 3 or 11 || (value & 128) != 0)
        {
            h->Status = value == 11 && h->RejectFeatures ? (byte)3 : value;
            h->ResetRequested = false; h->ResetObserved = false;
        }
        else return 0;
        return Exit(h);
    }
    [UnmanagedCallersOnly] private static byte Read16(void* c, ulong address, ushort* value)
    {
        var h = (Hardware*)c; if (!Enter(h)) return 0;
        if (address == (ulong)h->Data + 150) { *value = *(ushort*)(h->Data + 150); h->IndexObserved = true; h->Acquired = false; return 1; }
        switch (address - Common)
        {
            case 16: *value = h->ConfigVector; break; case 18: *value = 1; break;
            case 22: *value = h->Selected; break; case 24: *value = h->Size == 0 ? (ushort)16 : h->Size; break;
            case 26: *value = h->QueueVector; break; case 28: *value = h->Enabled; break; case 30: *value = 3; break;
            default: return 0;
        }
        return 1;
    }
    [UnmanagedCallersOnly] private static byte Write16(void* c, ulong address, uint value)
    {
        var h = (Hardware*)c; if (!Enter(h, true)) return 0;
        if (address == Notify + 12 && value == 0)
        {
            ushort index = *(ushort*)(h->Data + 130);
            if (!h->BusEnabled || h->Status != 15 || h->Enabled != 1 || h->Pending ||
                h->Pool->AllocatedMask != 1 || index != unchecked((ushort)(h->AvailableSeen + 1)) || h->FencedIndex != index ||
                *(ushort*)(h->Data + 132 + (h->AvailableSeen & 7) * 2) != 0 || *(ushort*)(h->Data + 128) != 1) h->BadOrder = true;
            h->Type = *(uint*)(h->Data + 256);
            bool write = h->Type == 1, read = h->Type == 0;
            if (!write && !read && h->Type != 4 || *(uint*)(h->Data + 260) != 0 || h->Data[272] != 255) h->BadOrder = true;
            int descriptors = write || read ? 3 : 2;
            for (int i = 0; i < descriptors; i++)
            {
                byte* p = h->Data + i * 16;
                bool payload = (write || read) && i == 1, status = i == descriptors - 1;
                if (*(ulong*)p != DeviceBase + (i == 0 ? 256UL : payload ? 512UL : 272UL) ||
                    *(uint*)(p + 8) != (i == 0 ? 16U : payload ? 512U : 1U) ||
                    *(ushort*)(p + 12) != (status ? 2 : payload && read ? 3 : 1) || *(ushort*)(p + 14) != (status ? 0 : i + 1)) h->BadOrder = true;
            }
            if (!write && !read && (*(ulong*)(h->Data + 264) != 0 || new ReadOnlySpan<byte>(h->Data + 32, 16).IndexOfAnyExcept((byte)0) >= 0)) h->BadOrder = true;
            h->Sector = *(ulong*)(h->Data + 264); h->AvailableSeen = index; h->Notifies++; h->Pending = true;
            return Exit(h);
        }
        switch (address - Common)
        {
            case 16: h->ConfigVector = (ushort)value; break; case 22: h->Selected = (ushort)value; break;
            case 24: h->Size = (ushort)value; break; case 26: h->QueueVector = (ushort)value; break;
            case 28:
                if (h->BusEnabled || h->Descriptor != DeviceBase || h->Available != DeviceBase + 128 || h->Used != DeviceBase + 148) h->BadOrder = true;
                h->Enabled = (ushort)value; break;
            default: return 0;
        }
        return Exit(h);
    }
    [UnmanagedCallersOnly] private static byte Read32(void* c, ulong address, uint* value)
    {
        var h = (Hardware*)c; if (!Enter(h)) return 0;
        if (address == Common + 4) { *value = (uint)(h->Offered >> (int)(h->FeatureSelect * 32)); return 1; }
        if (address == Config) { *value = (uint)h->Capacity; return 1; }
        if (address == Config + 4) { *value = (uint)(h->Capacity >> 32); return 1; }
        if (address >= (ulong)h->Data + 152 && address < (ulong)h->Data + 216 && (address & 3) == 0)
        { if (!h->Acquired) h->BadOrder = true; *value = *(uint*)address; return 1; }
        ulong offset = address - Common;
        if (offset < 32 || offset > 52 || (offset & 3) != 0) return 0;
        ulong bits = offset < 40 ? h->Descriptor : offset < 48 ? h->Available : h->Used;
        *value = (uint)(bits >> ((offset & 4) == 0 ? 0 : 32)); return 1;
    }
    [UnmanagedCallersOnly] private static byte Write32(void* c, ulong address, uint value)
    {
        var h = (Hardware*)c; if (!Enter(h, true)) return 0; ulong offset = address - Common;
        if (offset == 0) { h->FeatureSelect = value; return Exit(h); }
        if (offset == 8) { h->DriverSelect = value; return Exit(h); }
        if (offset == 12) { int featureShift = (int)(h->DriverSelect * 32); h->Chosen = (h->Chosen & ~(0xffffffffUL << featureShift)) | ((ulong)value << featureShift); return Exit(h); }
        if (offset < 32 || offset > 52 || (offset & 3) != 0) return 0;
        if (h->BusEnabled || h->Enabled != 0) h->BadOrder = true;
        ulong* target = offset < 40 ? &h->Descriptor : offset < 48 ? &h->Available : &h->Used;
        int shift = (offset & 4) == 0 ? 0 : 32; *target = (*target & ~(0xffffffffUL << shift)) | ((ulong)value << shift); return Exit(h);
    }
    [UnmanagedCallersOnly] private static byte Fence(void* c)
    {
        var h = (Hardware*)c; if (!Enter(h)) return 0; h->Fences++;
        if (h->ResetRequested)
        {
            if (h->ResetFenceFailure) return 0;
            if (!h->ResetObserved || h->BusEnabled) h->BadOrder = true;
            if (h->Pool->AllocatedMask == 1) h->HeldAtResetFence = true;
        }
        if (h->IndexObserved) h->Acquired = true;
        h->FencedIndex = *(ushort*)(h->Data + 130); return 1;
    }
    [UnmanagedCallersOnly] private static byte BusOn(void* c)
    { var h = (Hardware*)c; if (!Enter(h, true)) return 0; h->BusEnabled = true; return Exit(h); }
    [UnmanagedCallersOnly] private static byte BusOff(void* c)
    { var h = (Hardware*)c; if (!Enter(h, true) || h->DisableFailure) return 0; h->BusEnabled = false; return Exit(h); }
    [UnmanagedCallersOnly] private static byte Clock(void* c, ulong* value)
    { var h = (Hardware*)c; if (!Enter(h) || h->ClockFailure) return 0; *value = h->Now; return 1; }

}
