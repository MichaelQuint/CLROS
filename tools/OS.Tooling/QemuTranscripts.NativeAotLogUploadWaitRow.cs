using System.Collections.ObjectModel;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Immutable numeric observation at its original input position.
    /// Text and every actual field are copied values, not tokens or pointers
    /// that permit access to the observed process, fd, output or request.</summary>

    public sealed class NativeAotLogUploadWaitRow
    {
        internal NativeAotLogUploadWaitRow(int index, string text, Dictionary<string, ulong> values)
        {
            RecordIndex = index;
            Text = text;
            Values = new ReadOnlyDictionary<string, ulong>(values);
        }

        public int RecordIndex { get; }
        public string Text { get; }
        public IReadOnlyDictionary<string, ulong> Values { get; }

        public ulong this[string field] => Values[field];
    }
}
