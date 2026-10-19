using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;

public static partial class LinuxSignalProofFixture
{
    /// <summary>Canonical two-thread epoll readiness race. The child writes,
    /// directs a signal, then consumes readiness before the parent resumes.
    /// Package identity and instruction inventories grant no guest authority.</summary>
    public const string EpollStolenWaitVariant = "SignalEpollStolen";
    // Packed epoll_event is twelve bytes. Neither output aliases signal control,
    // either TLS/stack, the immutable event input, or the other thread's read.
    private const int EpollEvent = 0x11e0, EpollChildRead = 0x9010, EpollOutput = 0x9020;

    private static (byte[] Elf,Invocation[] Calls,WaitInvocation[] Steps) BuildEpollStolenWait()
    {
        byte[] elf=new byte[WaitElfBytes]; LinuxThreadProofFixture.CreateMechanicalImage(Witness).CopyTo(elf,0);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(152),0x9000);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(160),0x9000);
        Encoding.ASCII.GetBytes(HandlerWitness).CopyTo(elf,HandlerWitnessOffset);
        void U64(int at,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at),value);
        U64(ActionOffset,Address(HandlerOffset));U64(ActionOffset+8,0x0c000004);U64(ActionOffset+16,Address(RestorerOffset));
        U64(StackOffset,AlternateBase);U64(StackOffset+16,4096);
        U64(0x2000,0x13579bdf);U64(0x3000,0x2468ace0);
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(EpollEvent),1); // EPOLLIN, level triggered.
        U64(EpollEvent+4,0x12345678);
        elf[WaitPayload]=0x5a;
        Writer p=new(elf,EntryOffset),c=new(elf,WaitChildOffset),h=new(elf,HandlerOffset),r=new(elf,RestorerOffset);
        List<Invocation> parent=[],child=[],handler=[];
        void Call(Writer w,List<Invocation> list,uint nr,ulong a=0,ulong b=0,ulong n=0,ulong d=0,ulong e=0)
        {w.Call(nr,a,b,n,d,e);list.Add(new(nr,w.Rips[^1],ReferenceEquals(w,h)));}
        void Fd(Writer w,List<Invocation> list,uint nr,bool read,int buffer)
        {w.FdCall(nr,WaitPair+(read?0:4),Address(buffer),1);list.Add(new(nr,w.Rips[^1],false));}
        void EpollWait()
        {
            // RAX=232, RDI=5, RSI=output, RDX=1, R10=-1, R8=R9=0.
            // Writer's normal arguments zero-extend uint immediates; this timeout
            // deliberately uses MOV R10,imm64 to retain the full signed value.
            p.Arguments(232,5,Address(EpollOutput),1,0,0,0);
            p.Op(0xba49,2);p.Op(ulong.MaxValue,8);p.Finish();
            parent.Add(new(232,p.Rips[^1],false));
        }
        p.Immediate(4,(uint)ParentStackTop);p.Store32(WaitClearTid,1);
        Call(p,parent,39);p.Compare(1);p.StoreRax(PidOffset);Call(p,parent,186);p.Compare(1);
        Call(p,parent,158,0x1002,ParentTls);p.Compare(0);
        Call(p,parent,13,10,Address(ActionOffset),0,8);p.Compare(0);
        Call(p,parent,131,Address(StackOffset));p.Compare(0);
        Call(p,parent,293,Address(WaitPair));p.Compare(0);p.Memory(WaitPair,3);p.Memory(WaitPair+4,4);
        Call(p,parent,291);p.Compare(5);
        Call(p,parent,233,5,1,3,Address(EpollEvent));p.Compare(0);
        Call(p,parent,56,0x7d0f00,0x406000,Address(WaitTid),Address(WaitClearTid),0x403000);
        p.Op(0xc08548,3);p.Branch(0x84,WaitChildOffset);p.Compare(2);p.Memory(WaitTid,2);
        p.Immediate(3,0x13579bdf);EpollWait();
        int blocked=parent.Count-1;ulong waitRip=parent[blocked].ReturnRip;
        p.Compare(-4);p.Op(0xd889,2);p.Compare(0x13579bdf);p.Memory(CountOffset,1);p.Memory(WaitClearTid,0);
        Fd(p,parent,1,false,WaitPayload);p.Compare(1);EpollWait();p.Compare(1);
        p.Memory(EpollOutput,1);p.Memory(EpollOutput+4,0x12345678,true);
        Fd(p,parent,0,true,WaitRead);p.Compare(1);p.Memory(WaitRead,0x5a);
        Call(p,parent,14,2,0,Address(MaskOut),8);p.Compare(0);p.Memory(MaskOut,0,true);
        Call(p,parent,131,0,Address(StackOut));p.Compare(0);p.Memory(StackOut+8,0);
        Call(p,parent,3,5);p.Compare(0);Call(p,parent,3,3);p.Compare(0);Call(p,parent,3,4);p.Compare(0);
        Call(p,parent,1,1,Address(WitnessOffset),15);p.Compare(15);Call(p,parent,231,42);p.JumpFailure();
        if(p.Position>WaitChildOffset)throw new ToolFailure("m16_020_epoll_parent_overlap");

        c.Fs();c.Compare(0x2468ace0);Call(c,child,39);c.Compare(1);Call(c,child,186);c.Compare(2);
        Fd(c,child,1,false,WaitPayload);c.Compare(1);
        Call(c,child,234,1,1,10);c.Compare(0);
        Fd(c,child,0,true,EpollChildRead);c.Compare(1);c.Memory(EpollChildRead,0x5a);
        Call(c,child,60,7);c.JumpFailure();
        if(c.Position>HandlerOffset)throw new ToolFailure("m16_020_epoll_child_overlap");

        h.Op(0xf88948,3);h.Compare(10);h.Op(0x068b,2);h.Compare(10);h.Op(0x08466348,4);h.Compare(-6);h.Op(0x10468b,3);h.Compare(1);
        h.Op(0x028b48,3);h.Compare(6);
        void Context(uint at,long value){h.Op(0x828b48,3);h.Op(at,4);h.Compare(value);}
        Context(144,-4);Context(168,(long)waitRip);Context(160,(long)ParentStackTop);
        Context(104,5);Context(112,(long)Address(EpollOutput));Context(136,1);Context(56,-1);
        h.Op(0xe08948,3);h.Compare(0x406c38);h.Memory(CountOffset,0);h.Store32(CountOffset,1);h.Fs();h.Compare(0x13579bdf);
        h.Immediate(3,0x76543210); // Real sigreturn restores the parent's original RBX.
        Call(h,handler,186);h.Compare(1);
        Call(h,handler,14,2,0,Address(HandlerMaskOut),8);h.Compare(0);h.Memory(HandlerMaskOut,0x200,true);
        Call(h,handler,131,0,Address(HandlerStackOut));h.Compare(0);h.Memory(HandlerStackOut+8,1);
        Call(h,handler,1,1,Address(HandlerWitnessOffset),15);h.Compare(15);h.Op(0xc3,1);
        if(h.Position>RestorerOffset)throw new ToolFailure("m16_020_epoll_handler_overlap");
        r.Immediate(0,15);r.Finish();r.Op(0x0b0f,2);
        Writer failure=new(elf,FailureOffset);failure.Call(231,99,0,0);failure.Op(0x0b0f,2);

        List<WaitInvocation> steps=[];
        void Add(IEnumerable<Invocation> list,bool isChild)=>steps.AddRange(list.Select(i=>new WaitInvocation(i.Number,i.ReturnRip,isChild,i.Handler)));
        Add(parent.Take(blocked+1),false);Add(child,true);Add(handler,false);steps.Add(new(15,r.Rips[0],false,true));
        Add(parent.Skip(blocked+1),false);
        return(elf,steps.Select(s=>new Invocation(s.Number,s.ReturnRip,s.Handler)).ToArray(),steps.ToArray());
    }
}
