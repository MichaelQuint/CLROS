using System.Buffers.Binary;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxSignalProofFixture;

internal static unsafe class LinuxSignalEpollWaitProofFixtureTests
{
    [HostTest("m16-020 epoll race package has exact canonical identity and two-thread mechanical layout")]
    private static void Package()
    {
        HostCheck.Equal("SignalEpollStolen",F.EpollStolenWaitVariant);
        HostCheck.Equal(1,F.Variants.Count(v=>v=="SignalEpollStolen"));
        HostCheck.True(F.IsWaitVariant("SignalEpollStolen")&&!F.IsWaitVariant("signalepollstolen"));
        byte[] elf=F.CreateElf("SignalEpollStolen"),package=F.CreatePackage("SignalEpollStolen");
        HostCheck.Equal(40960,elf.Length);HostCheck.Equal(36864UL,U64(elf,152));HostCheck.Equal(36864UL,U64(elf,160));
        HostCheck.Equal("SignalEpollStolen",F.PackageVariant(package));
        HostCheck.True(package.AsSpan().SequenceEqual(F.CreatePackage("SignalEpollStolen")));
        HostCheck.Equal(42U,F.ExpectedExitCode("SignalEpollStolen"));
        var view=LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded&&view.PayloadBytes==40960&&view.Manifest.Threads==2&&view.Manifest.Descriptors==16);
        HostCheck.True(view.Manifest.Ipv4==0&&view.Manifest.Port==8080);
        fixed(byte* p=package)
        {
            HostCheck.Equal(LinuxElfLoadStatus.Ready,LinuxElfLoader.Validate(p+view.PayloadOffset,view.PayloadBytes,
                view.Manifest.MemoryBytes,view.Manifest.StackBytes,out var plan));
            HostCheck.True(plan.ImageBytes==40960&&plan.LoadCount==2&&plan.Entry==0x400200);
            HostCheck.True(LinuxUserInterruptPolicy.TryRead(p+view.ManifestOffset,view.ManifestBytes,view.Manifest,out bool irq)&&!irq);
        }
        using MemoryStream input=new(package);F.RequirePackage(input);HostCheck.Equal(0L,input.Position);
        byte[] altered=(byte[])package.Clone();altered[checked((int)view.PayloadOffset)+0x11e4]^=1;
        Reject(altered);Reject(package[..^1]);
    }

    [HostTest("m16-020 epoll race byte inventory binds every syscall argument and child write signal drain order")]
    private static void Instructions()
    {
        byte[] elf=F.CreateElf("SignalEpollStolen");var calls=F.ExpectedWaitInvocations("SignalEpollStolen");
        Expected[] expected=[
            new(39),new(186),new(158,0x1002,0x402000),new(13,10,0x401100,0,8),new(131,0x401120),
            new(293,0x4011c0),new(291),new(233,5,1,3,0x4011e0),new(56,0x7d0f00,0x406000,0x4011b8,0x4011bc,0x403000),
            new(232,5,0x409020,1,ulong.MaxValue),
            new(39),new(186),new(1,4,0x407000,1,Fd:true),new(234,1,1,10),new(0,3,0x409010,1,Fd:true),new(60,7),
            new(186),new(14,2,0,0x401158,8),new(131,0,0x4011a0),new(1,1,0x4001a0,15),new(15),
            new(1,4,0x407000,1,Fd:true),new(232,5,0x409020,1,ulong.MaxValue),new(0,3,0x409000,1,Fd:true),
            new(14,2,0,0x401150,8),new(131,0,0x401180),new(3,5),new(3,3),new(3,4),new(1,1,0x400180,15),new(231,42)];
        HostCheck.Equal(expected.Length,calls.Count);
        HostCheck.True(F.ExpectedInvocations("SignalEpollStolen").Select(c=>(c.Number,c.ReturnRip,c.Handler))
            .SequenceEqual(calls.Select(c=>(c.Number,c.ReturnRip,c.Handler))));
        for(int i=0;i<calls.Count;i++)
        {
            var call=calls[i];var want=expected[i];int end=checked((int)(call.ReturnRip-0x400000));
            int start=end-(want.Number==15?7:want.Fd?46:want.Number==232?50:40);
            HostCheck.Equal(want.Number,call.Number);
            HostCheck.True(elf[start]==0xb8&&U32(elf,start+1)==want.Number&&elf.AsSpan(end-2,2).SequenceEqual(new byte[]{15,5}));
            HostCheck.Equal(i is >=10 and <16,call.Child);HostCheck.Equal(i is >=16 and <21,call.Handler);
            HostCheck.True(start>=(call.Child?0x800:call.Handler?want.Number==15?0xe00:0xa00:0x200));
            HostCheck.True(end<=(call.Child?0xa00:call.Handler?want.Number==15?0xf00:0xe00:0x800));
            if(want.Number==15)continue;
            HostCheck.True(elf[start+5]==0xbf&&elf[start+10]==0xbe&&elf[start+15]==0xba);
            HostCheck.Equal(want.Fd?0U:checked((uint)want.A),U32(elf,start+6));
            HostCheck.Equal(checked((uint)want.B),U32(elf,start+11));HostCheck.Equal(checked((uint)want.C),U32(elf,start+16));
            HostCheck.True(elf.AsSpan(start+20,2).SequenceEqual(new byte[]{0x41,0xba})&&
                elf.AsSpan(start+26,2).SequenceEqual(new byte[]{0x41,0xb8})&&elf.AsSpan(start+32,2).SequenceEqual(new byte[]{0x41,0xb9}));
            HostCheck.Equal(want.Number==232?0U:checked((uint)want.D),U32(elf,start+22));
            HostCheck.Equal(checked((uint)want.E),U32(elf,start+28));HostCheck.Equal(0U,U32(elf,start+34));
            if(want.Fd)
            {
                HostCheck.True(elf.AsSpan(end-8,2).SequenceEqual(new byte[]{0x8b,0x3d}));
                HostCheck.Equal(want.A==3?0x11c0:0x11c4,end-2+BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(end-6)));
            }
            if(want.Number==232)
            {
                HostCheck.True(elf.AsSpan(start+38,2).SequenceEqual(new byte[]{0x49,0xba}));
                HostCheck.Equal(ulong.MaxValue,U64(elf,start+40));
            }
        }
        HostCheck.Equal(calls.Count,calls.Select(c=>c.ReturnRip).Distinct().Count());
    }

    [HostTest("m16-020 epoll race handler checks real EINTR context and disjoint packed event output")]
    private static void ContextAndOutputs()
    {
        byte[] elf=F.CreateElf("SignalEpollStolen");var calls=F.ExpectedWaitInvocations("SignalEpollStolen");
        var blocked=calls.First(c=>c.Number==232);
        HostCheck.Equal(0x400a00UL,U64(elf,0x1100));HostCheck.Equal(0x0c000004UL,U64(elf,0x1108));HostCheck.Equal(0x400e00UL,U64(elf,0x1110));
        HostCheck.True(U64(elf,0x1120)==0x406000&&U64(elf,0x1130)==4096&&U64(elf,0x2000)==0x13579bdf&&U64(elf,0x3000)==0x2468ace0);
        (uint Field,long Value)[] saved=[(144,-4),(168,(long)blocked.ReturnRip),(160,0x405000),(104,5),(112,0x409020),(136,1),(56,-1)];
        foreach(var field in saved)
        {
            byte[] check=[0x48,0x8b,0x82,0,0,0,0,0x48,0x3d,0,0,0,0];
            BinaryPrimitives.WriteUInt32LittleEndian(check.AsSpan(3),field.Field);
            BinaryPrimitives.WriteInt32LittleEndian(check.AsSpan(9),checked((int)field.Value));
            HostCheck.True(elf.AsSpan(0xa00,0x400).IndexOf(check)>=0);
        }
        HostCheck.True(elf.AsSpan(0xa00,0x400).IndexOf(new byte[]{0xbb,0x10,0x32,0x54,0x76})>=0&&
            elf.AsSpan(0x200,0x600).IndexOf(new byte[]{0x89,0xd8,0x48,0x3d,0xdf,0x9b,0x57,0x13})>=0);
        byte[] eventInput=elf.AsSpan(0x11e0,12).ToArray();
        HostCheck.Equal(1U,U32(eventInput,0));HostCheck.Equal(0x12345678UL,U64(eventInput,4));
        HostCheck.True(elf.AsSpan(0x9000,4096).IndexOfAnyExcept((byte)0)<0);
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(0x11c0),3);BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(0x11c4),4);
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(0x11b8),2);BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(0x11bc),1);
        byte[] controls=elf.AsSpan(0x11b8,32).ToArray();
        foreach(var call in calls.Where(c=>c.Number is 14 or 131))
        {
            int start=checked((int)(call.ReturnRip-0x400000))-40;
            uint address=U32(elf,start+(call.Number==14?16:11));if(address==0)continue;
            int output=checked((int)(address-0x400000));
            if(call.Number==14)BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(output),call.Handler?512UL:0);
            else
            {
                BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(output),0x406000);
                BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(output+8),call.Handler?1UL:0);
                BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(output+16),4096);
            }
        }
        eventInput.CopyTo(elf,0x9020);elf[0x9000]=0x5a;elf[0x9010]=0x5a;
        HostCheck.True(controls.AsSpan().SequenceEqual(elf.AsSpan(0x11b8,32))&&eventInput.AsSpan().SequenceEqual(elf.AsSpan(0x11e0,12)));
        HostCheck.Equal((byte)0x5a,elf[0x9000]);HostCheck.Equal((byte)0x5a,elf[0x9010]);
        HostCheck.True(elf.AsSpan(0x9020,12).SequenceEqual(eventInput));
    }

    private readonly record struct Expected(uint Number,ulong A=0,ulong B=0,ulong C=0,ulong D=0,ulong E=0,bool Fd=false);
    private static uint U32(byte[] bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static ulong U64(byte[] bytes,int at)=>BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(at));
    private static void Reject(byte[] bytes)
    {bool rejected=false;try{F.PackageVariant(bytes);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);}
}
