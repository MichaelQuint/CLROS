using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using Transition=OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;

internal static unsafe class LinuxExitTailTests
{
    // Exact linked musl bytes at 4648B7..4648C7 in the retained suspension ELF.
    // This host model walks the bytes; it does not execute CPL3 or native IRQs.
    private static ReadOnlySpan<byte> Code=>[0xb8,11,0,0,0,0x0f,5,0x48,0x31,0xff,0xb8,60,0,0,0,0x0f,5];
    private const ulong Rip=0x4648be,Stack=0x100000000,Bytes=0x183000;
    private struct Model
    {
        internal LinuxSyscallGateState* Gate;
        internal byte* Code;
        internal bool Removed,Remapped,BadCode,WritableCode,WrongGeneration,Unmasked,Handler,CopyFails,Partial;
        internal uint Mutations,ThreadCalls;
        internal long Result;
    }
    [UnmanagedCallersOnly] private static byte Pages(void* context,ulong page)
    {
        Model* m=(Model*)context;
        if(page==0x464000)return m->WritableCode?(byte)7:(byte)5;
        if(page>=Stack && page<Stack+Bytes)return !m->Removed || m->Remapped || m->Partial && page==Stack?(byte)3:(byte)0;
        return 0;
    }
    [UnmanagedCallersOnly] private static byte Copy(void* context,ulong address,byte* output,uint bytes)
    {
        Model* m=(Model*)context;
        if(m->CopyFails || address!=Rip-7 || bytes!=17)return 0;
        for(uint i=0;i<bytes;i++)output[i]=m->Code[i];
        if(m->BadCode)output[11]=231;
        return 1;
    }
    [UnmanagedCallersOnly] private static byte Write(void* context,uint fd,byte* bytes,uint count)=>0;
    [UnmanagedCallersOnly] private static long Memory(void* context,LinuxSyscallFrame* frame)
    {
        Model* m=(Model*)context;
        if(frame->Number!=11)return -38;
        m->Mutations++;
        if(m->Result==0 && frame->Argument0==Stack && frame->Argument1==Bytes)m->Removed=true;
        return m->Result;
    }
    [UnmanagedCallersOnly] private static LinuxSyscallResult Thread(void* context,LinuxSyscallFrame* frame)
    {
        ((Model*)context)->ThreadCalls++;
        return frame->Number==60?new(LinuxSyscallAction.ThreadExited):new(LinuxSyscallAction.Return,-38);
    }
    [UnmanagedCallersOnly] private static byte Policy(void* context,LinuxSyscallFrame* frame)
    {
        Model* m=(Model*)context;
        if(!m->Gate->HasExitTail)return 0;
        if(m->WrongGeneration || m->Unmasked || m->Handler)return 2;
        return LinuxSyscallGateway.ExitTailPolicy(ref *m->Gate,*frame);
    }
    private static LinuxSyscallAccess Access(Model* model,bool policy=true)=>new()
    {Context=model,UserInterrupts=true,PageAccess=&Pages,CopyFromUser=&Copy,Write=&Write,Startup=&Memory,Descriptors=&Memory,Threads=&Thread,
        ReturnPolicy=policy?&Policy:null};
    private static LinuxSyscallFrame Frame()=>new()
    {Number=11,Argument0=Stack,Argument1=Bytes,Rip=Rip,Rsp=Stack+Bytes-1024,FsBase=Stack+Bytes-512,Rflags=0x202};
    private static void Begin(Model* model,LinuxSyscallGateState* gate,byte* scratch,bool policy=true)
    {
        *gate=default;model->Gate=gate;
        LinuxSyscallAdmission admission=new(){UserRoot=0x2000,MaximumCalls=8,MaximumWrite=64,Scratch=scratch,ScratchBytes=64,
            FirmwareExited=true,OwnedBoundaryValidated=true,EnableStartup=true,EnableDescriptors=true,EnableThreads=true};
        HostCheck.Equal(LinuxSyscallGateStatus.Active,LinuxSyscallGateway.Begin(ref *gate,admission,Access(model,policy)));
    }
    private static LinuxSyscallFrame Exit()=>new()
    {Number=60,Argument0=0,Argument1=Bytes,Rip=Rip+10,Rsp=Stack+Bytes-1024,FsBase=Stack+Bytes-512,Rflags=0x246};

    [HostTest("linux.exit-tail real self-unmap requires exact linked code owner policy blocked signals and complete removal")]
    private static void Admission()
    {
        byte* code=stackalloc byte[17],scratch=stackalloc byte[64];Code.CopyTo(new Span<byte>(code,17));
        for(int mode=0;mode<9;mode++)
        {
            Model model=new(){Code=code,BadCode=mode==2,CopyFails=mode==3,WrongGeneration=mode==4,Unmasked=mode==5,Handler=mode==6,Partial=mode==7,Result=mode==8?-12:0};
            LinuxSyscallGateState gate=default;Begin(&model,&gate,scratch,mode!=1);
            var frame=Frame();HostCheck.True(LinuxSyscallGateway.ValidateReturn(frame,Access(&model,mode!=1)));
            var result=LinuxSyscallGateway.Dispatch(ref gate,ref frame);
            HostCheck.Equal(1U,model.Mutations);
            if(mode==0)
            {
                HostCheck.True(model.Removed && gate.HasExitTail && result.Action==LinuxSyscallAction.Return && result.Value==0);
                HostCheck.True(!LinuxSyscallGateway.ValidateReturn(frame,new(){Context=&model,UserInterrupts=true,PageAccess=&Pages}));
                var exit=Exit();result=LinuxSyscallGateway.Dispatch(ref gate,ref exit);
                HostCheck.True(result.Action==LinuxSyscallAction.ThreadExited && gate.Status==LinuxSyscallGateStatus.Exited && !gate.HasExitTail && model.ThreadCalls==1);
                HostCheck.True(!LinuxSyscallGateway.ValidateReturn(exit,Access(&model)),"exit revoked the exceptional continuation");
            }
            else if(mode==8)
                HostCheck.True(!model.Removed && !gate.HasExitTail && result.Action==LinuxSyscallAction.Return && result.Value==-12);
            else
                HostCheck.True(model.Removed && !gate.HasExitTail && result.Action==LinuxSyscallAction.Terminate && gate.Status==LinuxSyscallGateStatus.InvalidReturn);
        }
    }

    [HostTest("linux.exit-tail rejects forged intermediate instructions mapping revival changed owner and nonexit syscalls")]
    private static void Forgery()
    {
        byte* code=stackalloc byte[17],scratch=stackalloc byte[64];Code.CopyTo(new Span<byte>(code,17));
        for(int mode=0;mode<12;mode++)
        {
            Model model=new(){Code=code};LinuxSyscallGateState gate=default;Begin(&model,&gate,scratch);
            var unmap=Frame();HostCheck.Equal(LinuxSyscallAction.Return,LinuxSyscallGateway.Dispatch(ref gate,ref unmap).Action);
            var exit=Exit();
            switch(mode)
            {
                case 0:exit.Rip++;break;
                case 1:exit.Rsp--;break;
                case 2:exit.FsBase++;break;
                case 3:exit.GsBase=Stack;break;
                case 4:exit.Rflags=0x202;break;
                case 5:exit.Argument0=1;break;
                case 6:exit.Number=39;break;
                case 7:model.Remapped=true;break;
                case 8:model.BadCode=true;break;
                case 9:model.WrongGeneration=true;break;
                case 10:model.Unmasked=true;break;
                case 11:model.WritableCode=true;break;
            }
            HostCheck.Equal(LinuxSyscallAction.Terminate,LinuxSyscallGateway.Dispatch(ref gate,ref exit).Action);
            HostCheck.True(model.ThreadCalls==0 && !gate.HasExitTail && gate.Status==LinuxSyscallGateStatus.InvalidReturn);
        }
    }

    [HostTest("linux.exit-tail full user syscall capture restore and interposed IRQ images retain only exact instruction boundaries")]
    private static void Images()
    {
        byte* code=stackalloc byte[17],scratch=stackalloc byte[64];Code.CopyTo(new Span<byte>(code,17));
        byte* data=(byte*)NativeMemory.AlignedAlloc(Transition.DataBytes,4096);
        Transition.UserImage* image=(Transition.UserImage*)NativeMemory.AlignedAlloc(4096,4096);
        try
        {
            for(uint offset=0;offset<=8;offset=offset==0?3U:8U)
            {
                new Span<byte>(data,(int)Transition.DataBytes).Clear();*image=default;
                Model model=new(){Code=code};LinuxSyscallGateState gate=default;Begin(&model,&gate,scratch);var user=Access(&model);
                var frame=Frame();HostCheck.True(Transition.InitializeUserImage(image,Transition.UserImageBytes,frame,0xffff,user));
                for(int i=0;i<14;i++)image->Gpr[i]=(ulong)i+100;
                HostCheck.True(Transition.InstallUserImage(data,Transition.DataBytes,image,Transition.UserImageBytes,0xffff,user));
                *(ulong*)(data+Transition.PhaseOffset)=1;
                HostCheck.True(Transition.CaptureUserImage(data,Transition.DataBytes,image,Transition.UserImageBytes,0xffff,user));
                var result=LinuxSyscallGateway.Dispatch(ref gate,ref image->Frame);image->Value=result.Value;
                HostCheck.Equal(LinuxSyscallAction.Return,result.Action);
                HostCheck.True(Transition.RestoreActiveUserImage(data,Transition.DataBytes,image,Transition.UserImageBytes,0xffff,user));
                var irq=frame;irq.Number=0;irq.Rip+=offset;irq.Rflags=offset==0?0x202UL:0x246UL;
                if(offset!=0)irq.Argument0=0;
                *(LinuxSyscallFrame*)(data+Transition.FrameOffset)=irq;
                *(ulong*)(data+Transition.OutcomeOffset)=3;*(ulong*)(data+Transition.VectorOffset)=0xe1;
                *(ulong*)(data+Transition.ErrorOffset)=0;
                *(ushort*)(data+Transition.FaultSelectorsOffset)=Transition.UserCs;*(ushort*)(data+Transition.FaultSelectorsOffset+2)=Transition.UserSs;
                *(long*)(data+Transition.ValueOffset)=offset==8?60:0;
                if(offset!=0)*(ulong*)(data+0x178)=0;
                HostCheck.True(Transition.CaptureInterruptUserImage(data,Transition.DataBytes,image,Transition.UserImageBytes,0xffff,user));
                HostCheck.True(image->Frame.Rip==irq.Rip && image->Gpr[0]==100 && image->Value==(offset==8?60:0));
                *(ulong*)(data+Transition.PhaseOffset)=2;data[0x10]=0xa7;data[Transition.KernelFxOffset]=0x93;
                HostCheck.True(Transition.InstallUserImage(data,Transition.DataBytes,image,Transition.UserImageBytes,0xffff,user));
                HostCheck.True(data[0x10]==0xa7 && data[Transition.KernelFxOffset]==0x93,"supervisor restoration storage retained");
                var forged=image->Frame;image->Frame.Rip=Rip+4;
                HostCheck.True(!Transition.InstallUserImage(data,Transition.DataBytes,image,Transition.UserImageBytes,0xffff,user));image->Frame=forged;
                var exit=Exit();*(LinuxSyscallFrame*)(data+Transition.FrameOffset)=exit;*(ulong*)(data+Transition.PhaseOffset)=1;
                HostCheck.True(Transition.CaptureUserImage(data,Transition.DataBytes,image,Transition.UserImageBytes,0xffff,user));
                HostCheck.Equal(LinuxSyscallAction.ThreadExited,LinuxSyscallGateway.Dispatch(ref gate,ref image->Frame).Action);
                if(offset==8)break;
            }
        }
        finally{NativeMemory.AlignedFree(data);NativeMemory.AlignedFree(image);}
    }
}
