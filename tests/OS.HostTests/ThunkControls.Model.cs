#if CHECKED_REFERENCE_THUNK_CHILD
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.X64TextSerializerGenerator;

internal static partial class ThunkControls
{



















































































































    // Finite interpreter for exactly the emitted instruction forms. It models bytes and a private
    // patterned stack only. The opaque callback is a test transition, never a callable export.
    sealed class Model
    {
        readonly byte[] code; readonly int op,residue; readonly uint status; readonly bool entryDf,callbackDf;
        readonly ulong[] g=new ulong[16], original=new ulong[16]; readonly byte[][] xmm=new byte[16][];
        readonly byte[][] originalXmm=new byte[16][]; readonly byte[] stack=new byte[1024], initial=new byte[1024];
        readonly List<int> stores=[], restores=[]; ulong flags; uint mxcsr=0x1F80; const int StackBase=0x100000;
        int ip,callbacks,minimum,home=-1; bool zero,returned,rejected; readonly int start,terminal,s; readonly ulong originalFlags;
        const ulong ReturnAddress=0x123456789ABCDEF0;
        internal Model(byte[] bytes,int operation,int stackResidue,uint result,bool df,bool postDf)
        {
            code=bytes;op=operation;residue=stackResidue;status=result;entryDf=df;callbackDf=postDf;
            start=operation==1?0:operation==2?464:928; terminal=start+(operation==3?460:450);ip=start;
            s=StackBase+768+residue; minimum=s;
            for(int i=0;i<16;i++) { g[i]=0xA110000000000000UL+(ulong)(i*0x101+0x31);xmm[i]=Enumerable.Range(0,16).Select(j=>(byte)(i*17+j*3+5)).ToArray();originalXmm[i]=(byte[])xmm[i].Clone(); }
            // Distinct opaque addresses, intentionally never dereferenced.
            g[6]=0x12345000; g[7]=0x23456000; g[4]=(ulong)s;
            Array.Copy(g,original,16); flags=0x246UL|(df?0x400UL:0); originalFlags=flags;
            for(int i=0;i<stack.Length;i++) stack[i]=(byte)(i*31+7);
            Put64(s,ReturnAddress);Array.Copy(stack,initial,stack.Length);
        }
        Span<byte> Mem(int address,int length) { Need(address>=StackBase && address+length<=StackBase+stack.Length,"stack-bounds");return stack.AsSpan(address-StackBase,length); }
        void Put64(int address,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(Mem(address,8),value);
        ulong Get64(int address)=>BinaryPrimitives.ReadUInt64LittleEndian(Mem(address,8));
        void Push(ulong value) {g[4]-=8;minimum=Math.Min(minimum,(int)g[4]);Put64((int)g[4],value);}
        ulong Pop() {ulong value=Get64((int)g[4]);g[4]+=8;return value;}
        int I32(int offset)=>BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(offset));
        bool At(params byte[] bytes)=>code.AsSpan(ip,Math.Min(bytes.Length,code.Length-ip)).SequenceEqual(bytes);
        void Callback()
        {
            callbacks++;Need(callbacks==1,"single-callback");home=(int)g[4];
            Need((home&15)==0 && s-home==(residue==0?432:440),"callback-alignment-depth");
            Need(g[1]==(ulong)op && g[2]==original[op==3?7:1] && g[8]==original[op==3?6:2] && g[9]==8,"callback-args");
            Need((flags&0x400)==0,"callback-entry-DF");
            Need(stores.SequenceEqual(Enumerable.Range(0,16)),"all-XMM-saved-before-call");
            int r=s-16; int[] saved=[0,1,2,3,6,7,8,9,10,11,12,13,14,15];
            for(int i=0;i<saved.Length;i++) Need(Get64(r-8*(i+1))==original[saved[i]],"GPR-save-slot");
            Need(Get64(r)==original[5] && Get64(r+8)==originalFlags && Get64(s)==ReturnAddress,"anchor-flags-return");
            for(int i=0;i<16;i++) Need(Mem(home+32+i*16,16).SequenceEqual(originalXmm[i]),"XMM-save-slot");
            Need(BinaryPrimitives.ReadUInt32LittleEndian(Mem(home+288,4))==0x1F80,"MXCSR-save-slot");
            Need(home+292<=r-112 && home+32>home,"home-save-separation");
            Push((ulong)(ip+5));Need((g[4]&15)==8,"ordinary-callback-entry-residue");
            Mem(home,32).Fill(0xC9); // callback owns exactly home0..31, never touches caller data.
            // Adversarial transition clobbers even more GPR/SIMD state than the normal ABI permits;
            // anchored RBP/RSP remain valid. This is model coverage, not proof of a real callback.
            for(int i=0;i<16;i++) if(i!=4 && i!=5) g[i]=0xD220000000000000UL+(ulong)i;
            foreach(byte[] row in xmm) Array.Fill(row,(byte)0xDD);
            mxcsr=0x3F80; flags=0x202UL|(callbackDf?0x400UL:0);g[0]=status;
            Need(Pop()==(ulong)(ip+5),"call-return-slot");
        }
        void Step()
        {
            if(ip==terminal){Need(At(0xEB,0xFE),"reject-loop");rejected=true;return;}
            if(At(0x9C)){Push(flags);ip++;return;}
            if(At(0x9D)){flags=Pop();ip++;return;}
            if(At(0x48,0x89,0xE5)){g[5]=g[4];ip+=3;return;}
            if(At(0x48,0x81,0xEC)){g[4]-=(uint)I32(ip+3);minimum=Math.Min(minimum,(int)g[4]);ip+=7;return;}
            if(At(0x48,0x83,0xE4,0xF0)){g[4]&=~15UL;minimum=Math.Min(minimum,(int)g[4]);ip+=4;return;}
            if(At(0x48,0xF7,0x45,0x08)){zero=(Get64((int)g[5]+8)&(uint)I32(ip+4))==0;ip+=8;return;}
            if(At(0x0F,0x85)){ip=zero?ip+6:ip+6+I32(ip+2);return;}
            if(code[ip]==0xF3)
            {
                int p=ip+1;bool high=code[p]==0x44;if(high)p++;
                Need(code[p]==0x0F && (code[p+1]==0x7F || code[p+1]==0x6F) && (code[p+2]&0xC7)==0x84 && code[p+3]==0x24,"finite-MOVDQU-form");
                int register=((code[p+2]>>3)&7)+(high?8:0), offset=I32(p+4);
                Need(offset==32+16*register,"XMM-offset");
                if(code[p+1]==0x7F){xmm[register].CopyTo(Mem((int)g[4]+offset,16));stores.Add(register);}
                else {Mem((int)g[4]+offset,16).CopyTo(xmm[register]);restores.Add(register);}
                ip=p+8;return;
            }
            if(At(0x0F,0xAE,0x9C,0x24)){BinaryPrimitives.WriteUInt32LittleEndian(Mem((int)g[4]+I32(ip+4),4),mxcsr);ip+=8;return;}
            if(At(0x0F,0xAE,0x94,0x24)){mxcsr=BinaryPrimitives.ReadUInt32LittleEndian(Mem((int)g[4]+I32(ip+4),4));ip+=8;return;}
            if(code[ip]==0xB9){g[1]=(uint)I32(ip+1);ip+=5;return;}
            if(At(0x48,0x8B,0x55)){g[2]=Get64((int)g[5]+(sbyte)code[ip+3]);ip+=4;return;}
            if(At(0x4C,0x8B,0x45)){g[8]=Get64((int)g[5]+(sbyte)code[ip+3]);ip+=4;return;}
            if(At(0x41,0xB9)){g[9]=(uint)I32(ip+2);ip+=6;return;}
            if(code[ip]==0xE8){Need(I32(ip+1)==0,"unlinked-call-addend");Callback();ip+=5;return;}
            if(At(0x83,0xF8,0x01)){zero=(uint)g[0]==1;ip+=3;return;}
            if(At(0x49,0xF7,0xC3)){zero=(g[11]&(uint)I32(ip+3))==0;ip+=7;return;}
            if(At(0x48,0x83,0x45)){int a=(int)g[5]+(sbyte)code[ip+3];Put64(a,Get64(a)+(ulong)(sbyte)code[ip+4]);ip+=5;return;}
            if(At(0x48,0x8D,0x65,0x90)){g[4]=g[5]-112;ip+=4;return;}
            bool extended=code[ip]==0x41;int opcode=code[ip+(extended?1:0)];int registerIndex=(opcode&7)+(extended?8:0);
            if(opcode>=0x50 && opcode<=0x57){Push(g[registerIndex]);ip+=extended?2:1;return;}
            if(opcode>=0x58 && opcode<=0x5F){g[registerIndex]=Pop();ip+=extended?2:1;return;}
            if(code[ip]==0xC3){Need(Pop()==ReturnAddress,"return-address");returned=true;ip++;return;}
            throw new InvalidOperationException("unsupported-instruction-at-"+ip);
        }
        internal object Verify()
        {
            for(int step=0;step<250 && !returned && !rejected;step++) Step();
            bool success=status==1 && !entryDf && !callbackDf;
            Need(returned==success && rejected!=success,"return-rejection-outcome");
            Need(callbacks==(entryDf?0:1),"callback-count");
            Need(stack.AsSpan(768+residue).SequenceEqual(initial.AsSpan(768+residue)),"caller-return-home-canary");
            Need(stack.AsSpan(0,320+residue).SequenceEqual(initial.AsSpan(0,320+residue)),"lower-stack-canary");
            if(success)
            {
                for(int i=0;i<16;i++) Need(g[i]==(i==4?(ulong)s+8:original[i]+(op==3 && (i==6 || i==7)?8UL:0)),"GPR-return-"+i);
                for(int i=0;i<16;i++) Need(xmm[i].AsSpan().SequenceEqual(originalXmm[i]),"XMM-return-"+i);
                Need(mxcsr==0x1F80 && flags==originalFlags && restores.SequenceEqual(Enumerable.Range(0,16)),"MXCSR-flags-restore");
                Need(s-minimum==(residue==0?440:448),"maximum-local-call-depth");
            }
            else
            {
                Need(restores.Count==0,"rejection-before-restores");
                Need(Get64(s-56)==original[6] && Get64(s-64)==original[7],"no-saved-byref-advance-on-reject");
            }
            return new {Operation=op,EntryResidue=residue,Status=status,EntryDf=entryDf,CallbackDf=callbackDf,Callbacks=callbacks,Returned=returned,Rejected=rejected,MeasuredModelDepth=s-minimum,ActualNativeExecution=false};
        }
    }
}

















#else
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
#endif
