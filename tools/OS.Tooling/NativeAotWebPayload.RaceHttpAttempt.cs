using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    private sealed class RaceHttpAttempt(int number, string verb, string target)
    {
        private int headerStatus, hasHeaders;
        public int id { get; } = number;
        public string method { get; } = verb;
        public string path { get; } = target;
        public int completion
        {
            get; set;
        }
        public bool headers
        {
            get => Volatile.Read(ref hasHeaders) != 0; set => Volatile.Write(ref hasHeaders, value ? 1 : 0);
        }
        public int status
        {
            get => Volatile.Read(ref headerStatus); set => Volatile.Write(ref headerStatus, value);
        }
        public string mediaType { get; set; } = "";
        public bool mediaTypeComplete { get; set; } = true;
        public string bodyBase64 { get; set; } = "";
        public bool bodyComplete
        {
            get; set;
        }
        public string outcome { get; set; } = "pending";
        public string error { get; set; } = "";
        internal string SignalVersion { get; set; } = "";
        internal string ChurnVersion { get; set; } = "";
    }
}
