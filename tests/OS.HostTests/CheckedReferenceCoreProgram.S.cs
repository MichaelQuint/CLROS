using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Reflection.Metadata;
using System.Text;
using System.Xml.Linq;
#if CHECKED_REFERENCE_CORE_TEST
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

internal static unsafe partial class CheckedReferenceCoreProgram
{




























































































































    private sealed class S : IDisposable
    {
        private const int Bytes=147456;
        private readonly F f;
        private readonly byte* storage=(byte*)NativeMemory.AlignedAlloc(Bytes,4096);
        internal CheckedReferenceStackState* State=>(CheckedReferenceStackState*)(f.Storage+0x7000);
        internal ulong Authority=>(ulong)f.Storage+0x6800;
        internal ulong Start=>(ulong)storage+4096;
        internal ulong End=>Start+135168;
        internal ulong Rsp=>End-40;
        internal ulong Witness;
        internal S(F fixture)
        {f=fixture;Need(storage!=null,"mapped fake stack");new Span<byte>(storage,Bytes).Fill(0xD7);new Span<byte>(State,96).Clear();Witness=End-4096;}
        internal bool Arm()=>RhpNewFastAllocationCore.ArmReferenceStack(f.Access,f.Bump,f.Control,f.Records,f.State,State,Authority,Start,End,Rsp,Witness);
        internal bool Check()=>RhpNewFastAllocationCore.CheckReferenceStack(f.Access,f.Bump,f.Control,f.Records,f.State,State,Authority,Start,End,Rsp,Witness);
        internal byte[] Snapshot()=>new ReadOnlySpan<byte>(storage,Bytes).ToArray();
        internal void Unchanged(byte[] bytes)=>Need(new ReadOnlySpan<byte>(storage,Bytes).SequenceEqual(bytes),"mapped stack unchanged");
        internal void OnlyWords(byte[] bytes)
        {for(int i=0;i<Bytes;i++)if(i<4096||i>=4112)Need(storage[i]==bytes[i],"only admitted16 bytes written");}
        public void Dispose()=>NativeMemory.AlignedFree(storage);
    }




































































































































































































}
#else
#endif
