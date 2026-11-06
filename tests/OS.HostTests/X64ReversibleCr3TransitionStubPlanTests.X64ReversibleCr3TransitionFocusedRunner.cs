using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OS.Kernel.Memory;
#if M01_013_TRANSITION_FOCUSED
internal static class X64ReversibleCr3TransitionFocusedRunner
{
    public static int Main()
    {
        return X64ReversibleCr3TransitionStubPlanTests
            .RunM01013Transition() == 0
            ? 0
            : 1;
    }
}
#endif
