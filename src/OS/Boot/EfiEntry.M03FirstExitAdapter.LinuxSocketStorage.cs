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









#if M16_014_PROBE


#endif

        // Image-resident ownership outlives every borrowed callback, including a
        // rejected native transfer. Never reuse or free this storage after fault.
        private struct LinuxSocketStorage
        {
            internal Storage* Boot;
            internal NetDevice.State* Device;
            internal LinuxSocketServices.State Sockets;
            internal LinuxEpollServices.State Epoll;
            internal SocketNetwork.State Network;
            internal TcpConnectionTable.Owner Table;
            internal MacAddress Peer;
            internal uint Phase, Failed, Draining;
            internal ulong Polls, Received, Copied, Started;
            internal ulong ResolveSent, ResolveReceived, ResolveStarted, ResolveEnded;
            internal ulong ArpReceived, Accepted, Ignored, Capacity, ArpCopied;
            internal ulong Fresh, Retired, Ended, MaximumDeadline;
#if M16_014_PROBE
            internal ulong CompactTransfers, CompactFresh, CompactRetired;
            internal uint BulkHighWater, CompactHighWater;
            internal uint QuotaLeasePublished;
            internal ulong QuotaLeaseAcquired, QuotaLeaseReturned;
            internal ulong QuotaBindingAcquired, QuotaBindingReturned;
            internal fixed ulong CompactGenerations[64], CompactDeadlines[64];
#endif
            internal fixed byte Endpoints[2048], Ports[32], Registrations[2560];
            internal fixed byte Slots[1024 * (int)LinuxSocketConnections], Entries[128 * (int)LinuxSocketConnections];
            internal fixed ulong Generations[(int)LinuxSocketConnections];
            internal fixed ulong Lifetimes[16 * (int)LinuxSocketConnections];
            internal fixed ulong WakeIdentities[(int)LinuxNativeWorkers], WakeSequences[(int)LinuxNativeWorkers];
            internal fixed byte Buffers[((int)LinuxSocketReceive + (int)LinuxSocketSend) * (int)LinuxSocketConnections];
            internal fixed byte Input[1536], Output[1536];
        }

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
