using OS.Tools.QemuRunner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal static partial class Program
{
    private sealed class SerialTerminalGate
    {
        private readonly FileStream _publicStream;
        private readonly string[] _guestMarkers;
        private readonly string[] _forbiddenMarkers;
        private readonly List<byte> _line = new List<byte>();
        private readonly MemoryStream _withheld = new MemoryStream();
        private int _nextMarker;
        public SerialTerminalGate(FileStream publicStream, string[] guestMarkers, string[] forbiddenMarkers)
        {
            _publicStream = publicStream;
            _guestMarkers = guestMarkers;
            _forbiddenMarkers = forbiddenMarkers;
        }

        public bool TerminalWithheld
        {
            get; private set;
        }

        public void Accept(byte value)
        {
            _line.Add(value);
            if (value == (byte)'\n')
            {
                ProcessCompleteLine();
            }
        }

        public void ValidateCompleteMarkerContract()
        {
            if (!TerminalWithheld || _nextMarker != _guestMarkers.Length)
            {
                throw new InvalidDataException("marker_contract_incomplete");
            }
        }

        public void CompleteAfterStop()
        {
            if (_line.Count != 0)
            {
                if (!TerminalWithheld)
                {
                    throw new InvalidDataException("partial_line_before_terminal");
                }

                byte[] partial = _line.ToArray();
                _withheld.Write(partial, 0, partial.Length);
                _line.Clear();
            }
        }

        public byte[] GetWithheldBytes()
        {
            return _withheld.ToArray();
        }

        private void ProcessCompleteLine()
        {
            byte[] bytes = _line.ToArray();
            _line.Clear();
            string line = Encoding.UTF8.GetString(bytes).TrimEnd('\r', '\n');
            foreach (string forbidden in _forbiddenMarkers)
            {
                if (string.Equals(line, forbidden, StringComparison.Ordinal) || line.StartsWith(forbidden + " ", StringComparison.Ordinal))
                {
                    WriteCurrent(bytes);
                    throw new InvalidDataException("forbidden_marker_" + Sanitize(forbidden));
                }
            }

            if (string.Equals(line, "M01_OWNED_CR3_ENTERED", StringComparison.Ordinal) || line.StartsWith("M01_OWNED_CR3_ENTERED ", StringComparison.Ordinal))
            {
                WriteCurrent(bytes);
                throw new InvalidDataException("owned_cr3_serial_marker_forbidden");
            }

            int markerIndex = Array.IndexOf(_guestMarkers, line);
            if (markerIndex >= 0)
            {
                if (markerIndex != _nextMarker)
                {
                    WriteCurrent(bytes);
                    throw new InvalidDataException("marker_order_or_duplicate_" + Sanitize(line));
                }

                _nextMarker++;
            }

            if (string.Equals(line, SuccessTerminal, StringComparison.Ordinal))
            {
                if (TerminalWithheld)
                {
                    throw new InvalidDataException("duplicate_success_terminal");
                }

                TerminalWithheld = true;
                _withheld.Write(bytes, 0, bytes.Length);
                return;
            }

            WriteCurrent(bytes);
        }

        private void WriteCurrent(byte[] bytes)
        {
            if (TerminalWithheld)
            {
                _withheld.Write(bytes, 0, bytes.Length);
                return;
            }

            _publicStream.Write(bytes, 0, bytes.Length);
            _publicStream.Flush();
        }
    }
}
