#if M16_008_PROBE
using OS.Boot;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Kernel.Threading;
using OS.Pal;
using NativeProgram = OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram;
using NativeContext = OS.Kernel.Threading.X64ThreadContext;
using NativeSwitch = OS.Kernel.Threading.ThreadContextSwitch;

namespace OS.Boot;

#pragma warning disable 649 // Caller-owned buffers are initialized through admitted native pointers.


public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
#if M16_014_PROBE
        // One serialized, supervisor-only negative lifetime. Each stage requires
        // its exact predecessor and independently checked owned restoration;
        // neither a log string nor an arbitrary false return can advance it.
        private enum LinuxQuotaDisposition : uint { None, Pending, Terminating, NativeCleaned, CpuRestored, NetworkCleaned }



#endif









#if M10_017_PROBE || M10_018_PROBE



#if M10_018_PROBE

#endif
#endif
#if M16_014_PROBE



#endif
#if M10_004_PROBE





#endif
#if M16_009_PROBE



#endif
























#if M16_014_PROBE


#endif









#if M16_027_PROBE


#else

#endif




#if M16_027_PROBE


#endif
























#if M16_027_PROBE

#else

#endif






#if M16_014_PROBE

#endif




























#if M10_018_PROBE


#endif

#if M16_027_PROBE

#else

#endif

#if M16_009_PROBE

#endif























#if M16_009_PROBE

#endif






























#if M16_009_PROBE



#endif





#if M16_027_PROBE














#endif


































#if M10_018_TIMING_PROBE
























#else

#endif

#if M16_009_PROBE



#endif




































#if M10_018_PROBE




#endif








#if M16_027_PROBE







#endif
#if M16_025_PROBE



#if M10_018_PROBE






#endif
#endif
#if CLROS_LINUX_SOCKETS



#endif

#if M16_027_PROBE

#endif
#if M16_025_PROBE

#endif

#if M16_027_PROBE
































#endif

#if M10_018_PROBE


#endif
#if M16_025_PROBE




#if !M16_027_PROBE && !M10_018_PROBE

#endif








#endif
#if CLROS_LINUX_SOCKETS




















#if M10_018_PROBE














#endif

#endif





















#if M16_014_PROBE







#endif










#if M16_009_PROBE

#endif






#if M16_027_PROBE


#else

#endif



























#if M16_009_PROBE





#endif






#if M16_025_PROBE && !M16_027_PROBE && !M10_018_PROBE


#endif
#if CLROS_LINUX_SOCKETS



#if M10_018_TIMING_PROBE


#else

#if M16_025_PROBE && !M16_027_PROBE && !M10_018_PROBE

#endif

#endif

#endif
#if M10_017_PROBE || M10_018_PROBE





#endif
#if M16_009_PROBE

#endif
#if M16_025_PROBE && !M16_027_PROBE && !M10_018_PROBE





#endif
#if M16_025_PROBE && !M16_027_PROBE

#endif

#if M16_027_PROBE

#endif
#if M10_017_PROBE || M10_018_PROBE


#elif M10_004_PROBE

#endif
#if M16_014_PROBE

#endif
#if M16_009_PROBE

#endif














#if M16_014_PROBE


#else

#endif















#if M16_014_PROBE

#else

#endif






#if M16_009_PROBE

#endif
























#if M16_027_PROBE

#endif
#if M16_014_PROBE && CLROS_LINUX_SOCKETS

#endif
#if M16_014_PROBE


#endif
#if CLROS_LINUX_SOCKETS







#endif
#if M16_009_PROBE


#endif


#if CLROS_LINUX_SOCKETS

#endif
#if M16_014_PROBE














#endif









#if M16_014_PROBE

#else

#endif


#if M16_009_PROBE












#endif






#if M16_009_PROBE

#endif
#if M16_027_PROBE



#else

#endif
















































#if M16_009_PROBE




#endif








#if M16_009_PROBE

#endif

#if M16_009_PROBE

#endif


#if M16_009_PROBE






#endif




#if M16_027_PROBE

#endif
#if M16_025_PROBE

#endif
#if CLROS_LINUX_SOCKETS

#endif


#if M16_027_PROBE

#endif




#if M16_025_PROBE && !M16_027_PROBE

#endif
#if M16_027_PROBE



#else

#endif
#if M16_025_PROBE && !M16_027_PROBE

#endif























#if M16_009_PROBE

#else

#endif






















#if M16_027_PROBE

#endif




















#if M16_014_PROBE

















































#endif

#if M16_014_PROBE
































































































































#endif








#if M16_014_PROBE

#endif







#if M16_027_PROBE



#else

#endif







    }
}
#endif
