using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Hash;
using OS.Std.Time;
using OS.Storage;

namespace OS.Tooling;
public static partial class WritableDataImageFixture
{
    /// <summary>Copied, immutable final-image comparison for a small mechanical
    /// mutation. Both extractions remain available, including logical file bytes.
    /// This does not validate intermediate images, an operation's permitted
    /// targets, flush/sync, recovery, persistence, process or guest execution.</summary>

    public sealed class TransitionReceipt
    {
        private readonly byte[] json;
        internal TransitionReceipt(Inspection before, Inspection after, SectorChange[] changes)
        {
            Before = before;
            After = after;
            Changes = Array.AsReadOnly(changes);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("schema", "CLROS_WRITABLE_DATA_TRANSITION_V1");
                writer.WritePropertyName("before");
                writer.WriteRawValue(Serialize(before));
                writer.WritePropertyName("after");
                writer.WriteRawValue(Serialize(after));
                writer.WriteStartArray("changes");
                foreach (var change in changes)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("lba", change.Lba);
                    writer.WriteString("kind", change.Kind.ToString());
                    writer.WriteString("oldSha256", change.OldSha256);
                    writer.WriteString("newSha256", change.NewSha256);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            json = stream.ToArray();
        }

        public Inspection Before { get; }
        public Inspection After { get; }
        public ReadOnlyCollection<SectorChange> Changes { get; }

        public byte[] CopyJson() => (byte[])json.Clone();
    }
}
