#if CLROS_LINUX_SOCKETS
using System;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Platform;
using OS.Net;
using NetDevice = OS.Drivers.ContinuingVirtioNetDevice;
using SocketNetwork = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using ThreadTable = OS.Kernel.Threading.KernelThreadTable;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {











































#if M16_014_PROBE

































































































#endif

#if M10_004_PROBE && !M16_014_PROBE



#else

#endif

        // Borrowed observations only. A release is reported after the original
        // handle becomes stale and its network slot disappears or is reused.
        private struct LinuxSocketLifetime
        {
            internal TcpConnectionTable.Handle Handle;
            internal TcpConnectionKey Key;
            internal ulong MaximumDeadline, Duration;
            internal uint Active, WaitObserved, DrainObserved, TimeoutObserved;
#if M16_014_PROBE
            internal uint LastCloseObserved, QuotaLeaseObserved;
            internal ulong AppSent, AppReceived;
#endif
        }
















#if M16_014_PROBE






#endif









#if M16_027_PROBE










































































#endif




#if M16_027_PROBE








#else


#endif










#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE






#else

#if M16_012_PROBE

#endif
#if M16_013_PROBE

#endif
#if M07_027_PROBE && M07_028_PROBE

#endif

#if M10_018_PROBE

#elif M10_017_PROBE

#else

#endif



#if M10_018_PROBE

#endif


#if M16_014_PROBE



#endif

















#endif




















#if !M16_027_PROBE

#endif


#if M16_014_PROBE


#else


#endif

#if M16_027_PROBE

#else

#endif





#if M16_014_PROBE

#endif
#if M16_027_PROBE

#endif

#if M16_014_PROBE



#endif

#if M10_004_PROBE && !M16_014_PROBE

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






#if M16_014_PROBE

#endif




#if M16_014_PROBE

#endif






#if M16_014_PROBE



















#endif





















#if M16_014_PROBE










#else

#endif


#if M16_014_PROBE






































































































#endif
















#if M16_025_PROBE && !M16_027_PROBE && !M10_018_PROBE



















































































































#endif


#if M10_018_TIMING_PROBE

#endif

#if M16_025_PROBE && !M16_027_PROBE && !M10_018_PROBE

#endif


#if M16_025_PROBE && !M16_027_PROBE

#endif

#if M10_018_TIMING_PROBE



























































#elif M16_027_PROBE
#if M16_027_TIMING_PROBE











#else














#endif
#else



#if M16_025_PROBE

#if !M10_018_PROBE

#endif


#if !M10_018_PROBE

#endif

#endif

#endif


#if M16_027_TIMING_PROBE

















#endif















#if M16_027_PROBE



#elif M10_018_PROBE



#else

#endif







#if M16_027_PROBE

#elif M10_018_PROBE

#endif


#if M16_027_PROBE





#elif M10_018_PROBE

#endif















#if M10_018_TIMING_PROBE

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





#if M16_027_PROBE


#else

#endif





    }
}
#endif
