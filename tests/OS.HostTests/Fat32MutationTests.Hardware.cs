using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using M = OS.Storage.Fat32Mutation;
using Block = OS.Storage.WritableBlockSession;

internal static unsafe partial class Fat32MutationTests
{
    private struct Hardware
    {
        internal byte* Data, Volatile, Durable;
        internal ulong Descriptor, Available, Used, Chosen, Sector, Now;
        internal uint FeatureSelect, DriverSelect, Type, Produced, Notifies;
        internal ushort Selected, Size, Enabled, ConfigVector, QueueVector;
        internal byte Status;
        internal bool Pending;
    }
}
