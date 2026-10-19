using System.Text.Json;

namespace OS.Tools.QemuRunner;

/// <summary>Strict immutable reader of untrusted application service telemetry.
/// Samples retain no input bytes. Their counters are independently observed,
/// not atomic snapshots, and confer no native, guest or cleanup authority.</summary>
public sealed class ServiceStatus
{
    public string Contract => "clros.service.v1";
    public int Phase { get; }
    public int Failure { get; }
    public int ManagedWorkerId { get; }
    public long Calls { get; }
    public long RandomRequests { get; }
    public long RandomBytes { get; }
    public long Iterations { get; }
    public long ElapsedMilliseconds { get; }

    private ServiceStatus(ReadOnlySpan<long> values)
    {
        Phase = (int)values[0]; Failure = (int)values[1]; ManagedWorkerId = (int)values[2];
        Calls = values[3]; RandomRequests = values[4]; RandomBytes = values[5];
        Iterations = values[6]; ElapsedMilliseconds = values[7];
    }

    /// <summary>Application-reported stopped witness only. This does not prove
    /// response delivery, native execution, worker reap or guest resource cleanup.</summary>
    public bool ApplicationStoppedWitness => Phase == 4 && Failure == 0 && ManagedWorkerId > 0 &&
        ElapsedMilliseconds > 300_000 && Calls > 4096 && RandomBytes > 4096 && Iterations > 0;

    /// <summary>Read at most8192 UTF-8 bytes containing exactly the nine service
    /// fields. Malformed JSON, unknown/duplicate/missing fields, wrong schema and
    /// nonintegral/out-of-range values throw InvalidDataException.</summary>
    public static ServiceStatus Parse(ReadOnlySpan<byte> body)
    {
        if (body.IsEmpty || body.Length > 8192) throw Invalid();
        try
        {
            var reader = new Utf8JsonReader(body, new JsonReaderOptions { MaxDepth = 2 });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) throw Invalid();
            Span<long> values = stackalloc long[8]; uint seen = 0; bool ended = false;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) { ended = true; break; }
                if (reader.TokenType != JsonTokenType.PropertyName) throw Invalid();
                int field = Field(ref reader); uint bit = 1U << field;
                if ((seen & bit) != 0 || !reader.Read()) throw Invalid();
                seen |= bit;
                if (field == 0)
                {
                    if (reader.TokenType != JsonTokenType.String || !reader.ValueTextEquals("clros.service.v1"u8)) throw Invalid();
                }
                else
                {
                    if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out long value) || value < 0 ||
                        (field == 1 && value > 4) || ((field is 2 or 3) && value > int.MaxValue)) throw Invalid();
                    values[field - 1] = value;
                }
            }
            if (!ended || seen != 0x1ff || reader.Read()) throw Invalid();
            return new(values);
        }
        catch (JsonException error) { throw new InvalidDataException("service_status_invalid", error); }
    }

    /// <summary>Require nonregressing successful telemetry from an earlier
    /// sample. A nonzero worker identity stays fixed; phase0/1/2/4 may skip
    /// transient states, while phase3 or any failure rejects either sample.
    /// Independent counters have no cross-counter or atomicity requirements.
    /// Violations throw InvalidDataException; no execution authority is added.</summary>
    public void RequireProgressFrom(ServiceStatus prior)
    {
        ArgumentNullException.ThrowIfNull(prior);
        if (Failure != 0 || prior.Failure != 0 || Phase == 3 || prior.Phase == 3 || Phase < prior.Phase ||
            prior.ManagedWorkerId != 0 && ManagedWorkerId != prior.ManagedWorkerId ||
            Calls < prior.Calls || RandomRequests < prior.RandomRequests || RandomBytes < prior.RandomBytes ||
            Iterations < prior.Iterations || ElapsedMilliseconds < prior.ElapsedMilliseconds)
            throw new InvalidDataException("service_status_progress");
    }

    private static int Field(ref Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals("contract"u8)) return 0;
        if (reader.ValueTextEquals("phase"u8)) return 1;
        if (reader.ValueTextEquals("failure"u8)) return 2;
        if (reader.ValueTextEquals("managedWorkerId"u8)) return 3;
        if (reader.ValueTextEquals("calls"u8)) return 4;
        if (reader.ValueTextEquals("randomRequests"u8)) return 5;
        if (reader.ValueTextEquals("randomBytes"u8)) return 6;
        if (reader.ValueTextEquals("iterations"u8)) return 7;
        if (reader.ValueTextEquals("elapsedMilliseconds"u8)) return 8;
        throw Invalid();
    }
    private static InvalidDataException Invalid() => new("service_status_invalid");
}
