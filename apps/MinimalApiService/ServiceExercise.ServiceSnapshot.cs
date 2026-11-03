using System.Diagnostics;
using System.Runtime.InteropServices;

// A detached numeric observation, with no output bytes, keys, pointers or
// authority to acknowledge kernel exit/reap/resource cleanup.
// Phase:0 idle,1 running,2 stopping,3 failed,4 stopped.
internal sealed record ServiceSnapshot(string Contract, int Phase, int Failure, int ManagedWorkerId, long Calls, long RandomRequests, long RandomBytes, long Iterations, long ElapsedMilliseconds);
