using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadOnlyReadSessionResult
{
    private VirtioBlockReadOnlyReadSessionResult(
        VirtioBlockReadOnlyReadSessionStatus status,
        VirtioBlockReadQueuePipelineAdapterResult adapter,
        bool isInitialized,
        VirtioBlockQueueCursorState cursorBefore,
        VirtioBlockQueueCursorState cursorAfter)
    {
        Status = status;
        Adapter = adapter;
        IsInitialized = isInitialized;
        CursorBefore = cursorBefore;
        CursorAfter = cursorAfter;
    }

    public VirtioBlockReadOnlyReadSessionStatus Status
    {
        get;
    }

    public VirtioBlockReadQueuePipelineAdapterResult Adapter
    {
        get;
    }

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockQueueCursorState CursorBefore
    {
        get;
    }

    public VirtioBlockQueueCursorState CursorAfter
    {
        get;
    }

    public BlockReadResult BlockRead => Adapter.BlockRead;

    public bool Succeeded =>
        IsInitialized && Status == VirtioBlockReadOnlyReadSessionStatus.Success;

    internal static VirtioBlockReadOnlyReadSessionResult FromAdapter(
        VirtioBlockReadQueuePipelineAdapterResult adapter,
        VirtioBlockQueueCursorState cursorBefore,
        VirtioBlockQueueCursorState cursorAfter)
    {
        VirtioBlockReadOnlyReadSessionStatus status = adapter.Succeeded ?
            VirtioBlockReadOnlyReadSessionStatus.Success :
            VirtioBlockReadOnlyReadSessionStatus.AdapterFailed;
        return new VirtioBlockReadOnlyReadSessionResult(
            status,
            adapter,
            isInitialized: true,
            cursorBefore,
            cursorAfter);
    }
}
