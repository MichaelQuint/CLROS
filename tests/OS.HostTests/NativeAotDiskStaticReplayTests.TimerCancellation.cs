using System.Globalization;
using System.Text.RegularExpressions;

internal static partial class NativeAotDiskStaticReplayTests
{
    [HostTest("Full disk replay distinguishes quiet cancellation from acknowledged timer epochs", HostLane.Native)]
    private static void QuietTimerCancellation() => WithInventory((inventory, image) =>
    {
        var original = Vector(inventory, image, full: true);
        var before = Inspect(original, inventory, true);
        var cancelled = CancelFirstReturn(original);
        var after = Inspect(cancelled, inventory, true);
        HostCheck.True(after.FullProfile && !after.GrantsOriginalExecutionAuthority);
        HostCheck.Equal(before.Static.Protocol.Drains - 1, after.Static.Protocol.Drains);
        HostCheck.Equal(before.Static.Protocol.Irqs, after.Static.Protocol.Irqs);
        HostCheck.Equal(before.Static.Protocol.Transfers, after.Static.Protocol.Transfers);
        Reject(Change(cancelled, "M16_008_OWNER_END ", "irqs", ulong.MaxValue), inventory, "owner_accounting", true);
        Reject(Change(cancelled, "M16_009_CANCEL ", "identity", 0), inventory, "timer_cancel_binding", true);
        Reject(Change(cancelled, "M16_009_CANCEL ", "epoch", ulong.MaxValue), inventory, "timer_cancel_binding", true);
        int index = cancelled.FindIndex(row => row.StartsWith("M16_009_CANCEL ", StringComparison.Ordinal));
        var duplicate = cancelled.ToList();
        duplicate.Insert(index + 1, duplicate[index]);
        Reject(duplicate, inventory, null, true);
        Reject(cancelled.Where(row => !row.StartsWith("M16_009_CANCEL ", StringComparison.Ordinal)), inventory, null, true);
    });

    [HostTest("ordinary disk replay retains mandatory acknowledged return drains", HostLane.Native)]
    private static void QuietTimerCancellationScope() => WithInventory((inventory, image) =>
    {
        var original = Vector(inventory, image);
        _ = Inspect(original, inventory);
        Reject(CancelFirstReturn(original), inventory, "timer_cancel_profile");
    });

    // Independent scheduler vectors distinguish cancellation from an actual
    // acknowledged interrupt. Device epochs and every wall clock stay exact.
    private static List<string> CancelFirstReturn(List<string> source)
    {
        var rows = source.ToList();
        int first = rows.FindIndex(row => row.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal));
        HostCheck.True(first >= 0);
        rows[first] = rows[first].Replace("M16_009_DRAIN ", "M16_009_CANCEL ", StringComparison.Ordinal);
        for (int i = first; i < rows.Count; i++)
        {
            string row = rows[i];
            if (row.StartsWith("M16_009_CANCEL ", StringComparison.Ordinal) ||
                row.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal) ||
                row.StartsWith("M16_009_IRQ ", StringComparison.Ordinal) ||
                row.StartsWith("M16_008_TICK ", StringComparison.Ordinal) ||
                row.StartsWith("M16_008_TIMING ", StringComparison.Ordinal))
                rows[i] = Regex.Replace(row, @"\bepoch=([0-9]+)", match =>
                    "epoch=" + checked(ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) - 1).ToString(CultureInfo.InvariantCulture));
            else if (row.StartsWith("M16_008_OWNER_END ", StringComparison.Ordinal))
                rows[i] = Regex.Replace(row, @"\birqs=([0-9]+)", match =>
                    "irqs=" + checked(ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) - 1).ToString(CultureInfo.InvariantCulture));
        }
        return rows;
    }
}
