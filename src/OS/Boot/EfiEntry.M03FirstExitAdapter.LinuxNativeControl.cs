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







#endif


        // Metadata lives only in the program's retained RW/NX arena. No user leaf
        // exposes this storage. One IRQ-off BSP owns the complete lifetime.
        private struct LinuxNativeControl
        {
            internal Storage* State;
            internal ulong Key, AliasSerial;
            internal uint Phase, Published, Capacity, Selected, Failed, Stopping, Reaped, Transfers, UserFaults;
#if M10_017_PROBE || M10_018_PROBE
            internal LinuxStaticFileObservation StaticApp;
            internal OSHostHandle StaticMain;
            internal LinuxDescriptorServices.Token StaticListener;
#if M10_018_PROBE
            internal LinuxStaticPending* StaticPending;
#endif
#endif
#if M16_014_PROBE
            internal ulong QuotaStackAcquired, QuotaStackLastReturned, QuotaStackPeakBytes, QuotaContextPeakBytes;
            internal uint QuotaStackPeakLive, QuotaStackStarted, QuotaStackEnded;
            internal fixed ulong QuotaStackGenerations[(int)LinuxNativeWorkers];
#endif
#if M10_004_PROBE
            internal uint WebFlags, WebBoundFd, WebStartMatch;
            internal ulong WebConfigBytes, WebStdoutBytes, WebStderrBytes;
            internal LinuxAppStartupObservation WebStartup;
            internal LinuxAppLogObservation WebLogs;
            internal LinuxAppLogQuotaObservation WebQuota;
#endif
#if M16_009_PROBE
            internal uint UserTimer, UserAcknowledged, UserDrain, UserPreempted;
            internal ulong TimingStart, TimingArm, TimingCapture, TimingReady;
            internal uint TimingValid;
#endif
            internal NativeProgram.Plan Plan;
            internal KernelThreadTable Threads;
            internal LinuxThreadServices Process;
            internal LinuxSignalServices.State* Signals;
            internal LinuxThreadServices.FutexCell* Cells;
            internal LinuxThreadServices.Slot* Slots;
            internal LinuxThreadServices.Worker* Workers;
            internal LinuxSyscallGateState* Gates;
            internal uint* Actions;
            internal uint* Entered;
            internal LinuxSyscallAccess User;
            internal KernelThreadSlot* KernelSlots;
            internal OSHostHandle* Ready;
            internal DeadlineQueue.Entry* Deadlines;
            internal WaitAddress.Slot* Addresses;
            internal WaitAddress.Registration* Registrations;
            internal X64LinuxUserTransition.UserImage* Initial;
            internal byte* CodeCopy;
            internal X64TimerInterrupt.Plan Handler;
            internal SchedulerTimer Timer;
            internal DeadlineQueue TimerDeadlines;
            internal DeadlineQueue.Entry TimerDeadline;
            internal ulong Epoch, OriginalIst;
            internal LinuxThreadExecutionBudget Budget;
#if M16_014_PROBE
            internal ulong ServiceProgress, ServicePeakCalls, ServicePeakIdentity, ServiceFinished;
            internal uint ServiceReady, ServiceShutdown;
#endif
            internal uint Installed, GuardAttached, ProcessAttached, TableAttached, SignalsInitialized;
            internal fixed ulong Cpu[(int)NativeSwitch.ObservationWords];
            internal fixed ulong GatewayCpu[(int)X64LinuxUserTransition.ObservationWords];
            internal fixed ulong OriginalIdtr[2], NewIdtr[2], ObservedIdtr[2];
            internal fixed byte OriginalTss[104];
        }



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
