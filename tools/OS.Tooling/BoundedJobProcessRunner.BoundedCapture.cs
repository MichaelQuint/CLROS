// Extracted from the reviewed EFI verifier helper; preserve native lifecycle semantics.
#nullable disable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling.Native
{
    public static partial class BoundedJobProcessRunner
    {
        private sealed class BoundedCapture
        {
            private readonly object sync = new object();
            private readonly StringBuilder standardOutput = new StringBuilder();
            private readonly StringBuilder standardError = new StringBuilder();
            private readonly int limit;
            private int characters;
            private bool overflow;
            private string failureType = String.Empty;
            private readonly Action<string, bool> outputReceived;
            internal BoundedCapture(int limit, Action<string, bool> outputReceived = null)
            {
                this.limit = limit;
                this.outputReceived = outputReceived;
            }

            internal bool Append(bool isStandardError, char[] buffer, int count)
            {
                string notification = null;
                bool complete;
                lock (sync)
                {
                    if (overflow || failureType.Length != 0)
                    {
                        return false;
                    }

                    int remaining = limit - characters;
                    int accepted = Math.Min(remaining, count);
                    if (accepted > 0)
                    {
                        StringBuilder target = isStandardError ? standardError : standardOutput;
                        target.Append(buffer, 0, accepted);
                        characters += accepted;
                        if (outputReceived != null)
                            notification = new string(buffer, 0, accepted);
                    }

                    if (accepted != count)
                    {
                        overflow = true;
                    }

                    complete = !overflow;
                }

                // A caller's callback must not hold the capture lock needed by
                // deadline/cancellation polling and termination diagnostics.
                if (notification != null)
                    outputReceived(notification, isStandardError);
                return complete;
            }

            internal void Fail(Exception exception)
            {
                lock (sync)
                {
                    if (failureType.Length == 0)
                    {
                        failureType = exception.GetType().Name;
                    }
                }
            }

            internal bool Overflow
            {
                get
                {
                    lock (sync)
                    {
                        return overflow;
                    }
                }
            }

            internal string FailureType
            {
                get
                {
                    lock (sync)
                    {
                        return failureType;
                    }
                }
            }

            internal int CharacterCount
            {
                get
                {
                    lock (sync)
                    {
                        return characters;
                    }
                }
            }

            internal string GetOutput()
            {
                lock (sync)
                {
                    return standardOutput.ToString() + standardError.ToString();
                }
            }

            internal string GetStandardOutput()
            {
                lock (sync)
                    return standardOutput.ToString();
            }

            internal string GetStandardError()
            {
                lock (sync)
                    return standardError.ToString();
            }
        }
    }
}
