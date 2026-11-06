/// <summary>Fixed diagnostic response for the pinned vector qualification.
/// The response writer owns each requested block until its flush completes.
/// This endpoint stages three blocks below the adapter's65536-byte aggregate
/// limit. Actual transport segmentation, partial progress and wire contents
/// require independent syscall/peer evidence. Cancellation propagates normally.</summary>
internal static class KestrelVectorExercise
{
    internal static async Task WriteAsync(HttpContext context)
    {
        const int segmentBytes = 16384, segments = 3;
        context.Response.ContentType = "application/octet-stream";
        context.Response.ContentLength = segmentBytes * segments;
        for (int segment = 0; segment < segments; segment++)
        {
            Memory<byte> block = context.Response.BodyWriter.GetMemory(segmentBytes)[..segmentBytes];
            for (int index = 0; index < block.Length; index++)
                block.Span[index] = (byte)((segment * segmentBytes + index) % 251);
            context.Response.BodyWriter.Advance(segmentBytes);
        }
        var flushed = await context.Response.BodyWriter.FlushAsync(context.RequestAborted);
        if (flushed.IsCanceled)
            throw new OperationCanceledException(context.RequestAborted);
        Console.Error.WriteLine("CLROS_VECTOR_STAGED bytes=49152 blocks=3");
    }
}
