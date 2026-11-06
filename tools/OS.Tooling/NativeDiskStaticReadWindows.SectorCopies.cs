namespace OS.Tooling;

public static partial class NativeDiskStaticReadWindows
{
    /// <summary>Copy bounded transcript indices and index each committed sector
    /// under its original submit and validated-completion positions. Inputs stay
    /// borrowed for this synchronous call; returned collections are local to the
    /// enclosing inspection. Reject malformed indices or stage joins with the
    /// existing reasons. This does not authenticate windows, image or ownership.</summary>
    private static List<SectorCopy> IndexSectorCopies(NativeDiskReadObservations.Inspection device, IReadOnlyList<int> ioRecordIndices, out Dictionary<ulong, List<int>> bySector)
    {
        int rows = device!.Rows.Count;
        Need(rows is > 0 and <= NativeDiskReadObservations.MaximumRows && ioRecordIndices is not null && ioRecordIndices.Count == rows, "io_indices");
        int[] indices = new int[rows];
        for (int i = 0; i < rows; i++)
        {
            indices[i] = ioRecordIndices![i];
            Need(indices[i] >= 0 && (i == 0 || indices[i] > indices[i - 1]), "io_indices");
        }

        List<SectorCopy> copies = [];
        bySector = [];
        int submitted = -1, validated = -1;
        for (int i = 0; i < rows; i++)
        {
            var observation = device.Rows[i];
            if (observation.Kind == NativeDiskReadObservations.Kind.Submitted)
            {
                submitted = i;
                validated = -1;
                continue;
            }

            if (observation.Kind == NativeDiskReadObservations.Kind.CompletionValidated)
            {
                validated = i;
                continue;
            }

            Need(submitted >= 0 && validated == submitted + 1 && validated < i, "copy_stages");
            var submit = device.Rows[submitted];
            var validation = device.Rows[validated];
            Need(submit.Kind == NativeDiskReadObservations.Kind.Submitted && validation.Kind == NativeDiskReadObservations.Kind.CompletionValidated && validation.Bytes == observation.Bytes && submit.Generation == observation.Generation && validation.Generation == observation.Generation && submit.Sector == observation.Sector && validation.Sector == observation.Sector, "copy_stages");
            int copy = copies.Count;
            copies.Add(new(i, indices[submitted], indices[validated], indices[i], observation));
            if (!bySector.TryGetValue(observation.DataSector, out var list))
                bySector.Add(observation.DataSector, list = []);
            list.Add(copy);
        }

        return copies;
    }
}
