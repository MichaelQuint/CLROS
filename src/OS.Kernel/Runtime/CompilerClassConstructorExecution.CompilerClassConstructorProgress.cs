using OS.Boot;

namespace OS.Kernel.Runtime;
public struct CompilerClassConstructorProgress
{
    public uint EagerCount, EagerCompleted, LazyCount, LazyCompleted, ActiveLazyCount;
    public bool EagerFinished;
}
