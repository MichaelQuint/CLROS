namespace OS.Kernel.Runtime;
public enum NativeAotClassConstructorState
{
    Uninitialized = 0,
    Initializing = 1,
    Initialized = 2,
    Failed = 3
}
